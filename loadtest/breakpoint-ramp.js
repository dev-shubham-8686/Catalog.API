import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

// Stepped ramp to find the actual sustainable ceiling of this deployment, rather than jumping
// straight to a target that overwhelms it. Each step holds for STEP_DURATION before increasing.
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const STEP_DURATION = __ENV.STEP_DURATION || '20s';
const MAX_VUS = parseInt(__ENV.MAX_VUS || '2000', 10);

// Item creation is Admin-only; see the same-named env vars in mixed-workload.js and
// loadtest/README.md for how to provide one or the other.
const ITEM_ID = __ENV.ITEM_ID || '';
const ADMIN_EMAIL = __ENV.ADMIN_EMAIL || '';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || '';

const errors = new Counter('errors_total');

export const options = {
  scenarios: {
    breakpoint_ramp: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 200,
      maxVUs: MAX_VUS,
      stages: [
        { target: 100, duration: STEP_DURATION },
        { target: 250, duration: STEP_DURATION },
        { target: 500, duration: STEP_DURATION },
        { target: 1000, duration: STEP_DURATION },
        { target: 1500, duration: STEP_DURATION },
        { target: 0, duration: '10s' },
      ],
    },
  },
};

export function setup() {
  const headers = { headers: { 'Content-Type': 'application/json' } };

  // Every endpoint now requires auth. Register + log in once here, outside the measured loop —
  // this script already isolates the cached-read path from login/write cost by never calling
  // those endpoints in the per-iteration loop below; this setup-time token is just what makes
  // the reads themselves possible now, not something being load tested.
  const email = `breakpoint-${Date.now()}@test.com`;
  const password = 'P@ssw0rd123!';
  http.post(`${BASE_URL}/api/auth/register`, JSON.stringify({ email, password }), headers);
  const loginRes = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ email, password }), headers);
  const token = loginRes.json('accessToken');
  const authHeaders = { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } };

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
      JSON.stringify({ name: 'Breakpoint Seed Item', description: 'seed', labelName: 'L', price: 1, format: 'CD', availableStock: 1000 }),
      { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` } }
    );
    itemId = createRes.json('id');
  }

  http.get(`${BASE_URL}/api/items/${itemId}`, authHeaders);
  return { itemId, token };
}

export default function (data) {
  // Cached-read only for this run — isolates "how far can the read path scale" from the much
  // more expensive password-hashing/DB-write paths, which are characterized separately.
  const res = http.get(`${BASE_URL}/api/items/${data.itemId}`, { headers: { Authorization: `Bearer ${data.token}` } });
  if (!check(res, { '200': (res) => res.status === 200 })) errors.add(1);
}
