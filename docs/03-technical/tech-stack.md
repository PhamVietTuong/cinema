# Tech Stack

> Purpose: Languages, frameworks, and key libraries with versions.
> Sources: cinemabe/**/*.csproj, cinemafe/package.json
> Last reviewed: 2026-11-12

## Backend (`cinemabe/`)

| Area | Library | Version |
|---|---|---|
| Language/runtime | C# / .NET | 9 |
| Web | ASP.NET Core | 9 |
| Auth | Microsoft.AspNetCore.Authentication.JwtBearer | 9.0.0 |
| Real-time | SignalR client package | 1.2.0 |
| API docs | NSwag.AspNetCore | 14.2.0 |
| ORM | Microsoft.EntityFrameworkCore.SqlServer / .Design | 9.0.0 |
| JWT | System.IdentityModel.Tokens.Jwt | 8.3.3 |
| Logging | Serilog / Serilog.AspNetCore | 4.2.0 / 9.0.0 |
| Logging sinks | Serilog.Sinks.Console / .Sinks.File / .Enrichers.Thread | 6.0.0 / 6.0.0 / 4.0.0 |
| Google OAuth | Google.Apis.Auth | 1.75.0 |
| QR codes | QRCoder | 1.6.0 |
| Caching | Microsoft.Extensions.Caching.Memory | 9.0.0 |
| Testing | xUnit | 2.9.2 |
| Testing | Moq | 4.20.72 |
| Testing | FluentAssertions | 7.2.0 |
| Testing | coverlet.collector | 6.0.2 |

No central `Directory.Packages.props` — versions are pinned per-`.csproj`.

## Frontend (`cinemafe/`)

| Area | Library | Version |
|---|---|---|
| Framework | Angular (core/common/forms/router/animations/cdk/material/platform-browser) | 21.2.x |
| State | NgRx (`@ngrx/store`, `@ngrx/effects`, `@ngrx/store-devtools`) | 21.1.0 |
| Real-time | `@microsoft/signalr` | 10.0.0 |
| i18n | `@ngx-translate/core` + `@ngx-translate/http-loader` | 18.0.0 |
| Tables | `@swimlane/ngx-datatable` | 25.1.0 |
| Icons | `@ngneat/svg-icon` | 8.0.0 |
| QR codes | `qrcode` + `@types/qrcode` | 1.5.4 |
| Reactive | rxjs | ~7.8.0 |
| Language | TypeScript | ~5.9.2 |
| Testing | Vitest (`@angular/build:unit-test`) | 4.x |
| Lint/format | ESLint 9 + angular-eslint 21, Prettier | 3.8 |
| Library build | ng-packagr | 21 |

Requires Node >= 22.12.0 (see `.nvmrc`; the system default Node is v14 — use `nvm use`).

## Cross-cutting

| Area | Tool |
|---|---|
| Database | SQL Server 2019+ / Express (dev default `.\SQLEXPRESS`, db `Cinema`) |
| Schema management | Hand-maintained SQL scripts (`cinemabe/sql/create_db.sql`, `upgrade_db.sql`); no new EF Core migrations since the `InitialBaseline` (last one dropped in `7065d9c`) — see [`ADR-0003`](../04-decisions/ADR-0003-hand-written-sql-instead-of-ef-migrations.md) |
| CI | GitHub Actions (`.github/workflows/ci.yml`) — backend build+test, frontend lint+test+build, full e2e |
| Dev payment testing | ngrok (VNPay sandbox IPN tunnel) |
