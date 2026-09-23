"""
Real-time load test dashboard for Catalog API.

Mirrors the mixed workload used by loadtest/mixed-workload.js (k6): cached item reads,
paginated list reads, and health checks — weighted like real read-heavy traffic. Login and
registration happen exactly once, in setup(), to obtain the bearer token every request now
needs (every endpoint requires auth) — they are deliberately excluded from the measured
per-request mix so password-hashing cost doesn't skew what this script is trying to measure.
Unlike the k6 script, this renders a live-updating terminal dashboard (via `rich`) while the
test runs instead of only printing a summary at the end.

Usage:
    python load_test.py --base-url http://localhost:5000 --concurrency 50 --duration 60

Requires: pip install aiohttp rich
"""
from __future__ import annotations

import argparse
import asyncio
import random
import statistics
import time
import uuid
from collections import deque
from dataclasses import dataclass, field

import aiohttp
from rich.console import Console, Group
from rich.layout import Layout
from rich.live import Live
from rich.panel import Panel
from rich.table import Table

console = Console()


@dataclass
class EndpointStats:
    name: str
    total: int = 0
    success: int = 0
    failed: int = 0
    # Bounded so a long-running test doesn't grow memory unboundedly; recent samples are
    # what matter for a live "how are we doing right now" view anyway.
    latencies_ms: deque = field(default_factory=lambda: deque(maxlen=5000))
    errors: deque = field(default_factory=lambda: deque(maxlen=20))

    def record(self, latency_ms: float, ok: bool, error: str | None = None) -> None:
        self.total += 1
        self.latencies_ms.append(latency_ms)
        if ok:
            self.success += 1
        else:
            self.failed += 1
            if error:
                self.errors.append(error)

    def percentile(self, pct: float) -> float:
        if not self.latencies_ms:
            return 0.0
        data = sorted(self.latencies_ms)
        idx = min(len(data) - 1, int(len(data) * pct))
        return data[idx]


class Stats:
    def __init__(self) -> None:
        self.start_time = time.monotonic()
        self.endpoints: dict[str, EndpointStats] = {}
        self.recent_timestamps: deque = deque()  # for a rolling requests/sec figure
        self.lock = asyncio.Lock()

    async def record(self, endpoint: str, latency_ms: float, ok: bool, error: str | None = None) -> None:
        async with self.lock:
            stat = self.endpoints.setdefault(endpoint, EndpointStats(endpoint))
            stat.record(latency_ms, ok, error)
            self.recent_timestamps.append(time.monotonic())

    def rolling_rps(self, window_seconds: float = 3.0) -> float:
        now = time.monotonic()
        while self.recent_timestamps and now - self.recent_timestamps[0] > window_seconds:
            self.recent_timestamps.popleft()
        return len(self.recent_timestamps) / window_seconds

    @property
    def total_requests(self) -> int:
        return sum(s.total for s in self.endpoints.values())

    @property
    def total_success(self) -> int:
        return sum(s.success for s in self.endpoints.values())

    @property
    def total_failed(self) -> int:
        return sum(s.failed for s in self.endpoints.values())

    def all_latencies(self) -> list[float]:
        merged: list[float] = []
        for s in self.endpoints.values():
            merged.extend(s.latencies_ms)
        return merged


