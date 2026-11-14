# Testing

> Purpose: Test frameworks, how to run them, and what the existing suites cover.
> Sources: E:\cinema\AGENTS.md, E:\cinema\.github\workflows\ci.yml, E:\cinema\cinemabe\Cinema\2-Business\Cinema.Business.Tests\*.cs, E:\cinema\e2e\
> Last reviewed: 2026-11-14

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

As of 2026-11-14 the business suite has 660 passing tests and the WebApiHost suite 46.

Verified by listing `cinemabe/Cinema/2-Business/Cinema.Business.Tests/*.cs`:

| File | Area |
|---|---|
| `AuthServiceTests.cs` | Login/auth flows |
| `AuthUserTheaterTests.cs` | Staff-theater scoping on auth |
| `BookingServiceTests.cs` | Booking / seat-lock logic, including that tiers give no percentage discount (`TierMemberWithoutProgram_PaysListPrice_NoTierPercentage`, `TierMemberWithProgram_PaysProgramPriceAlone`) |
| `BookingConfirmationTemplateTests.cs` | Booking confirmation template rendering |
| `BoxOfficeTests.cs` | Box office / counter sale flow |
| `ComboServiceTests.cs` | Food & drink combo logic |
| `DtoValidationTests.cs` | DTO validation rules |
| `EntityTests.cs` | Entity-level behavior |
| `GateManagerTests.cs` | Gate admission / ticket scanning |
| `LoyaltySettingServiceTests.cs` | Loyalty points conversion settings |
| `MovieCreditModelTests.cs` | Movie cast/crew credit model |
| `MovieServiceTests.cs` | Movie catalog logic |
| `OmdbRatingsProviderTests.cs` | External OMDb rating lookups |
| `OperationsBoardAndIncidentTests.cs` | Incident/operations board |
| `PatronCategoryServiceTests.cs` | Patron category pricing/eligibility |
| `PaymentCallbackTests.cs` | Payment callback handling: idempotency and concurrent IPN delivery, expiry refunds (points, seats), `InitiatePaymentAsync` ownership/status/zero-amount cases (`ZeroAmountDue_FinalizesWithoutAnyGateway`) |
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

`AGENTS.md` (imported by `CLAUDE.md`) does not list test files, so the table above is the reference.

### Web API host tests — `Cinema.Service.WebApiHost.Tests`

| File | Area |
|---|---|
| `ControllerArchitectureTests.cs` | Exactly three controllers, each in its own Swagger group |
| `ControllerAuthorizationCoverageTests.cs` | Every HTTP action has `[Authorize]` (action or controller) or is on the explicit `AnonymousAllowList`; a new unguarded action fails the test by name, a stale allow-list entry fails too |
| `StaffToolsetAuthorizationTests.cs` | Role matrix for the staff endpoints (403, never 401) |
| `BookingHubTests.cs` | Seat-lock hub: lock refused when held, unlock broadcasts only a real release, disconnect broadcasts every held seat |
| `PaymentControllerCallbackTests.cs` | The exact VNPay IPN `{RspCode, Message}` per outcome |
| `GateControllerTests.cs`, `StaffScopeTests.cs` | Gate scan and theater scoping |

Adding a public endpoint that is meant to be anonymous: add its `Controller.Action` to `AnonymousAllowList` in
`ControllerAuthorizationCoverageTests.cs` in the same change, and say why in the review.

## Traceability — rule to tests

Every test that proves a business rule carries `[Trait("Req", "BR-xx-nn")]` (xUnit, class or method level) or a
Playwright `tag: ['@BR-xx-nn']`. The IDs are defined in the table below; the rule text is in
`docs/01-business/business-rules.md`. Class-level traits are coarse: a filter selects the whole class, not only the
tests that prove that rule. Filter by rule: `dotnet test --filter "Req=BR-PAY-03"`,
`npx playwright test --grep @BR-PAY-02`.

| ID | Rule | Section | Tests |
|---|---|---|---|
| `BR-PAY-01` | Payments are callback-authoritative; the callback's gateway must match the invoice's | business-rules s3 | `PaymentCallbackTests` |
| `BR-PAY-02` | The IPN answers 200 with the exact `{RspCode, Message}` per outcome (97 bad signature, 01 unknown order) | s3 | `VnPayGatewayTests`, `PaymentControllerCallbackTests`, e2e `api-payment-callback.spec.ts` |
| `BR-PAY-03` | Callback idempotency, including two concurrent deliveries: one payment row, points accrued once | s3 | `PaymentCallbackTests` (`..._TwoConcurrentDeliveries_...`). The claim atomicity (`InvoiceStore.TryTransitionStatusAsync`) is exercised only through mocks; there is no real-database test (tracked in `_bmad-output/implementation-artifacts/deferred-work.md`) |
| `BR-PAY-04` | Amount cross-check (VNPay amount x100) | s3 | `VnPayGatewayTests` |
| `BR-PAY-05` | Only a Pending invoice can be paid; expiry gives back points, seats | s2 | `PaymentCallbackTests`, `BookingServiceTests` |
| `BR-SEAT-01` | Advisory seat lock: refused while held, only the owner unlocks, disconnect releases | s1 | `BookingHubTests`, `BookingServiceTests`, e2e `user-profile-booking.spec.ts` (seat map opens only) |
| `BR-SEAT-02` | DB anti-double-booking: a lost insert race rolls back as "just booked"; spent points roll back | s1, s5 | `BookingServiceTests` |
| `BR-AUTH-01` | Every endpoint is guarded or deliberately anonymous; role mismatch is 403 | s6 | `ControllerAuthorizationCoverageTests`, `StaffToolsetAuthorizationTests` |
| `BR-AUTH-02` | Login lockout, account status, 2FA | s6 | `AuthServiceTests` |

