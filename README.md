# Cinema

Full-stack cinema booking system.

- **`cinemabe/`** — .NET 9 Web API + SQL Server backend (clean architecture, JWT auth, SignalR).
- **`cinemafe/`** — Angular 21 workspace: `CinemaUser` (:4200), `CinemaAdmin` (:4201), shared `CinemaLib`.
- **`QA-tests/`** — regression harness: flow tests + Playwright E2E.

> Detailed architecture, conventions, and layer rules live in [`CLAUDE.md`](CLAUDE.md).

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.0.x | Pinned in `cinemabe/global.json` |
| Node.js | 22.12.0 | See `.nvmrc` — run `nvm use`. The system Node (v14) is too old. |
| SQL Server | 2019+ / Express | Or run via Docker |

## Backend (`cinemabe/`)

Secrets are **not** committed. Provide them once via user-secrets (dev) or environment variables (prod):

```powershell
cd cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost
$secret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "JWT:Secret" $secret
dotnet user-secrets set "ConnectionStrings:CinemaDatabase" "Server=.\SQLEXPRESS;Database=Cinema;User ID=<login>;Password=<pwd>;MultipleActiveResultSets=true;Encrypt=True;TrustServerCertificate=True"
```

Create the database and run:

```powershell
# From cinemabe/ — apply the schema + reference data
#   sql/create_db.sql  → fresh schema
#   sql/insert_db.sql  → reference data
dotnet build Cinema.sln
dotnet run   --project Cinema/2-Business/Cinema.Business.Tests   # seed admin@cinema.vn / user@cinema.vn
dotnet run   --project Cinema/1-Service/Cinema.Service.WebApiHost # http://localhost:5102
dotnet test  Cinema/2-Business/Cinema.Business.Tests             # xUnit
```

- Swagger UI: `http://localhost:5102/swagger` · ReDoc: `/redoc`
- Health check: `http://localhost:5102/health`

## Frontend (`cinemafe/`)

```powershell
nvm use            # 22.12.0
npm install
ng serve CinemaUser    # http://localhost:4200
ng serve CinemaAdmin   # http://localhost:4201
ng build CinemaLib     # rebuild the shared library after editing it
```

## QA

```powershell
# Flow regression (static invariants + build/test + manual playbook)
#   via Claude Code: /test-flow booking-seat-lock | auth-login | movie-admin

# Playwright E2E
cd QA-tests/playwright && npm test && npm run report
```
