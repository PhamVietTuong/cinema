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

### Optional configuration

Everything below is optional; without it the app falls back to dev behaviour. Set via user-secrets / environment variables (never commit). Full list: [`docs/03-technical/configuration.md`](docs/03-technical/configuration.md).

| Key | Purpose |
|-----|---------|
| `AllowedOrigins` | CORS allow-list (default `localhost:4200`–`4203`) |
| `Smtp:Host` / `Port` / `User` / `Password` / `From` | Real email; empty = emails are only written to the log |
| `Sms:Twilio:AccountSid` / `AuthToken` / `FromNumber` | Real SMS; empty = dev-log sender |
| `Payments:Provider` | Default gateway (`Sandbox` in dev) |
| `Payments:VnPay:TmnCode` / `HashSecret` / `ReturnUrl` | VNPay sandbox (see ngrok below) |

### Test accounts

After the seed step: `admin@cinema.vn` and `user@cinema.vn` (dev-only passwords `Admin@123` / `User@123`, defined in `Cinema.Business.Tests/Program.cs`; if login returns 401 the DB was seeded differently).

### Testing VNPay payments locally (ngrok)

VNPay is **callback-authoritative**: an invoice only becomes *Paid* when VNPay's servers call the signed IPN endpoint (`PaymentController.PaymentCallback?provider=VNPay`), never from the browser redirect. VNPay's servers cannot reach `localhost`, so a public tunnel is required. The default `Sandbox` gateway needs no tunnel.

1. Install ngrok and add your authtoken once: `winget install ngrok.ngrok`, then `ngrok config add-authtoken <token>`.
2. Start the API (http://localhost:5102), then in another terminal: `ngrok http 5102`.
3. Copy the printed `https://….ngrok-free.dev` URL.
4. In the VNPay sandbox merchant portal (`sandbox.vnpayment.vn`), set the **IPN URL** for your terminal (`Payments:VnPay:TmnCode`) to `<ngrok-url>/api/Payment/PaymentCallback?provider=VNPay`.
5. The free plan issues a **new URL on every restart** — repeat step 4 each time.
6. Make sure `Payments:VnPay:TmnCode`, `HashSecret` and `ReturnUrl` (default `http://localhost:4202/booking/payment-return`) are set, and choose `VNPay` as the provider at checkout.
7. Sandbox test card: bank **NCB**, number `9704198526191432198`, name `NGUYEN VAN A`, issue date `07/15`, OTP `123456`.

More detail: [`docs/03-technical/integrations.md`](docs/03-technical/integrations.md) §5.

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

### Performance (k6)

Load/stress/spike/soak tests of the public catalog endpoints live in `performance/` — install with `winget install k6`, start the API, then e.g. `k6 run performance/k6/load.js`. See [`performance/README.md`](performance/README.md).

## CI

GitHub Actions (`.github/workflows/ci.yml`) runs backend build + tests, frontend lint + test + build, and the full e2e suite. Run the same jobs locally and get them green before pushing.

## Troubleshooting

- **`nvm use` asks for admin (UAC) on Windows** — instead put Node 22 on PATH: `$env:PATH = "$env:APPDATA\nvm\v22.12.0;$env:PATH"` then check `node -v`.
- **Angular app shows stale shared UI** — run `ng build CinemaLib` first; apps consume the built library.
- **Vietnamese text is garbled after seeding** — run `sqlcmd` with `-f 65001`.
- **No upcoming showtime on a fresh DB** — see `e2e/sql/seed-future-showtime.sql` above (throw-away DBs only).
- **NSwag client regeneration fails** — stop the Web API first and run `Generator/GenerateNswag.ps1` from its own directory (`cinemabe/Cinema/1-Service/Cinema.Service.Clients/Generator`). Never hand-edit generated clients.
- **Seat locks are process-local** — they live in memory in the API process, so a restart clears them and running multiple API instances is not supported yet.
