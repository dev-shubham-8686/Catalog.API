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

const errors = new Counter('errors_total');
const cachedGetTrend = new Trend('get_item_cached_duration', true);
const listTrend = new Trend('get_list_duration', true);
const healthTrend = new Trend('health_duration', true);
const loginTrend = new Trend('login_duration', true);
const writeTrend = new Trend('create_item_duration', true);

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

  // Seed one item so the hot-path GET has something realistic (and cacheable) to read.
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
    headers
  );
  const itemId = createRes.json('id');

  // Warm the cache so most reads during the run hit Redis, not SQL Server — matches the
  // real-world assumption that hot items stay cached.
  http.get(`${BASE_URL}/api/items/${itemId}`);

  const email = `loadtest-${Date.now()}@test.com`;
  const password = 'P@ssw0rd123!';
  http.post(`${BASE_URL}/api/auth/register`, JSON.stringify({ email, password }), headers);

  return { itemId, email, password };
}

export default function (data) {
  const headers = { headers: { 'Content-Type': 'application/json' } };
  const r = Math.random();

  if (r < 0.5) {
    // 50% — cached single-item read (the hot path most real traffic looks like).
    const res = http.get(`${BASE_URL}/api/items/${data.itemId}`);
    cachedGetTrend.add(res.timings.duration);
    if (!check(res, { 'get item 200': (res) => res.status === 200 })) errors.add(1);
  } else if (r < 0.75) {
    // 25% — paginated list read.
    const res = http.get(`${BASE_URL}/api/items?pageSize=10&pageIndex=0`);
    listTrend.add(res.timings.duration);
    if (!check(res, { 'get list 200': (res) => res.status === 200 })) errors.add(1);
  } else if (r < 0.9) {
    // 15% — liveness check, no DB/cache/broker touch (the theoretical ceiling of this API host).
    const res = http.get(`${BASE_URL}/health/live`);
    healthTrend.add(res.timings.duration);
    if (!check(res, { 'health 200': (res) => res.status === 200 })) errors.add(1);
  } else if (r < 0.97) {
    // 7% — login (password hash verification + DB round trip).
    const res = http.post(
      `${BASE_URL}/api/auth/login`,
      JSON.stringify({ email: data.email, password: data.password }),
      headers
    );
    loginTrend.add(res.timings.duration);
    if (!check(res, { 'login 200': (res) => res.status === 200 })) errors.add(1);
  } else {
    // 3% — write path: DB insert + outbox row in one transaction, then a RabbitMQ publish.
    const res = http.post(
      `${BASE_URL}/api/items`,
      JSON.stringify({
        name: `LoadTest Item ${Date.now()}-${__VU}-${__ITER}`,
        description: 'created during load test',
        labelName: 'LoadTest',
        price: 1.23,
        format: 'CD',
        availableStock: 1,
      }),
      headers
    );
    writeTrend.add(res.timings.duration);
    if (!check(res, { 'create item 201': (res) => res.status === 201 })) errors.add(1);
  }
}
