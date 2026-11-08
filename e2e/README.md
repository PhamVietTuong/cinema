# Cinema E2E

Playwright suite covering the Web API and all three Angular apps. Tracked in git (unlike the local `QA-tests/` harness).

| Project | Target | Specs |
|---|---|---|
| `api` | Web API `:5102` | health, public catalog, auth/authorization, payment-callback signature contract |
| `user` | CinemaUser `:4202` | auth, route guards, discovery pages, movie detail, chatbot, profile, booking seat map |
| `admin` | CinemaAdmin `:4201` | login, guards, every routed screen (overview / catalog / operations), theater detail |
| `staff` | CinemaStaff `:4203` | login, guards, every routed screen (gate, POS, schedule, incidents, reports, audit log) |

## Prerequisites

1. SQL Server DB built from `cinemabe/sql/*.sql`.
2. Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (from `cinemabe/`) → `admin@cinema.vn`, `user@cinema.vn`.
3. API running on `http://localhost:5102`; apps served with `ng serve CinemaUser|CinemaAdmin|CinemaStaff` (Node 22 via nvm).

## Run

```bash
cd e2e
npm install
npx playwright install chromium
npm test                 # everything
npm run test:api         # one project
npm run report           # open the HTML report
```

Environment overrides: `CINEMA_API_URL`, `CINEMA_USER_URL`, `CINEMA_ADMIN_URL`, `CINEMA_STAFF_URL`,
`E2E_ADMIN_EMAIL/PASSWORD`, `E2E_USER_EMAIL/PASSWORD`, and `E2E_STAFF_EMAIL/PASSWORD` (the seed creates no
TheaterStaff account, so that one spec skips without them).

## Notes

- Runs serially (one worker): specs share one database.
- Data-dependent specs (movie detail, showtime → seat map, theater detail) skip when nothing is seeded.
- Admin is used for the staff-app screens because `STAFF_APP_ROLES` includes `Admin`.
- Out of scope: real payment-gateway round-trips (VNPay/MoMo/Stripe need public callbacks); only the callback contract is asserted.
- Keep reports/screenshots out of tracked folders; `playwright-report/` and `test-results/` are git-ignored here.
