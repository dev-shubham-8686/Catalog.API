import http from 'k6/http';
import { check } from 'k6';
import { Counter, Trend } from 'k6/metrics';

// Configurable via environment variables so the same script works locally, against the k8s
// cluster, or later against real scaled infrastructure.
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const TARGET_RPS = parseInt(__ENV.TARGET_RPS || '2000', 10);
const MAX_VUS = parseInt(__ENV.MAX_VUS || '3000', 10);
const RAMP_DURATION = __ENV.RAMP_DURATION || '30s';
const SUSTAIN_DURATION = __ENV.SUSTAIN_DURATION || '60s';

// Item creation is Admin-only, and this script's own load-test user is a plain registered user
// (on purpose — see setup()). Provide ONE of these so setup() has something to read:
//   ITEM_ID                       — an item that already exists; setup() just reads it.
//   ADMIN_EMAIL + ADMIN_PASSWORD  — a pre-promoted Admin account; setup() logs in and seeds a
//                                   fresh item with it (see README.md for how to promote a user).
const ITEM_ID = __ENV.ITEM_ID || '';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || '';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || '';

const errors = new Counter('errors_total');
const cachedGetTrend = new Trend('get_item_cached_duration', true);
const listTrend = new Trend('get_list_duration', true);
const healthTrend = new Trend('health_duration', true);

export const options = {
  scenarios: {
    mixed_workload: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: Math.min(300, MAX_VUS),
      maxVUs: MAX_VUS,
      stages: [
        { target: TARGET_RPS, duration: RAMP_DURATION },
        { target: TARGET_RPS, duration: SUSTAIN_DURATION },
        { target: 0, duration: '10s' },
      ],
    },
  },
  thresholds: {
    // Informational, not a hard pass/fail gate for this exploratory run — k6 will still report
    // the full summary either way.
    http_req_failed: ['rate<0.5'],
  },
};

export function setup() {
  const headers = { headers: { 'Content-Type': 'application/json' } };

  // Every endpoint now requires auth. Register + log in exactly once here, outside the measured
  // loop — login/registration are a one-time setup cost, not something we want to measure or
  // exercise per-request, so they're deliberately excluded from the request mix below (that also
  // removes the password-hashing cost from skewing the numbers this script is actually trying to
  // characterize: read/write throughput, not auth throughput).
  const email = `loadtest-${Date.now()}@test.com`;
  const password = 'P@ssw0rd123!';
  http.post(`${BASE_URL}/api/auth/register`, JSON.stringify({ email, password }), headers);
  const loginRes = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ email, password }), headers);
  const token = loginRes.json('accessToken');

  const authHeaders = { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } };

  // Seed one item so the hot-path GET has something realistic (and cacheable) to read. Item
  // creation is Admin-only, and this script's own load-test user is deliberately just a plain
  // registered user (no self-service admin escalation exists, by design) — so seeding needs
  // either a pre-existing item or a separately-provisioned Admin token. See the ITEM_ID /
  // ADMIN_EMAIL / ADMIN_PASSWORD env vars above and README.md for how to get one.
  let itemId = ITEM_ID;
  if (!itemId) {
    if (!ADMIN_EMAIL || !ADMIN_PASSWORD) {
      throw new Error(
        'No item to read: set ITEM_ID to an existing item, or ADMIN_EMAIL + ADMIN_PASSWORD ' +
        'for a pre-promoted Admin account so setup() can seed one. See loadtest/README.md.'
      );
    }
    const adminLoginRes = http.post(
      `${BASE_URL}/api/auth/login`,
      JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASSWORD }),
      headers
    );
    const adminToken = adminLoginRes.json('accessToken');
    const createRes = http.post(
      `${BASE_URL}/api/items`,
      JSON.stringify({
        name: 'LoadTest Seed Item',
        description: 'seeded by k6 setup()',
        labelName: 'LoadTest',
        price: 9.99,
        format: 'CD',
        availableStock: 1000,
      }),
      { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` } }
    );
    itemId = createRes.json('id');
  }

  // Warm the cache so most reads during the run hit Redis, not SQL Server — matches the
  // real-world assumption that hot items stay cached.
  http.get(`${BASE_URL}/api/items/${itemId}`, authHeaders);

  return { itemId, token };
}

export default function (data) {
  const headers = { headers: { Authorization: `Bearer ${data.token}` } };
  const r = Math.random();

  if (r < 0.56) {
    // 56% — cached single-item read (the hot path most real traffic looks like).
    const res = http.get(`${BASE_URL}/api/items/${data.itemId}`, headers);
    cachedGetTrend.add(res.timings.duration);
    if (!check(res, { 'get item 200': (res) => res.status === 200 })) errors.add(1);
  } else if (r < 0.84) {
    // 28% — paginated list read.
    const res = http.get(`${BASE_URL}/api/items?pageSize=10&pageIndex=0`, headers);
    listTrend.add(res.timings.duration);
    if (!check(res, { 'get list 200': (res) => res.status === 200 })) errors.add(1);
  } else {
    // 16% — liveness check, no DB/cache/broker touch (the theoretical ceiling of this API host).
    const res = http.get(`${BASE_URL}/health/live`, headers);
    healthTrend.add(res.timings.duration);
    if (!check(res, { 'health 200': (res) => res.status === 200 })) errors.add(1);
  }
}
