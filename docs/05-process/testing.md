# Testing

> Purpose: Test frameworks, how to run them, and what the existing suites cover.
> Sources: E:\cinema\CLAUDE.md, E:\cinema\.github\workflows\ci.yml, E:\cinema\cinemabe\Cinema\2-Business\Cinema.Business.Tests\*.cs, E:\cinema\QA-tests\
> Last reviewed: 2026-11-01

## Backend — `Cinema.Business.Tests`

- Frameworks: **xUnit + Moq + FluentAssertions**.
- The project is **dual-purpose**: `<OutputType>Exe</OutputType>` with a `Program.cs` that seeds
  `admin@cinema.vn / Admin@123` and `user@cinema.vn / User@123` accounts when run directly, and a
  normal xUnit test suite when run via `dotnet test`.

```powershell
# From cinemabe/
dotnet test  Cinema/2-Business/Cinema.Business.Tests                # run all xUnit tests
dotnet run   --project Cinema/2-Business/Cinema.Business.Tests      # seed admin + user accounts instead

# Run a single test class or test
dotnet test Cinema/2-Business/Cinema.Business.Tests --filter "FullyQualifiedName~AuthServiceTests"
dotnet test Cinema/2-Business/Cinema.Business.Tests --filter "FullyQualifiedName=Cinema.Business.Tests.AuthServiceTests.Login_ValidCredentials_ReturnsAuthResponse"
```

### Test files present (as of this review)

Verified by listing `cinemabe/Cinema/2-Business/Cinema.Business.Tests/*.cs`:

| File | Area |
|---|---|
| `AuthServiceTests.cs` | Login/auth flows |
| `AuthUserTheaterTests.cs` | Staff-theater scoping on auth |
| `BookingServiceTests.cs` | Booking / seat-lock logic |
| `BookingConfirmationTemplateTests.cs` | Booking confirmation template rendering |
| `BoxOfficeTests.cs` | Box office / counter sale flow |
| `ComboServiceTests.cs` | Food & drink combo logic |
| `ConcessionTests.cs` | Concession (food & drink) catalog |
| `DtoValidationTests.cs` | DTO validation rules |
| `EntityTests.cs` | Entity-level behavior |
| `GateManagerTests.cs` | Gate admission / ticket scanning |
| `GiftCardServiceTests.cs` | Gift card issue/redeem |
| `LoyaltySettingServiceTests.cs` | Loyalty points conversion settings |
| `MovieCreditModelTests.cs` | Movie cast/crew credit model |
| `MovieServiceTests.cs` | Movie catalog logic |
| `OmdbRatingsProviderTests.cs` | External OMDb rating lookups |
| `OperationsBoardAndIncidentTests.cs` | Incident/operations board |
| `PatronCategoryServiceTests.cs` | Patron category pricing/eligibility |
| `PaymentCallbackTests.cs` | Generic payment callback handling |
| `PersonManagerTests.cs` | Cast/crew person management |
| `PromotionProgramServiceTests.cs` | Promotion program logic |
| `RoleNamesTests.cs` | Role-group → role mapping |
| `RoomSeatMapTests.cs` | Seat map editing |
| `SeatNumberingTests.cs` | Seat numbering/labeling |
| `ShowTimeServiceTests.cs` | Showtime scheduling validation |
| `SlugHelperTests.cs` | Slug generation |
| `SplashBannerServiceTests.cs` | Splash banner "active" computation |
| `StaffReportTests.cs` | Staff reporting |
| `TemplateManagerTests.cs` / `TemplateRendererTests.cs` | Notification template CRUD/rendering |
| `TicketPriceManagerTests.cs` | Ticket price multiplier logic |
| `TimeSlotManagerTests.cs` | Time slot management |
| `VnPayGatewayTests.cs` (+ `VnPayTestHelper.cs`) | VNPay gateway signature/parsing |
| `FixedClock.cs` | Test helper (not a test class) |

