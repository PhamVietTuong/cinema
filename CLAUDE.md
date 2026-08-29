# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

Monorepo with two siblings:
- `cinemabe/` — .NET 9 Web API + SQL Server backend (Visual Studio solution: `cinemabe/Cinema.sln`).
- `cinemafe/` — Angular 21 workspace containing three sub-projects: `CinemaUser` (port 4200), `CinemaAdmin` (port 4201), and `CinemaLib` (shared library).

There is no top-level build/test runner — backend and frontend are operated independently.

## Backend (`cinemabe/`)

### Architecture — 4 numbered layers

The solution enforces dependency direction by folder prefix. Higher-numbered layers may not depend on lower-numbered ones; reference projects only one tier deeper.

- **`1-Service/`**
  - `Cinema.Service.WebApiHost/` — ASP.NET Core Web API host. Three controllers (`CinemaController`, `IdentityController`, `PaymentController`) grouped via `[ApiExplorerSettings(GroupName=...)]` so NSwag emits **one OpenAPI document per group** (`/swagger/cinema/swagger.json`, `/swagger/identity/swagger.json`, `/swagger/payment/swagger.json`). All controller routes follow `api/[controller]/[action]` and HTTP verb is **POST** even for queries (paged-search DTOs go in the body). Houses `BookingHub` (SignalR, route `/hubs/booking`) and `ExceptionMiddleware`.
  - `Cinema.Service.Clients/` — Holds NSwag-generated clients: `Angular/*-http.service.ts`, `Cinema/CinemaClient.cs`, `Identity/IdentityClient.cs`, `Payment/PaymentClient.cs`. **Do not edit these files — they are regenerated.**
- **`2-Business/`**
  - `Cinema.Business.Contracts/` — one interface per file (`IMovieManager`, `IBookingManager`, `IInvoiceManager`, `ITheaterManager`, `ITokenService`, the per-catalog-entity `I*Manager` interfaces, etc.), flat except `Auth/` (`IAuthManager`, `IFacebookTokenValidator`, `IGoogleTokenValidator`) and `Payments/`.
  - `Cinema.Business.DTO/` — DTOs grouped by domain (`Auth/`, `Booking/`, `Movies/`, `Theaters/`, `Invoices/`, `Requests/`). `PagingSearchDTO` + `DefaultSearchResults<T>` are the standard list contract.
  - `Cinema.Business/` — `Managers/` (one file per `I*Manager`) wired via `AddBusiness()` in `DependencyInjection.cs`. Auth managers (`AuthManager`, `FacebookTokenValidator`, `GoogleTokenValidator`) live under `Managers/Auth/`; everything else (including the per-catalog-entity managers and the generic `CatalogManager<>` base) sits flat directly under `Managers/`. **Seat locking state lives in a static `ConcurrentDictionary` inside `BookingManager`** — it is process-local, not distributed.
  - `Cinema.Business.Tests/` — **Dual-purpose project**: `<OutputType>Exe</OutputType>` with `Program.cs` that seeds `admin@cinema.vn / Admin@123` and `user@cinema.vn / User@123` accounts, **plus** xUnit + Moq + FluentAssertions test classes (`AuthServiceTests`, `BookingServiceTests`, `MovieServiceTests`, `EntityTests`). `dotnet run` seeds; `dotnet test` runs the tests.
- **`3-Data/`**
  - `Cinema.Data.Entities/` — POCO entities + enums. All inherit `BaseEntity` (Guid `Id`, `CreationTime`, `LastUpdatedTime`).
  - `Cinema.Data.Contracts/` — Store interfaces and `IApplicationUnitOfWork` (exposes typed stores + transaction methods).
  - `Cinema.Data/` — `CinemaContext` (EF Core), Fluent API `Configurations/` auto-applied via `ApplyConfigurationsFromAssembly`, `Stores/` (repository implementations atop `GenericStore<T>`), `ApplicationUnitOfWork`, and `JwtTokenService`. Wired via `AddData(IConfiguration)`.
  - **EF Core quirk**: `CinemaContext.OnModelCreating` maps every `DateTime`/`DateTime?` to SQL `datetime` (not the EF default `datetime2`). Preserve this when adding entities.
