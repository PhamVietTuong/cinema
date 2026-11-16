// Load test: expected peak traffic held steady. Pass/fail is enforced by thresholds.
//   k6 run performance/k6/load.js            (API on :5102, Release build recommended)
import { sleep } from 'k6';
import { browseCatalog } from './lib.js';

// A cold API (JIT, empty EF model cache, cold DB connections) makes the very first requests take seconds, which says
// nothing about steady-state capacity. A short warm-up scenario absorbs that; thresholds apply to the `load` scenario only.
export const options = {
  scenarios: {
    warmup: { executor: 'constant-vus', vus: 3, duration: '15s', exec: 'warmup' },
    load: {
      executor: 'ramping-vus',
      startTime: '15s',
      stages: [
        { duration: '30s', target: 25 },
        { duration: '2m', target: 25 },
        { duration: '15s', target: 0 },
      ],
    },
  },
  thresholds: {
    'http_req_failed{scenario:load}': ['rate<0.01'],
    'http_req_duration{scenario:load}': ['p(95)<500', 'p(99)<1000'],
  },
};

export function warmup() {
  browseCatalog();
  sleep(0.5);
}

export default function () {
  browseCatalog();
  sleep(1);
}
