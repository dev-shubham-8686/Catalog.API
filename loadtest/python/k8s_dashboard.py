"""
Real-time terminal dashboard for the catalog k8s namespace: pods, CPU/memory usage,
Deployments/HPA status, and current ConfigMap/Secret config — refreshed continuously.

This is a lightweight, scriptable complement to `k9s` (which is the recommended full-featured
tool for interactive cluster exploration — `k9s -n catalog`). Use this one when you just want a
plain, unattended, "leave it running in a terminal" status view, or want to script/extend it.

Shells out to `kubectl` under the hood — no Kubernetes Python client dependency needed.

Usage:
    python k8s_dashboard.py --namespace catalog --interval 2

Requires: pip install rich   (kubectl must be on PATH and pointed at the right cluster/context)
"""
from __future__ import annotations

import argparse
import json
import subprocess
import time
from datetime import datetime, timezone

from rich.console import Console, Group
from rich.live import Live
from rich.panel import Panel
from rich.table import Table

console = Console()


def kubectl(*args: str) -> str:
    result = subprocess.run(
        ["kubectl", *args],
        capture_output=True,
        text=True,
        timeout=10,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"kubectl {' '.join(args)} failed")
    return result.stdout


def kubectl_json(*args: str) -> dict:
    return json.loads(kubectl(*args, "-o", "json"))


def format_age(creation_timestamp: str) -> str:
    if not creation_timestamp:
        return "-"
    created = datetime.fromisoformat(creation_timestamp.replace("Z", "+00:00"))
    delta = datetime.now(timezone.utc) - created
    seconds = int(delta.total_seconds())
    if seconds < 60:
        return f"{seconds}s"
    if seconds < 3600:
        return f"{seconds // 60}m"
    if seconds < 86400:
        return f"{seconds // 3600}h{(seconds % 3600) // 60}m"
    return f"{seconds // 86400}d"


def try_kubectl(*args: str) -> str | None:
    try:
        return kubectl(*args)
    except Exception:
        return None


def render_pods_table(namespace: str) -> Table:
    table = Table(title=f"Pods — namespace: {namespace}", expand=True)
    table.add_column("Pod")
    table.add_column("Ready")
    table.add_column("Status")
    table.add_column("Restarts", justify="right")
    table.add_column("CPU", justify="right")
    table.add_column("Memory", justify="right")
    table.add_column("Age")

    try:
        pods = kubectl_json("get", "pods", "-n", namespace)["items"]
    except Exception as ex:
        table.add_row(f"[red]error: {ex}[/red]", "", "", "", "", "", "")
        return table

    # `kubectl top` needs metrics-server; degrade gracefully to "-" if it's not available.
    top_by_pod: dict[str, tuple[str, str]] = {}
    top_output = try_kubectl("top", "pods", "-n", namespace, "--no-headers")
    if top_output:
        for line in top_output.strip().splitlines():
            parts = line.split()
            if len(parts) >= 3:
                top_by_pod[parts[0]] = (parts[1], parts[2])

    for pod in pods:
        name = pod["metadata"]["name"]
        status = pod["status"].get("phase", "Unknown")
        containers = pod["status"].get("containerStatuses", [])
        ready_count = sum(1 for c in containers if c.get("ready"))
        restarts = sum(c.get("restartCount", 0) for c in containers)
        cpu, mem = top_by_pod.get(name, ("-", "-"))

        age = format_age(pod["metadata"].get("creationTimestamp", ""))
        status_style = "green" if status == "Running" else ("yellow" if status == "Pending" else "red")

        table.add_row(
            name,
            f"{ready_count}/{len(containers)}",
            f"[{status_style}]{status}[/{status_style}]",
            str(restarts) if restarts == 0 else f"[yellow]{restarts}[/yellow]",
            cpu,
            mem,
            age,
        )

    return table


