import http from 'k6/http';
import { check } from 'k6';

export const API = __ENV.CINEMA_API_URL || 'http://localhost:5102';
const JSON_HEADERS = { headers: { 'Content-Type': 'application/json' } };

/** One browse-the-catalog iteration: the anonymous read path every visitor hits (no auth, so no auth rate limit). */
export function browseCatalog() {
  const paging = JSON.stringify({ pageIndex: 1, pageSize: 10 });
  const responses = http.batch([
    ['POST', `${API}/api/Cinema/GetNowShowingMovies`, paging, JSON_HEADERS],
    ['POST', `${API}/api/Cinema/GetComingSoonMovies`, paging, JSON_HEADERS],
    ['POST', `${API}/api/Cinema/GetTheaters`, paging, JSON_HEADERS],
    ['POST', `${API}/api/Cinema/GetShowTimes`, paging, JSON_HEADERS],
  ]);
  for (const res of responses) {
    check(res, { 'status is 200': (r) => r.status === 200 });
  }
}