- **`4-Foundation/`**
  - `Cinema.Foundation/Logging/` — Serilog-backed `ILog` abstraction. Access globally via `LogProvider.Current.Information(...)`. Register with `services.AddFoundationLogging()` in `Program.cs`.

### Auth & SignalR

- JWT bearer auth. Tokens issued by `JwtTokenService`; secret/issuer/audience in `appsettings.json` under `JWT:*`. Role claim drives `[Authorize(Roles="Admin")]`.
- SignalR JWT comes via the `access_token` **query string** for paths starting with `/hubs` (configured in `Program.cs` `OnMessageReceived`).
- `ClaimsPrincipalExtensions.GetUserId()` is the canonical way controllers read the user GUID.

### Database

- SQL Server (default connection string targets `PHAMVIETTUONG\SQLEXPRESS`, db `Cinema`). Override `ConnectionStrings:CinemaDatabase` in `appsettings.Development.json` or env vars.
- **Schema is managed by EF Core migrations** (`Cinema.Data/Migrations`), baselined at `InitialBaseline` — the exact shape the SQL scripts produce. New schema changes go through migrations, not by hand-editing SQL.
- Bootstrapping a database:
  - **Fresh DB**: apply `cinemabe/sql/create_db.sql` (it ends by stamping `__EFMigrationsHistory` with the baseline), then `insert_db.sql` for reference data, then `dotnet ef database update` for anything added since. Seed users by running the `Cinema.Business.Tests` exe (see above).
  - **Existing DB predating migrations**: apply `upgrade_db.sql` once — it ends with the same baseline stamp — then `dotnet ef database update`.
- Migrations are **never applied automatically at startup**; `database update` is an explicit deploy step.
- The pre-migration scripts remain the bootstrap path, so **keep `create_db.sql` and `upgrade_db.sql` in sync** — they must describe the same final schema, and both must end with the baseline stamp.

```powershell
# From cinemabe/. A design-time factory supplies the connection, so no --startup-project is
# needed (the Web API host refuses to start without JWT:Secret, which would break design time).
dotnet ef migrations add <Name> --project Cinema/3-Data/Cinema.Data
dotnet ef migrations script --project Cinema/3-Data/Cinema.Data --idempotent   # review before applying

# Point at a real server for anything that connects:
$env:CINEMA_DESIGNTIME_CONNECTION = "Server=...;Database=Cinema;..."
dotnet ef database update --project Cinema/3-Data/Cinema.Data
```

### Common backend commands

Run from `cinemabe/`:

```powershell
dotnet build Cinema.sln
dotnet run   --project Cinema/1-Service/Cinema.Service.WebApiHost   # http://localhost:5102, https://localhost:7068
dotnet test  Cinema/2-Business/Cinema.Business.Tests                # xUnit tests
dotnet run   --project Cinema/2-Business/Cinema.Business.Tests      # seed admin + user accounts

# Run a single test
dotnet test Cinema/2-Business/Cinema.Business.Tests --filter "FullyQualifiedName~AuthServiceTests"
dotnet test Cinema/2-Business/Cinema.Business.Tests --filter "FullyQualifiedName=Cinema.Business.Tests.AuthServiceTests.Login_ValidCredentials_ReturnsAuthResponse"
```

Swagger UI: `http://localhost:5102/swagger` (per-API dropdown). ReDoc at `/redoc`.

### Regenerating API clients (NSwag)

The C# `*Client.cs` and Angular `*-http.service.ts` files are generated from the running Web API. To regenerate:

```powershell
cd cinemabe/Cinema/1-Service/Cinema.Service.Clients/Generator
./GenerateNswag.ps1
```

