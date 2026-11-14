# Deployment

> Purpose: Build, environment, and pipeline facts that are actually verifiable from config in the repo.
> Sources: E:\cinema\docker-compose.yml, E:\cinema\.github\workflows\ci.yml, E:\cinema\CLAUDE.md, E:\cinema\README.md
> Last reviewed: 2026-11-12

## CI/CD pipeline: quality gates, no deploy step

`.github/workflows/ci.yml` defines a GitHub Actions workflow (`name: CI`) triggered on push/PR to
`master`, with these jobs:

| Job | What it does |
|---|---|
| `backend` | `dotnet build -c Release` on `Cinema.sln` (analyzers on via `cinemabe/Directory.Build.props`), the **analyzer warning ratchet**, the NuGet vulnerability audit, `dotnet test` on `Cinema.Business.Tests` (with coverage) and `Cinema.Service.WebApiHost.Tests`, the 60% `Cinema.Business` coverage ratchet, `backend-coverage` artifact. |
| `secrets` | gitleaks over the **full git history** with `.gitleaks.toml` (default rules + an allow-list of documented false positives). `continue-on-error` until a clean run is seen on master, then flip to blocking. |
| `docker` | `docker build ./cinemabe` (build only, no push) so a broken Dockerfile fails the PR. |
| `frontend` | `npm ci --legacy-peer-deps`, `npm run lint:ci`, `npm audit --audit-level=high`, builds `CinemaLib`, `ng test` for all four projects, production builds of the three apps, `frontend-coverage` artifact. |
| `db-upgrade` (15 min) | `mssql/server:2022-latest` service; `cinemabe/sql/tests/run-upgrade-harness.sh` builds the OLD `create_db.sql` from git history (commit `3656d73`, needs `fetch-depth: 0`), seeds gift-card / child-ticket / consent rows, runs `upgrade_db.sql` twice and asserts the result, so the destructive guarded blocks are tested. |
| `e2e` (30 min) | `mssql/server:2022-latest` service, create -> upgrade -> insert SQL, seed accounts, start API + three apps, Playwright suite, HTML report. |

### Ratchets (numbers may only go down)

| Gate | Baseline (2026-11-12) | Allowed growth | Where |
|---|---|---|---|
| ESLint warnings | 1,437 | none: `--max-warnings 1437` fails on any new warning | `cinemafe/package.json` `lint:ci`; lower the number whenever the count drops. Errors always fail. Top rules: 719 prefer-control-flow, 437 prefer-inject, 159 no-explicit-any, 60 prefer-standalone (partial: 1,375 of 1,437). |
| .NET analyzer warnings | 641 (compiler + analyzer codes CA/CS/IDE/SYSLIB, de-duplicated) | baseline + 5% (limit 673) | `.github/scripts/analyzer-baseline.txt`, checked by `.github/scripts/check-warnings.py`, which also fails when it parses no warnings and prints a notice when the count is below the baseline (lower the file). Top rules: CS8618 467, CA1725 37, CA1716 23, CA1305 21 (partial). CA1707 (593 xUnit `Method_Scenario_Expected` names) is switched off for `*Tests` projects in `cinemabe/.editorconfig`. |
| `Cinema.Business` line coverage | 60% | none | `check-coverage.py` |

### Informational gates (do not fail the build yet)

| Gate | Current state | Flip to blocking when |
|---|---|---|
| gitleaks (`secrets` job, pinned `zricethezav/gitleaks:v8.30.1`, full history, `.gitleaks.toml`) | clean locally (412 commits); one false positive allow-listed (prose in `business/catalog.md`) | one clean run on master: remove `continue-on-error` on the job |
| NuGet audit (`dotnet list package --vulnerable --include-transitive`) | open: `Microsoft.AspNetCore.Http.Connections` 1.2.0 (High) and `Microsoft.AspNetCore.WebUtilities` 2.3.0 (Moderate), both transitive in the WebApiHost; the High finding is tracked in `_bmad-output/implementation-artifacts/deferred-work.md` | the report is clean: remove `continue-on-error` on the step |
| npm audit (`--audit-level=high`) | open: 67 advisories (25 high, 3 critical), mostly build/tooling transitives | exits 0: remove `continue-on-error` on the step |

### What is deliberately NOT done

- **No deploy or release job**: no image push, no deploy-to-environment step, no versioning/tagging. There is no deploy target in the repo.
- **No monitoring or alerting**: nothing watches the running API (health endpoint `/health` exists, nothing polls it), no error tracking, no log shipping, no alerts on failed payments or IPN errors.
- **No mass fix of the lint or analyzer backlog** - only ratcheted so it cannot grow.
- GitHub repository settings (branch protection) are not changed by the repo; see the recommendation in `workflow.md`.

## Local/dev stack: `docker-compose.yml`

```yaml
services:
  db:       # mssql/server:2022-latest, SA password hardcoded for dev, port 1433, healthcheck (sqlcmd SELECT 1)
  db-init:  # same image; waits for db healthy, creates DB if missing, runs create -> upgrade -> insert ONLY if the schema is absent
  api:      # build: ./cinemabe, depends_on db-init completing, port 5102:8080
```

`docker compose up -d` is the whole first-time setup. `db-init` records completion in a `dbo.__DbInit` marker table. Marker present: only the idempotent `upgrade_db.sql` runs.
No marker and no schema: create -> upgrade -> insert, then the marker is written last. No marker but the schema exists (legacy
volume or a failed earlier init): `create_db.sql` (which drops tables) is never run; only `upgrade_db.sql`, then the marker.
Every probe fails the container if `sqlcmd` fails (e.g. wrong password). A half-built database is reset with
`docker compose down -v`. The SA password is `${CINEMA_DB_PASSWORD}` (default: the dev value), used by `db`, `db-init` and `api`.
`Payments__EnableSandbox: "true"` is set for the local demo only (off by default outside Development).

The frontend apps are **not** part of `docker-compose.yml` — they're run separately with `ng serve`.

Notable values baked into `docker-compose.yml` (dev-only, explicitly commented as such):
- `JWT__Secret`: `"compose-dev-signing-key-not-for-production-change-me-1234567890"` — comment says
  "override for any real deployment."
- `AllowedOrigins__0`/`__1`/`__2`: `http://localhost:4202` (CinemaUser), `4201` (CinemaAdmin), `4203` (CinemaStaff).
- `cinemabe/Dockerfile`: multi-stage, `dotnet/sdk:9.0` publish (Release) -> `dotnet/aspnet:9.0` runtime, port 8080, `TZ=Asia/Ho_Chi_Minh`; built by the CI `docker` job (no push). Logs go to `/app/logs/` (compose mounts the named volume `cinema-logs` there so they persist).

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

Owner input needed: whether a production deployment target exists outside this repository (a separate ops repo, a manual VM deploy or a cloud service). Nothing under `E:\cinema` indicates one.
