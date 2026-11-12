# Cinema

Full-stack cinema booking system.

- **`cinemabe/`** — .NET 9 Web API + SQL Server backend (clean architecture, JWT auth, SignalR).
- **`cinemafe/`** — Angular 21 workspace: `CinemaUser` (:4202), `CinemaAdmin` (:4201), `CinemaStaff` (:4203), shared `CinemaLib`.
- **`e2e/`** — Playwright end-to-end suite against the real stack (API + DB + the three apps); run by the CI `e2e` job.

> Detailed architecture, conventions, and layer rules live in [`AGENTS.md`](AGENTS.md) (imported by `CLAUDE.md`); the knowledge base is in [`docs/`](docs/README.md).

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.0.x | Pinned in `cinemabe/global.json` |
| Node.js | 22.12.0 | See `.nvmrc` — run `nvm use`. The system Node (v14) is too old. |
| SQL Server | 2019+ / Express | Or run via Docker |

## Backend (`cinemabe/`)

**One command:** `docker compose up -d` starts SQL Server, initialises the schema (idempotent `db-init`) and the API on http://localhost:5102. The frontend apps still run with `ng serve`.

Or run it manually:

Secrets are **not** committed. Provide them once via user-secrets (dev) or environment variables (prod):

```powershell
cd cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost
$secret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "JWT:Secret" $secret
dotnet user-secrets set "ConnectionStrings:CinemaDatabase" "Server=.\SQLEXPRESS;Database=Cinema;User ID=<login>;Password=<pwd>;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True"
```

Create the database and run:

```powershell
# From cinemabe/ — apply the schema + reference data to an EMPTY database, in this order
#   sql/create_db.sql  → baseline schema (DROPS and recreates tables)
#   sql/upgrade_db.sql → idempotent; brings the baseline to the current schema
#   sql/insert_db.sql  → reference data
# This is what CI and `docker compose up` do. Run each with:
#   sqlcmd -I -b -f 65001 -d Cinema -i sql/<script>.sql
# (-f 65001 is required: insert_db.sql holds Vietnamese N'...' literals that are mangled otherwise.)
dotnet build Cinema.sln
dotnet run   --project Cinema/2-Business/Cinema.Business.Tests   # seed admin@cinema.vn / user@cinema.vn
dotnet run   --project Cinema/1-Service/Cinema.Service.WebApiHost # http://localhost:5102
dotnet test  Cinema/2-Business/Cinema.Business.Tests             # xUnit (business layer)
dotnet test  Cinema/1-Service/Cinema.Service.WebApiHost.Tests    # controller architecture, authorization, IPN contract
```

Never re-run `create_db.sql` on a database that holds data: it drops and recreates the tables. To bring an existing database up to date, run only `upgrade_db.sql` (idempotent). `docker compose up` does this for you.

`insert_db.sql` builds its showtime grid relative to the day it was authored, so a fresh database may have no upcoming showtime. `e2e/sql/seed-future-showtime.sql` moves past showtimes to tomorrow; run it on throw-away databases only, because it rewrites showtime history.

- Swagger UI: `http://localhost:5102/swagger` · ReDoc: `/redoc`
- Health check: `http://localhost:5102/health`

## Frontend (`cinemafe/`)

```powershell
nvm use            # 22.12.0
npm install
ng serve CinemaUser    # http://localhost:4202
ng serve CinemaAdmin   # http://localhost:4201
ng serve CinemaStaff   # http://localhost:4203
ng build CinemaLib     # rebuild the shared library after editing it
```

## QA

Playwright E2E needs the API and the three apps running (see `e2e/README.md` for accounts and prerequisites). Run it from the repository root, with the same Node 22 as the frontend (`nvm use`):

```powershell
nvm use            # 22.12.0
cd e2e
npm install
npx playwright install chromium
npm test
# HTML report (e2e/package.json has no report script; the default reporter is the list reporter):
npx playwright test --reporter=list,html
npx playwright show-report
```

Frontend unit tests: `ng test <project>` from `cinemafe/`. Backend unit tests are listed under Backend above.
