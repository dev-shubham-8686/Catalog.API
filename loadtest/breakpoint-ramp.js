import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

// Stepped ramp to find the actual sustainable ceiling of this deployment, rather than jumping
// straight to a target that overwhelms it. Each step holds for STEP_DURATION before increasing.
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const STEP_DURATION = __ENV.STEP_DURATION || '20s';
const MAX_VUS = parseInt(__ENV.MAX_VUS || '2000', 10);

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
  const createRes = http.post(
    `${BASE_URL}/api/items`,
    JSON.stringify({ name: 'Breakpoint Seed Item', description: 'seed', labelName: 'L', price: 1, format: 'CD', availableStock: 1000 }),
    headers
  );
  const itemId = createRes.json('id');
  http.get(`${BASE_URL}/api/items/${itemId}`);
  return { itemId };
}

export default function (data) {
  // Cached-read only for this run — isolates "how far can the read path scale" from the much
  // more expensive password-hashing/DB-write paths, which are characterized separately.
  const res = http.get(`${BASE_URL}/api/items/${data.itemId}`);
  if (!check(res, { '200': (res) => res.status === 200 })) errors.add(1);
}
