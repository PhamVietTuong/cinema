// Stress test: ramp past expected load to find where latency/errors break. Informational thresholds only
// (abortOnFail stops the run once errors exceed 10%, i.e. the breaking point is reached).
import { sleep } from 'k6';
import { browseCatalog } from './lib.js';

export const options = {
  stages: [
    { duration: '1m', target: 50 },
    { duration: '1m', target: 100 },
    { duration: '1m', target: 200 },
    { duration: '1m', target: 400 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: [{ threshold: 'rate<0.1', abortOnTrigger: true }],
    http_req_duration: ['p(95)<2000'],
  },
};

export default function () {
  browseCatalog();
  sleep(0.5);
}
