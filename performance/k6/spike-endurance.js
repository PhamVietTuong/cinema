// Spike (sudden surge, e.g. a blockbuster on sale) then soak (endurance, memory/connection leaks).
// Choose with SCENARIO=spike|soak (default spike):  k6 run -e SCENARIO=soak performance/k6/spike-endurance.js
import { sleep } from 'k6';
import { browseCatalog } from './lib.js';

const spike = [
  { duration: '20s', target: 10 },
  { duration: '10s', target: 300 },
  { duration: '1m', target: 300 },
  { duration: '10s', target: 10 },
  { duration: '1m', target: 10 },
];
const soak = [
  { duration: '1m', target: 20 },
  { duration: '30m', target: 20 },
  { duration: '30s', target: 0 },
];

export const options = {
  stages: __ENV.SCENARIO === 'soak' ? soak : spike,
  thresholds: { http_req_failed: ['rate<0.02'], http_req_duration: ['p(95)<1500'] },
};

export default function () {
  browseCatalog();
  sleep(1);
}
