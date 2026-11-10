<!-- bmad:context -->
<!-- Verified 2026-11-04 against 7bd1808fde0c08e6cba7ec23b65f34c4ddc88631. Managed by bmad-project-context; edits inside this block are replaced on refresh. Keep anything you want preserved outside the markers. -->

## cinema

Full-stack cinema booking platform. Monorepo: `cinemabe/` (.NET 9 Web API + EF Core + SQL Server, `Cinema.sln`), `cinemafe/` (Angular 21 workspace, `angular.json`, 4 sub-projects).

## Policy

- Never add a 4th controller or split `CinemaController` into partial files — only `CinemaController`, `IdentityController`, `PaymentController` may exist (enforced by `ControllerArchitectureTests.OnlyThreeControllersExist`); new endpoints go into `CinemaController` under a `#region <Area>`.
- Never hand-edit NSwag-generated files (`Cinema.Service.Clients/**`, `projects/CinemaLib/src/lib/services/*-http.service.ts`) — regenerate via `cinemabe/Cinema/1-Service/Cinema.Service.Clients/Generator/GenerateNswag.ps1`.
- Never reference a higher-numbered backend layer from a lower one (`1-Service`→`2-Business`→`3-Data`→`4-Foundation`); DTOs stay in `Cinema.Business.DTO`, entities in `Cinema.Data.Entities`.
- Schema changes go by hand into `cinemabe/sql/create_db.sql` and `upgrade_db.sql`, applied directly to the dev DB — never `dotnet ef migrations add`.
- `docs/` is the committed, living knowledge base (business rules, API spec, schema, features, config) — when a change alters a business rule, endpoint, schema, feature, permission, or config documented there, update the matching `docs/` file in the same change, not as a follow-up.
- Never commit secrets — DB connection strings / JWT secrets go in git-ignored `appsettings.Development.json` / `appsettings.Production.json`.
- VNPay (and other gateways) are callback-authoritative: only the signed server-to-server IPN/webhook can mark an invoice Paid, never the browser redirect. The IPN response must be the exact `{"RspCode": "..", "Message": ".."}` shape or VNPay retries for 50 minutes.

## Where things are

- Docs: start at `docs/README.md` → `docs/03-technical/codebase-map.md` → `docs/01-business/business-rules.md`. Run `/update-docs` to force a review of a session's changes against `docs/`.
- Backend entry: `cinemabe/Cinema.sln`; frontend entry: `cinemafe/angular.json` (`CinemaUser`:4202, `CinemaAdmin`:4201, `CinemaStaff`:4203, `CinemaLib` shared lib).
- Seat-locking state lives in a static `ConcurrentDictionary` inside `BookingManager` — process-local, not distributed.
- `ClaimsPrincipalExtensions.GetUserId()` is the canonical way to read the authenticated user's GUID in controllers.
- VNPay sandbox testing requires a public tunnel (`ngrok http 5102`) since VNPay's servers can't reach `localhost`; set the IPN URL in the sandbox portal each time the tunnel restarts.

## Running and verifying

- Backend: `dotnet build Cinema.sln`; run via `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102); tests via `dotnet test Cinema/2-Business/Cinema.Business.Tests`; seed `admin@cinema.vn`/`user@cinema.vn` via `dotnet run --project Cinema/2-Business/Cinema.Business.Tests`.
- Frontend: system Node is v14 (too old) — use nvm v22.12.0 (`.nvmrc`) to build/serve. Rebuild `CinemaLib` (`ng build CinemaLib`) before consuming apps pick up its changes.
- Angular 21 apps are zoneless (no zone.js) — data set in a `subscribe()` callback needs `ChangeDetectorRef.markForCheck()` to render.
- NSwag regen must run from its own `Generator/` directory with the Web API host stopped, or it fails with path/lock errors.
- Seeding Vietnamese text via `sqlcmd -i` mangles `N'...'` literals unless run with `-f 65001`; verify with `-o file -u` + a PowerShell Unicode read, not bash terminal output.
- E2E tests: `QA-tests/playwright/` (CinemaUser :4202, CinemaAdmin :4201) — `npm test`, HTML report via `npm run report`.

## Conventions that differ from defaults

- Every control-flow body (`if`/`else`/`for`/`foreach`/`while`/`do`/`using`/`lock`) uses `{ }`, even for one line — no braceless shortcuts anywhere, including `?? throw` and ternaries.
- All list/search endpoints take `PagingSearchDTO` in the POST body and return `DefaultSearchResults<T>`, even for pure queries.
- Controllers use try/catch + `LogProvider.Current` (`"{Type}.{Method} being awakened…"` on entry, `Fatal` on exception, `StatusCode(500, e.Message)`).
- New entities pick up SQL `datetime` (not `datetime2`) automatically via `CinemaContext.OnModelCreating` — don't override.
- Read-only EF queries: project only needed columns with `.Select(...)` + `AsNoTracking()`, filter in SQL not in-memory, use `CountAsync`/`ExistsAsync` instead of materializing, never call a store inside a loop (N+1) — load related entities once, keyed by id, and join in memory.
- Use `nameof(X)` instead of a hand-typed string literal whenever the string must match an existing type/class/method name.
- Centralize NSwag enum→label lookups in `CinemaLib`'s `cinema.model.ts` — never inline in a feature component.
- A UI element (menu/shell/topbar/dialog/table/seat map) used by 2+ frontend apps is one configurable `CinemaLib` component — never copy-pasted per app.
- New logging needs reuse the single `AuditLog` table with a type/action discriminator — never a new table per feature.
- Prefer a library already in `cinemafe/package.json` over adding a new one or hand-rolling config.

## Known pitfalls

- SignalR JWT auth comes via the `access_token` query string for `/hubs/*` paths, not the `Authorization` header.
- Git: `fetch` + `pull` before creating a new branch; work on `feature/<name>`; merge to `master` when done; ask whether to open a PR after finishing a feature; commit at each compiling milestone, not one giant commit per task.
- Save generated screenshots to `E:\cinema\tasks`; redirect run/serve/QA logs to `E:\cinema\logs` — both are git-ignored, never write them into tracked project folders.

<!-- /bmad:context -->

## Responsive layout (kept outside the managed block)

- Every new or changed UI feature must be responsive (hand-typed queries still in CinemaUser/CinemaStaff are migrated when those files are touched) at phone (<=480), small tablet (<=768) and tablet (<=1024) widths: use the shared CinemaLib mixins `phone` / `tablet-small` / `tablet` from `lib/theme/mixins` (`@use 'lib/theme/mixins' as m;` then `@include m.tablet-small { }`) — never a hand-typed `@media (max-width: …px)`. No page-level horizontal scroll at 360px, touch targets >= 44px on <=768px, list pages use the `.ad-desktop-only` / `.ad-mobile-only` card pattern.
