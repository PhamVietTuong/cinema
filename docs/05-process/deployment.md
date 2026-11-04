# Deployment

> Purpose: Build, environment, and pipeline facts that are actually verifiable from config in the repo.
> Sources: E:\cinema\docker-compose.yml, E:\cinema\.github\workflows\ci.yml, E:\cinema\CLAUDE.md, E:\cinema\README.md
> Last reviewed: 2026-11-01

## CI/CD pipeline: exists, build/test only — no deploy step

`.github/workflows/ci.yml` defines a GitHub Actions workflow (`name: CI`) triggered on push/PR to
`master`, with three jobs:

| Job | Runs on | What it does |
|---|---|---|
| `backend` | ubuntu-latest | `dotnet restore`/`build -c Release` on `Cinema.sln`, then `dotnet test` on `Cinema.Business.Tests` with coverage, uploads `backend-coverage` artifact. |
| `frontend` | ubuntu-latest | `npm ci --legacy-peer-deps`, `npm run lint`, builds `CinemaLib`, runs `ng test` for `CinemaLib`/`CinemaUser`/`CinemaAdmin`, production-builds `CinemaUser`/`CinemaAdmin`, uploads `frontend-coverage` artifact. |
| `e2e` | ubuntu-latest (30 min timeout) | Spins up a `mssql/server:2022-latest` service container, creates and seeds the `Cinema` DB from `cinemabe/sql/create_db.sql` + `insert_db.sql`, seeds admin/customer accounts via `dotnet run` on `Cinema.Business.Tests`, starts the Web API and `ng serve`s both `CinemaUser`/`CinemaAdmin`, then runs the Playwright suite in `QA-tests/playwright`, uploading the HTML report. |

**There is no deployment/release job** — no `docker build`/`docker push`, no deploy-to-environment
step, no versioning/tagging step, and no separate workflow file beyond `ci.yml` under
`.github/workflows/`. This CI pipeline verifies the build; it does not ship it anywhere.

`npm run lint` fails the `frontend` job only on ESLint `error`-level rules — see
`docs/05-process/coding-conventions.md` for which rules are `warn` vs `error` and why.

## Local/dev stack: `docker-compose.yml`

```yaml
services:
  db:   # mcr.microsoft.com/mssql/server:2022-latest, SA password hardcoded for dev, port 1433
  api:  # build: ./cinemabe, depends_on: db, port 5102:8080
```

Per the comment block at the top of `docker-compose.yml`, the intended first-time setup is:

1. `docker compose up -d db`
2. Apply `cinemabe/sql/create_db.sql` then `insert_db.sql` against `localhost:1433` (schema is
   **not** managed by EF migrations in this repo — see `CLAUDE.md`).
3. `docker compose up -d api`

The frontend apps are **not** part of `docker-compose.yml` — they're run separately with `ng serve`.

Notable values baked into `docker-compose.yml` (dev-only, explicitly commented as such):
- `JWT__Secret`: `"compose-dev-signing-key-not-for-production-change-me-1234567890"` — comment says
  "override for any real deployment."
- `AllowedOrigins__0`/`__1`: `http://localhost:4200` and `http://localhost:4201`.
  **Note**: `4200` here does not match the port CLAUDE.md and `README.md`'s QA section assign to
  `CinemaUser` elsewhere (`4202`) — see `docs/00-overview/open-questions.md`.
- No `cinemabe/Dockerfile` content was reviewed as part of this doc (only its existence via
  `build: ./cinemabe` in compose) — <!-- TODO: confirm with team what the Dockerfile's publish/runtime target is (e.g. which .NET base image, Release vs Debug). -->

## Manual build/run commands (from CLAUDE.md, no CI/CD wrapper)

Backend (`cinemabe/`):

```powershell
dotnet build Cinema.sln
dotnet run   --project Cinema/1-Service/Cinema.Service.WebApiHost   # http://localhost:5102, https://localhost:7068
dotnet test  Cinema/2-Business/Cinema.Business.Tests
dotnet run   --project Cinema/2-Business/Cinema.Business.Tests      # seed admin + user accounts
```

Frontend (`cinemafe/`):

```powershell
npm install
ng serve CinemaUser                  # http://localhost:4202
ng serve CinemaAdmin                 # http://localhost:4201
ng serve CinemaStaff                 # http://localhost:4203
ng build  CinemaUser --configuration production
ng build  CinemaLib                  # rebuild the library after changes
```

## Environments

Only two environment profiles are evidenced in the repo:

- **Local dev** — `appsettings.Development.json` (git-ignored, holds real connection
  string/JWT secret per developer machine) or `docker-compose.yml`'s hardcoded dev values.
- **CI** — `.github/workflows/ci.yml`'s `env:` block (`Cinema_CI_Pwd_123!`,
  `ci-only-signing-key-not-used-anywhere-else-0123456789`), used only for the duration of the `e2e`
  job.

No staging or production environment configuration (`appsettings.Production.json` contents, a
production connection string, a production JWT secret source, or a hosting target such as Azure/AWS)
was found in the repo — secrets for any such environment are expected to be supplied externally
(user-secrets or env vars, per `README.md`'s "Prerequisites" section), and there is no committed
evidence of where/how the app is actually deployed outside of a developer's machine or CI.

<!-- TODO: confirm with team whether a production deployment target exists outside this repo (e.g. a separate ops repo, manual VM deploy, or cloud service) — nothing under E:\cinema indicates one. -->