This publishes `Cinema.Service.WebApiHost` in Release, runs NSwag for each controller group (Cinema/Identity/Payment), and copies the TypeScript outputs to `cinemafe/projects/CinemaLib/src/lib/services/`. The script requires **NSwag Studio 14** installed at `${env:ProgramFiles(x86)}\Rico Suter\NSwagStudio14\Net80\dotnet-nswag.dll`. It also post-processes the generated TS (wraps namespace in an exported `*ServiceAgent` class, strips unused Http imports) and rewrites `ICollection<...>` to `IList<...>`/`new List<>` in the C# output — don't undo these tweaks manually.

## Frontend (`cinemafe/`)

### Workspace structure

Single Angular CLI workspace with three projects defined in `angular.json`:

| Project       | Type        | Path                      | `prefix` | Dev port |
|---------------|-------------|---------------------------|----------|----------|
| `CinemaLib`   | library     | `projects/CinemaLib`      | `cl`     | —        |
| `CinemaAdmin` | application | `projects/CinemaAdmin`    | `app`    | 4201     |
| `CinemaUser`  | application | `projects/CinemaUser`     | `app`    | 4200 (default `ng serve`) |
| `cinemafe`    | application | root `src/`               | `app`    | (legacy/default) |

Both apps consume `CinemaLib` as a regular import: `import { ... } from 'CinemaLib'`. The library exports through `projects/CinemaLib/src/public-api.ts`: tokens (`API_BASE_URL`, `HUB_BASE_URL`), `SharedModule` (Angular Material barrel), models, NSwag-generated services, guards (`authGuard`, `adminGuard`), HTTP interceptors (`authInterceptor`, `errorInterceptor`), and NgRx feature stores (auth, movies).

### Integration with generated services

Apps bootstrap NSwag service agents through DI:

```ts
{ provide: CinemaServiceAgent.CINEMA_BASE_URL, useValue: environment.apiUrl },
CinemaServiceAgent.HttpService,
```

`environment.apiUrl` defaults to `http://localhost:5102`, `hubUrl` to `http://localhost:5102/hubs`. Override in `environment.prod.ts`.

### State management

NgRx Store + Effects. Reducers live in the library (`store/auth`, `store/movies`) and are mounted in each app's `app.module.ts` via `StoreModule.forRoot({ auth: authReducer, movies: moviesReducer })`. JWT storage uses `localStorage` (read in `authInterceptor`).

### Common frontend commands

Run from `cinemafe/`:

```powershell
npm install
ng serve CinemaUser                  # http://localhost:4200
ng serve CinemaAdmin                 # http://localhost:4201
ng build  CinemaUser --configuration production
ng build  CinemaLib                  # rebuild the library after changes
ng test   CinemaUser                 # vitest via @angular/build:unit-test
ng test   CinemaLib
```

When editing `CinemaLib`, rebuild it (or `ng build CinemaLib --watch`) before the consuming apps will see changes.

## Working conventions to preserve

- **Git feature-branch workflow**: Before starting a new feature, `git fetch` and `git pull` first so you branch from the latest `master`. Do the work on a dedicated feature branch (`git checkout -b feature/<name>`), and once the feature is finished, merge it back into `master`.
- **Always use block statements (braces), never single-statement bodies**: every control-flow body — `if` / `else if` / `else`, `for`, `foreach`, `while`, `do`, `using`, `lock` — must be wrapped in `{ }`, even when it's a single line. Never write `if (x) return;` or a braceless one-liner; write `if (x)\n{\n    return;\n}`. (Expression-bodied members and lambdas using `=>` are fine — this rule is about statement bodies.)
- **Don't hand-edit NSwag-generated files** (`Cinema.Service.Clients/**`, `projects/CinemaLib/src/lib/services/*-http.service.ts`). Regenerate via the PowerShell script.
- **All list/search endpoints take `PagingSearchDTO` in the body via POST** and return `DefaultSearchResults<T>`. Filters use `search.Filters.GetGuid("key")` etc. (extension methods in `Cinema.Business/Extensions/FilterExtensions.cs`).
- **Controllers follow a try/catch + `LogProvider.Current` pattern** (`{GetType().Name}.{Method} being awakened…` on entry, `Fatal` on exception, then `StatusCode(500, e.Message)`). New endpoints should match.
- **Layer dependency**: never reference a higher-numbered project from a lower-numbered one. Keep DTOs in `Cinema.Business.DTO`, entities in `Cinema.Data.Entities`.
- **Datetime mapping**: new entities pick up the `datetime` column type automatically via `CinemaContext.OnModelCreating` — don't override unless intentional.

