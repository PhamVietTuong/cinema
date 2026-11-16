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