Add a row (and the trait) when a new rule gets a test; do not reuse an ID for a different rule.

## Defect tracking

- Defects are **GitHub Issues**, not text files (`bugs.txt` / `tasks/bugs-to-fix.txt` are not used).
- Labels: one severity (`sev:blocker` money or data loss or security, `sev:major` a core flow broken,
  `sev:minor` workaround exists, `sev:trivial` cosmetic) plus an area (`area:payment`, `area:booking`, `area:auth`,
  `area:admin`, `area:staff`, `area:user`).
- Every fix lands with a regression test that fails without it, tagged with the rule ID it protects, and the commit
  or PR names the issue (`fixes #123`). A fix with no test says why in the PR.

## Frontend

- Framework: **vitest**, run through Angular's `@angular/build:unit-test` builder via `ng test`.

```powershell
# From cinemafe/
ng test CinemaLib
ng test CinemaUser
ng test CinemaAdmin
```

- CI (`.github/workflows/ci.yml`) runs `ng test CinemaLib` with coverage (`lcovonly` +
  `text-summary` reporters), then `ng test CinemaUser`, `ng test CinemaAdmin` and `ng test CinemaStaff`
  without coverage; `CinemaUser`, `CinemaAdmin` and `CinemaStaff` are all production-built.
- Backend CI runs two test projects: `Cinema.Business.Tests` (with coverage) and
  `Cinema.Service.WebApiHost.Tests` (controller architecture, role authorization, the endpoint-guard coverage
  check, the SignalR seat-lock hub contract, IPN contract). A coverage
  ratchet, `.github/scripts/check-coverage.py 60 Cinema.Business`, fails the build when the line coverage of
  the `Cinema.Business` package drops below 60% (it was 63.4% when the gate was added — raise the number as
  coverage grows, never lower it).

## Local QA harness (not part of the repository)

`QA-tests/` is **git-ignored** (`.gitignore` line 21 `QA-tests/` and line 27 `/QA-tests/`): local scratch, never committed and never run by CI. Do not rely on it or cite it as test coverage. The tracked, CI-run suite is `e2e/` (below). There is no committed `/test-flow` command:
`.claude/commands/` contains only `update-docs.md`.

## CI-run E2E

`.github/workflows/ci.yml` has its own `e2e` job that is **not** the `/test-flow` flow-test system: it
spins up SQL Server in a container, builds and seeds the database, starts the Web API and the
`CinemaUser`/`CinemaAdmin`/`CinemaStaff` dev servers, then runs the committed `e2e/` suite
(`npm ci && npx playwright install --with-deps chromium && npx playwright test --reporter=list,html`),
uploading `e2e/playwright-report` and `e2e/test-results` as a build artifact. The git-ignored
`QA-tests/playwright/` harness is local only and never runs in CI.

## Committed E2E suite — `e2e/`

`e2e/` is the tracked Playwright suite (78 tests in 9 files in `e2e/tests/` as of 2026-11-12, counted with `npx playwright test --list` from `e2e/`; the number changes whenever specs or their route arrays change, one chromium project, files prefixed
`api-` / `user-` / `admin-` / `staff-`; same layout as `augure-fe/e2e`) covering the Web API plus
CinemaUser/CinemaAdmin/CinemaStaff: auth + route guards, every routed screen, and the VNPay callback
contract. See `e2e/README.md` for prerequisites and env overrides. It runs serially against a seeded DB;
the customer account `khach01@cinema.vn` comes from `sql/insert_db.sql`.

Two specs cannot pass by skipping:

- `api-payment-callback.spec.ts` needs the API to run with `Payments:VnPay:TmnCode` and `HashSecret` set, and
  `E2E_VNPAY_HASH_SECRET` to hold the same secret (it signs its own IPNs with HMAC-SHA512 over the sorted,
  URL-encoded params). Bad signature must answer 200 + `97`; a signed IPN for an unknown order must answer 200 + `01`.
  Locally it is skipped when the variable is absent; with `CI` set it fails instead. The CI e2e job sets a
  throw-away merchant for both sides.
- `user-profile-booking.spec.ts` asks `SearchShowTimes` for a movie that has an upcoming showtime, opens it, picks
  a slot and expects the seat map. With no upcoming showtime it fails, naming `e2e/sql/seed-future-showtime.sql`.
  That idempotent script moves every showtime that has already started to the same time tomorrow; CI runs it after
  `insert_db.sql`. Run it by hand on a throw-away e2e database only, it rewrites showtime history.

## What's not covered (only stated where verifiable)

- No architecture test checks the direction of backend project references (`1-Service` to `2-Business` to `3-Data` to `4-Foundation`, `AGENTS.md`), unlike the controller count (`ControllerArchitectureTests`). The only upward reference is `Cinema.Data` to `Cinema.Business.Contracts` (ADR-0001). Also tracked in `_bmad-output/implementation-artifacts/deferred-work.md`.
- The CI `db-upgrade` job (`cinemabe/sql/tests/`: `run-upgrade-harness.sh`, `seed-old-schema.sql`, `assert-after-upgrade.sql`) upgrades a database built from the old `create_db.sql` (commit `3656d73`) with the current `upgrade_db.sql`, twice, and asserts the purges, drops and defaults. Run it locally with `SQL_PASSWORD` (and `SQL_SERVER`, `SQL_USER`) set; it uses the scratch database `CinemaUpgradeHarness`.
- No dedicated backend integration tests against a real database were found — the CI comment on
  the `dotnet test` step states "xUnit tests use Moq — no database required," i.e. the backend
  suite is unit-level only; database-dependent behavior is exercised only via the CI `e2e` job's
  Playwright run (`e2e/`).
