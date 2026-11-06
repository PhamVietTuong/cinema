# Codebase Map

> Purpose: Folder/module → responsibility, plus a "where to change X" guide.
> Sources: cinemabe/Cinema/**, cinemafe/projects/**, CLAUDE.md
> Last reviewed: 2026-11-04

## Backend (`cinemabe/`)

| Folder | Responsibility |
|---|---|
| `Cinema/1-Service/Cinema.Service.WebApiHost/` | ASP.NET Core 9 Web API host: 3 controllers (Cinema, Identity, Payment), SignalR hubs (`BookingHub` at `/hubs/booking`, `StaffHub` at `/hubs/staff`), `ExceptionMiddleware`, background services (`PendingBookingReaper`, `ShowtimeReminderService`), health checks (`/health`, `/health/ready`), static routes (`/uploads`, `/seed-assets`), composition root (`Program.cs`). |
| `Cinema/1-Service/Cinema.Service.Clients/` | NSwag-generated HTTP clients (C# `*Client.cs` + Angular `*-http.service.ts`). **Never hand-edited** — regenerate via `Generator/GenerateNswag.ps1`. |
| `Cinema/1-Service/Cinema.Service.WebApiHost.Tests/` | xUnit tests against the host, including `ControllerArchitectureTests` (enforces the exactly-3-controllers rule). |
| `Cinema/2-Business/Cinema.Business.Contracts/` | One interface per manager/service/gateway (`I*Manager`, `ITokenService`, payment/notification gateway interfaces). |
| `Cinema/2-Business/Cinema.Business.DTO/` | Request/response DTOs grouped by domain folder (`Auth/`, `Booking/`, `Movies/`, `Theaters/`, `Invoices/`, `Requests/`). |
| `Cinema/2-Business/Cinema.Business/Managers/` | Business-logic managers, one per `I*Manager` interface. |
| `Cinema/2-Business/Cinema.Business/Managers/Auth/` | `AuthManager`, Google/Facebook token validators. |
| `Cinema/2-Business/Cinema.Business/Payments/` | `IPaymentGateway` implementations (Sandbox/VNPay/MoMo/Stripe) + gateway resolver. |
| `Cinema/2-Business/Cinema.Business/Notifications/` | Email/SMS/seat/staff notification implementations (dev-log, no-op, SMTP, Twilio, SignalR). |
| `Cinema/2-Business/Cinema.Business/ExternalRatings/` | OMDb ratings provider. |
| `Cinema/2-Business/Cinema.Business.Tests/` | Dual-purpose: `dotnet run` seeds dev accounts (`admin@cinema.vn`/`user@cinema.vn`), `dotnet test` runs xUnit suites. |
| `Cinema/3-Data/Cinema.Data.Entities/` | POCO entities + enums, no business logic. All inherit `BaseEntity`. |
| `Cinema/3-Data/Cinema.Data.Contracts/` | Store interfaces + `IApplicationUnitOfWork`. |
| `Cinema/3-Data/Cinema.Data/` | `CinemaContext` (EF Core), Fluent API `Configurations/`, `Stores/`, `ApplicationUnitOfWork`, `JwtTokenService`. References `Cinema.Business.Contracts` solely to implement `ITokenService` (a narrow, deliberate exception to the layer-direction rule). |
| `Cinema/4-Foundation/Cinema.Foundation/` | Serilog-backed `ILog`/`LogProvider` abstraction. |

**To change X (backend):**
- Pricing formula → `Cinema.Business/Managers/` (catalog/showtime managers), specifically `BookingManager.BuildSeatPricingContextAsync` — see `01-business/business-rules.md` for the exact `price = roomTypePrice * priceFactor + threeDSurcharge + showTimeRoom.BasePrice` formula and precedence.
- Seat-lock behavior → `BookingManager` (static `ConcurrentDictionary`) + `BookingHub` + `PendingBookingReaper` — see `01-business/business-rules.md` for the seat-lock state machine (5-min advisory lock vs. 15-min pending-invoice hold).
- Payment callback handling → `PaymentController.PaymentCallback` + `Cinema.Business/Payments/*Gateway.cs` — see `integrations.md` §1 and `01-business/business-rules.md` for the callback-authoritative rule.
- Auth/roles → `Cinema.Business/Managers/Auth/`, `RoleNames.cs`, `IdentityController` — see `01-business/roles-permissions.md`.
- A new endpoint → `CinemaController` only, in the matching `#region`. Never a new controller.
- Schema change → entity in `Cinema.Data.Entities`, Fluent config in `Cinema.Data/Configurations/`, then hand-edit `cinemabe/sql/create_db.sql` + `upgrade_db.sql` (no EF migrations — see [[../../feedback-schema-via-sql-scripts-not-ef-migrations.md|project convention]]).

## Frontend (`cinemafe/`)

| Folder | Responsibility |
|---|---|
| `projects/CinemaUser/src/app/features/` | Customer app: auth, discover (home/movies/theaters/promotions/membership), booking (seats/checkout/payment-return), profile (bookings/tickets/cancel/notification prefs). |
| `projects/CinemaAdmin/src/app/features/` | Admin app: auth, categories (reference-data CRUD: age-restrictions, discount-types, holidays, memberships, movie-types, news, templates, user-types), operations (audit-log, comments, discounts, gift-cards, invoices, loyalty-settings, promotion-programs, splash-banners), overview (dashboard, movies, theaters+tabs, showtimes, users, reports). |
| `projects/CinemaStaff/src/app/features/` | Staff app: auth, gate (scan/lookup), home, incidents, kitchen (coded, hidden from nav — see open-questions.md), pos (counter sale), profile, reports/audit-log, sales-reports, schedule. |
| `projects/CinemaLib/src/lib/components/` | Shared UI used by 2+ apps: seat-map, shell, dialogs, table base, filter-bar, status-pill, ticket/qr, price, empty-state, forbidden, profile, bar-chart. |
| `projects/CinemaLib/src/lib/services/` | NSwag-generated API service agents (cinema/identity/payment http services) + `seat-lock-session.service`, `booking-hub`/`staff-hub` (SignalR), `toast.service`, API error handling. |
| `projects/CinemaLib/src/lib/store/` | NgRx: `auth` and `movies` feature stores (mounted in all 3 apps), plus a legacy `reducers/actions/selectors` folder (search, loading, notification slices). |
| `projects/CinemaLib/src/lib/guards/` | `authGuard`, `adminGuard`, `roleGuard`, `homeRedirectGuard`/`homeRedirectByRole`. |
| `projects/CinemaLib/src/lib/interceptors/` | `authInterceptor`, `errorInterceptor`. |
| `projects/CinemaLib/src/lib/theme/` | Single shared theme service (no per-app duplication). |

**To change X (frontend):**
- A component used by 2+ apps → `CinemaLib/src/lib/components/`, never copy-paste per app.
- Seat map UI → `CinemaLib` seat-map component + `seat-lock-session.service` + `booking-hub.service`.
- API client code → regenerate via NSwag (`Generator/GenerateNswag.ps1`), never hand-edit `*-http.service.ts`.
- Role-based routing → `CinemaLib/src/lib/guards/`.