This list is substantially larger than the four classes named in `CLAUDE.md`
(`AuthServiceTests`, `BookingServiceTests`, `MovieServiceTests`, `EntityTests`) — those four are
still present, but the suite has grown well beyond them. <!-- TODO: confirm with team whether CLAUDE.md's test-file list should be updated to avoid underselling coverage. -->

## Frontend

- Framework: **vitest**, run through Angular's `@angular/build:unit-test` builder via `ng test`.

```powershell
# From cinemafe/
ng test CinemaLib
ng test CinemaUser
ng test CinemaAdmin
```

- CI (`.github/workflows/ci.yml`) runs `ng test CinemaLib` with coverage (`lcovonly` +
  `text-summary` reporters), then `ng test CinemaUser` and `ng test CinemaAdmin` without coverage.
- `CinemaStaff` is **not** run in CI's frontend job (only `CinemaLib`, `CinemaUser`, `CinemaAdmin`
  appear in `ci.yml`'s frontend/e2e jobs). <!-- TODO: confirm with team whether this is intentional or an oversight. -->

## QA-tests harness

`QA-tests/` is entirely **git-ignored** (`.gitignore` lines `QA-tests/` and `/QA-tests/`) — nothing
under it is committed, so what's on disk locally is a working snapshot, not shared project state.
`CLAUDE.md` describes two systems:

1. **Flow tests** — `/test-flow <flow-id>` runs static invariant checks + `dotnet build`/`dotnet test`
   + `ng build` + a manual E2E playbook for a business flow, logging to
   `QA-tests/FLOW-TEST-RESULTS.md` and `QA-tests/reports/<flow>-<date>/REPORT.html`. CLAUDE.md names
   four flows: `booking-seat-lock`, `auth-login`, `movie-admin`, `inventory-storage-plan`.
2. **Playwright E2E** — `QA-tests/playwright/` (CinemaUser :4202, CinemaAdmin :4201), `npm test` →
   HTML report via `npm run report`.

**What was actually verified on disk** (2026-11-01, this review):
- `QA-tests/flow-tests/` contains only `inventory-storage-plan.yaml` — no `booking-seat-lock.yaml`,
  `auth-login.yaml`, or `movie-admin.yaml`, and no `README.md`.
- `QA-tests/playwright/` contains a `specs/staff/shell.spec.ts` and a `node_modules/`, but no
  `package.json` or `README.md` at its root.
- `QA-tests/reports/` contains four historical HTML reports: `auth-login-2026-07-12`,
  `booking-seat-lock-2026-07-12`, `booking-seat-lock-2026-09-27`, `movie-admin-2026-07-12`.
- No `.claude/hooks/check-business-invariants.js` file and no hook wiring were found under
  `.claude/settings.json`.

This is a **discrepancy between CLAUDE.md's description and the current working tree** — see
`docs/00-overview/open-questions.md`. Because `QA-tests/` is git-ignored, this may simply reflect
an incomplete local checkout/setup rather than the harness having been removed.

## CI-run E2E (separate from the above)

`.github/workflows/ci.yml` has its own `e2e` job that is **not** the same as the `/test-flow`
flow-test system: it spins up SQL Server in a container, builds and seeds the database, starts the
Web API and both `CinemaUser`/`CinemaAdmin` dev servers, then runs
`QA-tests/playwright && npm ci && npx playwright install --with-deps chromium && npm test`,
uploading the HTML report as a build artifact. This confirms a Playwright suite is expected to
exist and run in `QA-tests/playwright/` even though a local checkout currently shows only one spec
file there (see above).

## What's not covered (only stated where verifiable)

- No `CinemaStaff` unit tests are run in CI (see above).
- No dedicated backend integration tests against a real database were found — the CI comment on
  the `dotnet test` step states "xUnit tests use Moq — no database required," i.e. the backend
  suite is unit-level only; database-dependent behavior is exercised only via the CI `e2e` job's
  Playwright run.
