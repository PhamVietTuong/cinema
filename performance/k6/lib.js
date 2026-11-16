import http from 'k6/http';
import { check } from 'k6';

export const API = __ENV.CINEMA_API_URL || 'http://localhost:5102';
const JSON_HEADERS = { headers: { 'Content-Type': 'application/json' } };

/** One browse-the-catalog iteration: the anonymous read path every visitor hits (no auth, so no auth rate limit). */
export function browseCatalog() {
  const paging = JSON.stringify({ pageIndex: 1, pageSize: 10 });
  const responses = http.batch([
    ['POST', `${API}/api/Cinema/GetNowShowingMovies`, paging, { headers: JSON_HEADERS.headers, tags: { name: 'GetNowShowingMovies' } }],
    ['POST', `${API}/api/Cinema/GetComingSoonMovies`, paging, { headers: JSON_HEADERS.headers, tags: { name: 'GetComingSoonMovies' } }],
    ['POST', `${API}/api/Cinema/GetTheaters`, paging, { headers: JSON_HEADERS.headers, tags: { name: 'GetTheaters' } }],
    ['POST', `${API}/api/Cinema/GetShowTimes`, paging, { headers: JSON_HEADERS.headers, tags: { name: 'GetShowTimes' } }],
  ]);
  for (const res of responses) {
    check(res, { 'status is 200': (r) => r.status === 200 });
  }
}
