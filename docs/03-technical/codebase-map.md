# Codebase Map

> Purpose: Folder/module → responsibility, plus a "where to change X" guide.
> Sources: cinemabe/Cinema/**, cinemafe/projects/**, CLAUDE.md
> Last reviewed: 2026-11-10

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
| `Cinema/2-Business/Cinema.Business/Payments/` | `IPaymentGateway` implementations (Sandbox/VNPay) + gateway resolver. |
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
| `projects/CinemaAdmin/src/app/features/` | Admin app: auth, categories (reference-data CRUD: age-restrictions, discount-types, holidays, memberships, movie-types, news, templates, user-types), operations (audit-log, comments, discounts, gift-cards, invoices, promotion-programs, splash-banners), overview (dashboard, movies, theaters+tabs, showtimes, users, reports). |
| `projects/CinemaStaff/src/app/features/` | Staff app: auth, gate (scan/lookup), home, incidents, pos (counter sale), profile, reports/audit-log, sales-reports, schedule. |
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
- Responsive breakpoints → `CinemaLib/src/lib/theme/_mixins.scss` is the single source: `m.phone` (<=480), `m.tablet-small` (<=768, drawer nav + card lists), `m.tablet` (<=1024, icon-rail sidebar). Use `@use 'lib/theme/mixins' as m;` in app component SCSS (CinemaLib components use a relative `../../theme/mixins`). Shared admin responsive rules (fluid inputs, 44px touch targets, dialog max-width, `.dlg-row--2/3` collapse) live in `_marquee-ledger.scss` / `_dialog-ledger.scss`.
- Horizontal scrollers → CinemaUser `shared/scroll-row` (`app-scroll-row`) wraps every row/strip that scrolls sideways (home rows, movie-detail cast/crew). Contract: prev/next chevrons for fine pointers whenever the content overflows (hidden only on `hover: none` + `pointer: coarse` and when nothing overflows; disabled at the ends; 44px and kept inside the viewport at <=1024), shift/horizontal wheel + touch swipe natively, mouse click-drag (a drag never activates the card under the pointer), Arrow/Home/End on the focused track, edge fade at <=1024, plain vertical wheel never trapped. Re-measures on resize, content change and image load. Do not hand-roll a new scroller; reuse it. Wrapping tab bars (e.g. profile nav) must wrap rather than scroll so no tab is hidden off-screen.
- Mouse drag-to-pan for non-CinemaUser scrollers → `cinemaDragScroll` directive (`CinemaLib/src/lib/directives/drag-scroll.directive.ts`, exported via `SharedModule`). Put it on any horizontally scrolling container (admin showtimes board, report/dashboard day charts, seat-map grid, staff schedule board). Mouse only (touch/pen keep native panning), starts after a 5px move so clicks on blocks still work, swallows the click that ends a drag, shows the grab cursor only while the container overflows. Wheel/shift+wheel/keyboard/touch are the browser defaults; give the container `overscroll-behavior-x: contain` where it matters.
- CinemaUser responsive → all CinemaUser component SCSS uses the shared mixins (no hand-typed `@media` except `prefers-*`/`hover`). Seat-map seat size is the CSS custom property `--cl-seat-size` (default 34px in `seat-map.component.scss`, so CinemaStaff is unchanged); `booking-selection.component.scss` lowers it to 30px (<=768) / 28px (<=480). `BookingSelectionComponent` publishes the fixed order bar's real height as `--cx-order-bar-h` on `<html>`; the spacer and the chatbot FAB (<=768) use it to stay clear of the bar.
- CinemaStaff responsive → CinemaStaff component SCSS uses the shared mixins (only `@media print` in `pos-receipt.component.scss` stays hand-typed). `counter-cart` sets `--cl-seat-size: 30px` on `cl-seat-map` at `m.phone` only; ticket rows restyle to cards via local SCSS (no markup duplication) at `m.tablet`; `incident-list` and `audit-log` use the `.ad-desktop-only` / `.ad-mobile-only` card pattern.
