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

## Baseline (2026-11-16, dev machine, API Release + Development logging, dev SQL Express, 25 VUs, with warm-up)

Zero errors, p95 about 130 ms, median about 45 ms. **p99 about 4.5 s and max about 10 s: threshold `p(99)<1000` fails.**
The stalls hit every endpoint at the same time (per-endpoint p99 3.6 to 5 s, except `GetTheaters`) while the slowest SQL
command in the API log was 142 ms, and a warm-up did not remove them. So it is not one slow query and not a cold start.
Not yet diagnosed; candidates: synchronous console logging at Information level (about 62k lines in this run), thread-pool
starvation, GC pauses, or the dev machine also hosting three `ng serve` processes. Next step: rerun with
`Logging__LogLevel__Default=Warning` and the apps stopped to separate these.
