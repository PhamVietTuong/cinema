// Load test: expected peak traffic held steady. Pass/fail is enforced by thresholds.
//   k6 run performance/k6/load.js            (API on :5102, Release build recommended)
import { sleep } from 'k6';
import { browseCatalog } from './lib.js';

export const options = {
  stages: [
    { duration: '30s', target: 25 },
    { duration: '2m', target: 25 },
    { duration: '15s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
  },
};

export default function () {
  browseCatalog();
  sleep(1);
}