## Behavioral Guidelines

> These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria allow independent looping. Weak criteria ("make it work") require constant clarification.


### 5. Query Efficiency

**Fetch the narrowest set of columns and rows that answers the question. Never issue one query per row.**

This codebase reaches the database through `IGenericStore<Entity>` exposed on `IApplicationUnitOfWork`. That interface makes it easy to load far more than you need, so these two rules apply to every read path you touch.

#### 5.1 Select only the columns you use

`AllAsync()`, `FindAllAsync(...)` and `FindAsync(...)` issue `SELECT *` and materialise **fully tracked** entities. Every column travels over the wire, is allocated on the heap, and is kept alive by the change tracker until the request ends. On a wide table or a large result set this is the single biggest source of avoidable memory pressure in the API.

Before writing a read, ask: *which properties does the caller actually consume?* If the answer is a subset of the entity, project to that subset.

```csharp
// Bad — loads Id, Type, Ordre, IsActif, SensDirection, HommePremier and tracks all 7 entities
// when the caller only needs Id and Type.
var criteres = await _uow.AdmissionAlgoCritereStore.AllAsync();
var pairs = criteres.Select(x => new { x.Id, x.Type });

// Good — the database returns two columns; nothing is tracked.
var pairs = await _uow.AdmissionAlgoCritereStore
    .GetQuery()
    .AsNoTracking()
    .Select(x => new { x.Id, x.Type })
    .ToListAsync();
```

Use `FindAllSelectAsync<TClass>` / `FindSelectAsync<TClass>` when the store's projection overloads fit, or drop to `GetQuery()` and compose the LINQ yourself when they don't.

Related rules:
- **Filter in SQL, not in memory.** `FindAllAsync(x => ...)` and `GetQuery().Where(...)` push the predicate to the database. Loading everything and then calling `.Where(...)`/`.FirstOrDefault(...)` on the resulting `IEnumerable` does not.
- **Count without materialising.** Use `CountAsync(...)`, never `(await AllAsync()).Count()`.
- **Check existence without materialising.** Use `ExistsAsync(...)`, never `FindAsync(...) != null`.
- **Page at the database.** Use `AllPageAsync` / `FindAllPageAsync` rather than fetching everything and slicing.
- **`AsNoTracking()` on every read-only query.** Change tracking exists to support writes; on a read it only costs memory and snapshot work.
- **Exception — writes.** When you intend to mutate and persist an entity, you *must* load the full tracked entity. Do not project a save path just to look efficient; EF needs the tracked instance. `AdmissionAlgoCritereManager.SaveOrdreAsync` is the reference example.

#### 5.2 Guard against N+1 queries

An N+1 is one query to fetch a list, then one more query per item in that list. It usually reads fine and passes review, because the extra queries are hidden behind a property access or a helper call inside a loop. It is the most common performance defect in this codebase's shape.

The three ways it appears here:

**a) A store call inside a loop.**