class Workload:
    """One shared, pre-seeded item id + bearer token, reused across all workers."""

    def __init__(self, base_url: str, item_id: str | None = None, admin_email: str | None = None, admin_password: str | None = None):
        self.base_url = base_url.rstrip("/")
        self.item_id = item_id
        self.admin_email = admin_email
        self.admin_password = admin_password
        self.email: str | None = None
        self.password = "P@ssw0rd123!"

    async def setup(self, session: aiohttp.ClientSession) -> None:
        console.print(f"[cyan]Seeding test data against {self.base_url} ...[/cyan]")

        # Every endpoint now requires auth. Register + log in exactly once, here — not part of
        # the measured per-request mix below (see WEIGHTS) — to get the bearer token every other
        # request needs. session.headers is applied to every subsequent request automatically.
        self.email = f"loadtest-{uuid.uuid4()}@test.com"
        async with session.post(
            f"{self.base_url}/api/auth/register",
            json={"email": self.email, "password": self.password},
        ) as resp:
            resp.raise_for_status()

        async with session.post(
            f"{self.base_url}/api/auth/login",
            json={"email": self.email, "password": self.password},
        ) as resp:
            resp.raise_for_status()
            token = (await resp.json())["accessToken"]
        session.headers["Authorization"] = f"Bearer {token}"

        if not self.item_id:
            # Item creation is Admin-only, and this load-test user is deliberately just a plain
            # registered user (no self-service admin escalation exists, by design) — so seeding
            # needs a pre-promoted Admin account. Pass --item-id instead to skip seeding entirely.
            if not self.admin_email or not self.admin_password:
                raise RuntimeError(
                    "No item to read: pass --item-id for an existing item, or "
                    "--admin-email/--admin-password for a pre-promoted Admin account so setup() "
                    "can seed one. See loadtest/python/README.md."
                )

            async with session.post(
                f"{self.base_url}/api/auth/login",
                json={"email": self.admin_email, "password": self.admin_password},
            ) as resp:
                resp.raise_for_status()
                admin_token = (await resp.json())["accessToken"]

            async with session.post(
                f"{self.base_url}/api/items",
                json={
                    "name": "LoadTest Seed Item",
                    "description": "seeded by python load_test.py",
                    "labelName": "LoadTest",
                    "price": 9.99,
                    "format": "CD",
                    "availableStock": 1000,
                },
                headers={"Authorization": f"Bearer {admin_token}"},
            ) as resp:
                resp.raise_for_status()
                body = await resp.json()
                self.item_id = body["id"]

        # Warm the cache so the read-heavy part of the mix mostly hits Redis, not SQL Server.
        async with session.get(f"{self.base_url}/api/items/{self.item_id}"):
            pass

        console.print(f"[green]Using item {self.item_id} and user {self.email}[/green]\n")


# (endpoint label, relative weight) — mirrors mixed-workload.js's weighting. Login/registration
# and the Admin-only write path are deliberately excluded (see module docstring).
WEIGHTS: list[tuple[str, float]] = [
    ("GET /api/items/{id} (cached)", 0.56),
    ("GET /api/items (list)", 0.28),
    ("GET /health/live", 0.16),
]


async def do_request(session: aiohttp.ClientSession, workload: Workload, stats: Stats) -> None:
    label = random.choices([w[0] for w in WEIGHTS], weights=[w[1] for w in WEIGHTS], k=1)[0]
    start = time.monotonic()
    ok = False
    error: str | None = None

    try:
        if label.startswith("GET /api/items/{id}"):
            async with session.get(f"{workload.base_url}/api/items/{workload.item_id}") as resp:
                ok = resp.status == 200
                if not ok:
                    error = f"HTTP {resp.status}"
        elif label.startswith("GET /api/items (list)"):
            async with session.get(f"{workload.base_url}/api/items", params={"pageSize": 10, "pageIndex": 0}) as resp:
                ok = resp.status == 200
                if not ok:
                    error = f"HTTP {resp.status}"
        else:  # GET /health/live
            async with session.get(f"{workload.base_url}/health/live") as resp:
                ok = resp.status == 200
                if not ok:
                    error = f"HTTP {resp.status}"
    except Exception as ex:  # network errors, timeouts, connection refused, etc.
        error = type(ex).__name__

    latency_ms = (time.monotonic() - start) * 1000
    await stats.record(label, latency_ms, ok, error)


async def worker(session: aiohttp.ClientSession, workload: Workload, stats: Stats, stop_at: float) -> None:
    while time.monotonic() < stop_at:
        await do_request(session, workload, stats)


