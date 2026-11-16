# Performance tests (k6)

Targets the anonymous catalog read path of the API (`GetNowShowingMovies`, `GetComingSoonMovies`, `GetTheaters`,
`GetShowTimes`). These endpoints are not behind the auth rate limiter, so load does not trip 429s.

Install k6 (`winget install k6`), start the API (prefer `-c Release`), then:

| Type | Command |
|---|---|
| Load | `k6 run performance/k6/load.js` |
| Stress | `k6 run performance/k6/stress.js` |
| Spike | `k6 run performance/k6/spike-endurance.js` |
| Endurance (soak) | `k6 run -e SCENARIO=soak performance/k6/spike-endurance.js` |

Override the target with `-e CINEMA_API_URL=http://host:5102`. Run against a dev/staging DB only; never production.
Redirect output to `E:\cinema\logs`, e.g. `k6 run ... > E:\cinema\logs\k6-load.txt`.

Not covered yet: booking creation and seat locking under contention (writes; needs seeded showtimes and a
rate-limit-aware login). Seat locks are a process-local `ConcurrentDictionary`, so that is the first thing to measure
before scaling the API horizontally.

## Baseline (2026-11-16, dev machine, API Release, dev SQL Express, 25 VUs, with warm-up)

Zero errors and p95 about 130 ms in every run, but **p99 3 to 6.6 s and max 10 to 14 s: `p(99)<1000` fails.** Findings:

- Not a cold start (warm-up did not help) and not logging (Serilog + Microsoft logging set to Warning, log silent: no change).
- Not one slow SQL command: the slowest command in the API log was 142 ms.
- The pattern depends on request concurrency. `browseCatalog()` sends 4 requests in parallel (`http.batch`), 25 VUs means about
  100 in flight, and every endpoint stalled together. Sending the same 4 requests sequentially (max 25 in flight) gave
  `GetComingSoonMovies` p99 123 ms, `GetShowTimes` 192 ms, `GetTheaters` 49 ms, overall p99 206 ms (passes).
- With that load shape **`GetNowShowingMovies` alone still has p99 9.7 s, max 14 s.** Its store query
  (`MovieStore.GetNowShowingAsync`) loads every now-showing movie with `Evaluations` as tracked entities and paginates in memory
  (`MovieManager.GetNowShowingAsync`), against the repo's own read-query convention (project columns, `AsNoTracking`, page in SQL).
  The dev DB is small (387 movies, 2 evaluations), so data volume alone does not explain 10 s: root cause **not confirmed**.
- Open suspects: connection-pool or thread-pool exhaustion under 100 concurrent requests, tracked-entity graph allocation (GC).
  Next steps: a quick experiment with `AsNoTracking` + SQL paging on that query; `dotnet-counters` (thread-pool queue length,
  GC pauses) during a run; try `Max Pool Size` on the connection string.