```csharp
// Bad — 1 query for the inscriptions, then one per inscription. 500 rows = 501 round-trips.
var inscriptions = await _uow.AdmissionAlgoInscriptionStore.FindAllAsync(x => x.SessionAdmissionAlgoId == id);
foreach (var inscription in inscriptions)
{
    var etudiant = await _uow.EtudiantStore.FindAsync(inscription.EtudiantId);
    result.Add(Map(inscription, etudiant));
}

// Good — two queries total, joined in memory by key.
var inscriptions = await _uow.AdmissionAlgoInscriptionStore
    .GetQuery().AsNoTracking()
    .Where(x => x.SessionAdmissionAlgoId == id)
    .ToListAsync();

var etudiantIds = inscriptions.Select(x => x.EtudiantId).Distinct().ToList();
var etudiants = (await _uow.EtudiantStore
        .GetQuery().AsNoTracking()
        .Where(x => etudiantIds.Contains(x.Id))
        .Select(x => new { x.Id, x.Nom, x.Prenom })
        .ToListAsync())
    .ToDictionary(x => x.Id);

var result = inscriptions.Select(x => Map(x, etudiants[x.EtudiantId])).ToList();
```

**b) Lazy-loading a navigation property that was never included.** A navigation not named in the include path is `null`, not loaded on demand — so this usually surfaces as a `NullReferenceException` rather than as slowness. Name every navigation you dereference: `GetQuery("Etudiant,Programme")`, `FindAllIncludeAsync("Etudiant.Adresse", ...)`.

**c) An `await` inside a `Select`/`foreach` that projects to a DTO.** Mapping code is where this hides best. If a mapper needs related data, pass it in from a pre-loaded dictionary; don't let the mapper query.

Before you call a read path done, trace it and answer explicitly:
- How many database round-trips does this make for 1 row? For 1 000 rows? If the second answer scales with the row count, fix it.
- Does every `Include` I asked for get used? An unused include is the same waste as an unused column.
- Am I including a collection navigation alongside other includes? That produces a cartesian result set in EF Core 3.1 — split it into a second query keyed by id instead.

State these answers in your self-review. "It works" is not sufficient for a read path.

## QA / regression test harness (`QA-tests/`)

Two complementary systems live under `QA-tests/` (see `QA-tests/flow-tests/README.md` and `QA-tests/playwright/README.md`):

1. **Flow tests** — `/test-flow <flow-id>` runs static invariant checks + `dotnet build`/`dotnet test` + `ng build` + a manual E2E playbook for a business flow, writing `QA-tests/FLOW-TEST-RESULTS.md` (rolling log) and `QA-tests/reports/<flow>-<date>/REPORT.html`. Real defects → `QA-tests/auto-bugs/BUG-<NNNN>-*.md` (+ optional `patch-suggestions/`). Flows: `booking-seat-lock`, `auth-login`, `movie-admin`.
2. **Playwright E2E** — `QA-tests/playwright/` (CinemaUser :4202, CinemaAdmin :4201), `npm test` → HTML report via `npm run report`.

A **PreToolUse hook** (`.claude/hooks/check-business-invariants.js`, wired in `.claude/settings.json`) guards edits in real time against the invariants in `module-paths-mapping.json` + `business-flows.json` (P0 → ask, P1/P2 → soft warning).

### Auto-trigger flow tests from natural language

When the user signals they finished a work sequence on code (e.g. "I'm done, test", "check the booking flow", "run the regression", "task done"), you SHOULD:

1. Check there is actually changed code (`git diff --name-only` on `cinemabe`/`cinemafe` + this session's edits).
2. Cross the changed files against each `QA-tests/flow-tests/*.yaml` `trigger_paths`.
3. **Propose** the matching flow(s) with a short question — never launch silently. The user confirms.
4. On confirmation → spawn a background agent (same mechanics as `/test-flow`).

- **Strong triggers** (propose immediately): "I'm done, test", "task done", "run the tests", "check the flow", "test the booking/auth/admin flow".
- **Soft triggers** (ask first): "that's done", "ok it's ready", "it compiles".
- **Non-triggers** (don't propose): "done reading", "ok thanks", "I finished the docs".
- Cooldown: don't re-propose the same flow within a few minutes or after a "no" this session; don't interrupt mid-task.
