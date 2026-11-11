# Cinema E2E tests

Playwright tests against a **real** stack — API + DB + the three Angular apps, no mocks. Same layout as
`augure-fe/e2e`: flat `tests/` folder, one shared `tests/helpers.ts`, a `global-setup.ts` that fails fast
when something is down, one chromium project.

## Prerequisites

Start everything on the same dev DB (Node 22 via nvm for the apps):

```bash
# 1. API — from cinemabe/  (http://localhost:5102)
dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost

# 2. Apps — from cinemafe/
npx ng serve CinemaAdmin --port 4201
npx ng serve CinemaUser  --port 4202
npx ng serve CinemaStaff --port 4203
```

Accounts (override with `E2E_{ADMIN,USER,STAFF}_{EMAIL,PASSWORD}`):

| Role | Default | Source |
|---|---|---|
| Admin (also used for CinemaStaff screens — `STAFF_APP_ROLES` includes Admin) | `admin@cinema.vn` / `Admin@123` | `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` |
| Customer | `khach01@cinema.vn` / `Khach01@123` | `cinemabe/sql/insert_db.sql` |
| Theater staff | `staff@cinema.vn` / `Staff@123` | seed (needs a theater) |

VNPay spec prerequisite: run the API with `Payments__VnPay__TmnCode` and `Payments__VnPay__HashSecret` set and export
the same secret as `E2E_VNPAY_HASH_SECRET`. Without it `api-payment-callback.spec.ts` skips locally and fails in CI.

Booking spec prerequisite: at least one upcoming showtime, see `e2e/sql/seed-future-showtime.sql`.

## Run

```bash
cd e2e
npm install
npx playwright install chromium
npm test                  # everything
npm run test:admin        # one app: api- / user- / admin- / staff- spec prefix
npx playwright test -g "gate"
```

Point at other hosts with `CINEMA_API_URL`, `CINEMA_USER_URL`, `CINEMA_ADMIN_URL`, `CINEMA_STAFF_URL`.

## Layout

| File | Covers |
|---|---|
| `tests/api-public-catalog.spec.ts` | health, `GetMovies` / `GetTheaters` paging, validation |
| `tests/api-auth.spec.ts` | login, 401 / 403 on admin endpoints |
| `tests/api-payment-callback.spec.ts` | VNPay IPN contract: bad signature gives 200 + `97`, signed unknown order gives 200 + `01` (needs `E2E_VNPAY_HASH_SECRET` = the API's `Payments:VnPay:HashSecret`; fails in CI when absent) |
| `tests/user-auth.spec.ts` | CinemaUser login/register/reset pages, route guards |
| `tests/user-discover.spec.ts` | home, movies, theaters, promotions, membership, chatbot |
| `tests/user-profile-booking.spec.ts` | profile, session persistence, showtime → seat map |
| `tests/admin-auth-guards.spec.ts` | CinemaAdmin login, guards, non-admin refusal |
| `tests/admin-screens.spec.ts` | all 22 routed admin screens + theater detail |
| `tests/staff-auth-screens.spec.ts` | CinemaStaff login, guards, all screens |
| `tests/helpers.ts` | URLs, accounts, `login`, session helpers, `expectRouteRenders`, `bearer` |
| `global-setup.ts` | reachability checks + one saved login per app (`.auth/`, git-ignored) |

## Notes

- Serial (`workers: 1`): specs share one dev DB.
- `/api/Identity/Login` allows 10 requests/min per IP, so specs that only need "a signed-in user" reuse the
  session saved by `global-setup.ts` (`test.use({ storageState: SESSION.admin })`) instead of logging in per test.
- Specs that need seeded data (a movie, an upcoming showtime, a theater) skip when there is none.
- Out of scope: real payment-gateway round trips (VNPay needs a public callback URL).