def render_dashboard(stats: Stats, duration: int, concurrency: int, base_url: str) -> Group:
    elapsed = time.monotonic() - stats.start_time
    remaining = max(0.0, duration - elapsed)

    header = Panel(
        f"[bold]Catalog API Load Test[/bold]   target: {base_url}   concurrency: {concurrency}\n"
        f"elapsed: {elapsed:5.1f}s   remaining: {remaining:5.1f}s   "
        f"[bold cyan]current RPS: {stats.rolling_rps():6.1f}[/bold cyan]   "
        f"avg RPS: {stats.total_requests / elapsed if elapsed > 0 else 0:6.1f}",
        style="white on dark_blue",
    )

    total = stats.total_requests
    success = stats.total_success
    failed = stats.total_failed
    success_pct = (success / total * 100) if total else 0.0
    all_lat = stats.all_latencies()

    summary = Table.grid(padding=(0, 2))
    summary.add_column(justify="right", style="bold")
    summary.add_column()
    summary.add_row("Total requests:", f"{total}")
    summary.add_row("Success:", f"[green]{success}[/green] ({success_pct:.1f}%)")
    summary.add_row("Failed:", f"[red]{failed}[/red]" if failed else "0")
    if all_lat:
        sorted_lat = sorted(all_lat)
        p50 = sorted_lat[int(len(sorted_lat) * 0.50)]
        p95 = sorted_lat[int(len(sorted_lat) * 0.95)]
        p99 = sorted_lat[min(len(sorted_lat) - 1, int(len(sorted_lat) * 0.99))]
        summary.add_row("Latency avg / p50 / p95 / p99:", f"{statistics.mean(all_lat):.1f}ms / {p50:.1f}ms / {p95:.1f}ms / {p99:.1f}ms")

    table = Table(title="By endpoint", expand=True)
    table.add_column("Endpoint")
    table.add_column("Requests", justify="right")
    table.add_column("Success %", justify="right")
    table.add_column("Avg ms", justify="right")
    table.add_column("p95 ms", justify="right")
    table.add_column("Last error", overflow="fold")

    for label, _ in WEIGHTS:
        stat = stats.endpoints.get(label)
        if not stat or stat.total == 0:
            table.add_row(label, "0", "-", "-", "-", "")
            continue
        pct = stat.success / stat.total * 100
        avg_ms = statistics.mean(stat.latencies_ms) if stat.latencies_ms else 0
        pct_style = "green" if pct >= 99 else ("yellow" if pct >= 90 else "red")
        last_error = stat.errors[-1] if stat.errors else ""
        table.add_row(
            label,
            str(stat.total),
            f"[{pct_style}]{pct:.1f}%[/{pct_style}]",
            f"{avg_ms:.1f}",
            f"{stat.percentile(0.95):.1f}",
            last_error,
        )

    return Group(header, Panel(summary, title="Overall"), table)


async def run(base_url: str, concurrency: int, duration: int, item_id: str | None, admin_email: str | None, admin_password: str | None) -> None:
    stats = Stats()
    workload = Workload(base_url, item_id=item_id, admin_email=admin_email, admin_password=admin_password)

    connector = aiohttp.TCPConnector(limit=concurrency + 10)
    timeout = aiohttp.ClientTimeout(total=30)

    async with aiohttp.ClientSession(connector=connector, timeout=timeout) as session:
        await workload.setup(session)

        stop_at = time.monotonic() + duration
        workers = [asyncio.create_task(worker(session, workload, stats, stop_at)) for _ in range(concurrency)]

        with Live(render_dashboard(stats, duration, concurrency, base_url), console=console, refresh_per_second=4) as live:
            while time.monotonic() < stop_at:
                live.update(render_dashboard(stats, duration, concurrency, base_url))
                await asyncio.sleep(0.25)
            live.update(render_dashboard(stats, duration, concurrency, base_url))

        await asyncio.gather(*workers)

    console.print("\n[bold green]Done.[/bold green]")
    console.print(f"Total requests: {stats.total_requests}  |  Failed: {stats.total_failed}  |  "
                   f"Overall RPS: {stats.total_requests / duration:.1f}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Real-time load test dashboard for Catalog API")
    parser.add_argument("--base-url", default="http://localhost:5000", help="API base URL")
    parser.add_argument("--concurrency", type=int, default=50, help="Number of concurrent workers")
    parser.add_argument("--duration", type=int, default=60, help="Test duration in seconds")
    parser.add_argument("--item-id", default=None, help="Existing item id to read instead of seeding a new one")
    parser.add_argument("--admin-email", default=None, help="Pre-promoted Admin account email, used only to seed an item if --item-id is not given")
    parser.add_argument("--admin-password", default=None, help="Password for --admin-email")
    args = parser.parse_args()

    try:
        asyncio.run(run(args.base_url, args.concurrency, args.duration, args.item_id, args.admin_email, args.admin_password))
    except KeyboardInterrupt:
        console.print("\n[yellow]Interrupted.[/yellow]")


if __name__ == "__main__":
    main()