def render_workloads_table(namespace: str) -> Table:
    table = Table(title="Deployments & Autoscalers", expand=True)
    table.add_column("Deployment")
    table.add_column("Replicas (ready/desired)")
    table.add_column("HPA target")
    table.add_column("HPA current")
    table.add_column("Min/Max")

    try:
        deployments = kubectl_json("get", "deployments", "-n", namespace)["items"]
    except Exception as ex:
        table.add_row(f"[red]error: {ex}[/red]", "", "", "", "")
        return table

    hpas_by_target: dict[str, dict] = {}
    try:
        for hpa in kubectl_json("get", "hpa", "-n", namespace)["items"]:
            hpas_by_target[hpa["spec"]["scaleTargetRef"]["name"]] = hpa
    except Exception:
        pass

    for dep in deployments:
        name = dep["metadata"]["name"]
        desired = dep["spec"].get("replicas", 0)
        ready = dep["status"].get("readyReplicas", 0)
        replicas_style = "green" if ready == desired else "yellow"

        hpa = hpas_by_target.get(name)
        if hpa:
            # currentMetrics entries aren't always populated with a full "resource" payload
            # right after the HPA is created (or if metrics-server hasn't reported yet) — find
            # the first Resource-type metric defensively rather than assuming index 0 is it.
            def resource_utilization(metric_list: list[dict], value_key: str) -> str | None:
                for metric in metric_list:
                    resource = metric.get("resource")
                    if resource and value_key in resource:
                        util = resource[value_key].get("averageUtilization")
                        if util is not None:
                            return f"{util}% CPU"
                return None

            target = resource_utilization(hpa["spec"].get("metrics", []), "target") or "-"
            current = resource_utilization(hpa["status"].get("currentMetrics", []), "current") or "collecting..."
            min_max = f"{hpa['spec']['minReplicas']}/{hpa['spec']['maxReplicas']}"
        else:
            target = current = min_max = "-"

        table.add_row(
            name,
            f"[{replicas_style}]{ready}/{desired}[/{replicas_style}]",
            target,
            current,
            min_max,
        )

    return table


def render_config_panel(namespace: str) -> Panel:
    lines: list[str] = []

    try:
        configmaps = kubectl_json("get", "configmaps", "-n", namespace)["items"]
        for cm in configmaps:
            name = cm["metadata"]["name"]
            # Every namespace gets an auto-generated "kube-root-ca.crt" ConfigMap holding the
            # cluster CA cert — not app config, just noise for this view.
            if name == "kube-root-ca.crt":
                continue
            lines.append(f"[bold]ConfigMap {name}[/bold]")
            for key, value in (cm.get("data") or {}).items():
                lines.append(f"  {key} = {value}")
    except Exception as ex:
        lines.append(f"[red]configmaps error: {ex}[/red]")

    try:
        secrets = kubectl_json("get", "secrets", "-n", namespace)["items"]
        for secret in secrets:
            if secret.get("type") != "Opaque":
                continue
            keys = ", ".join((secret.get("data") or {}).keys())
            # Keys only, never values — this is a live shared dashboard, not a secrets viewer.
            lines.append(f"[bold]Secret {secret['metadata']['name']}[/bold] (keys): {keys}")
    except Exception as ex:
        lines.append(f"[red]secrets error: {ex}[/red]")

    return Panel("\n".join(lines) or "(none found)", title="Config (ConfigMaps + Secret key names)")


def render(namespace: str) -> Group:
    header = Panel(
        f"[bold]k8s live dashboard[/bold]   namespace: {namespace}   "
        f"refreshed: {time.strftime('%H:%M:%S')}\n"
        f"(full interactive alternative: [cyan]k9s -n {namespace}[/cyan])",
        style="white on dark_blue",
    )
    return Group(header, render_pods_table(namespace), render_workloads_table(namespace), render_config_panel(namespace))


def main() -> None:
    parser = argparse.ArgumentParser(description="Real-time k8s dashboard for the catalog namespace")
    parser.add_argument("--namespace", default="catalog")
    parser.add_argument("--interval", type=float, default=2.0, help="Refresh interval in seconds")
    parser.add_argument("--duration", type=float, default=0, help="Stop automatically after N seconds (0 = run until Ctrl+C)")
    args = parser.parse_args()

    try:
        kubectl("get", "namespace", args.namespace)
    except Exception as ex:
        console.print(f"[red]Cannot reach namespace '{args.namespace}': {ex}[/red]")
        console.print("[yellow]Is a cluster running and is kubectl pointed at it? (kubectl config current-context)[/yellow]")
        return

    stop_at = time.monotonic() + args.duration if args.duration > 0 else None

    try:
        with Live(render(args.namespace), console=console, refresh_per_second=1) as live:
            while stop_at is None or time.monotonic() < stop_at:
                live.update(render(args.namespace))
                time.sleep(args.interval)
    except KeyboardInterrupt:
        console.print("\n[yellow]Stopped.[/yellow]")


if __name__ == "__main__":
    main()
