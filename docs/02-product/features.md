# Implemented Features

> Purpose: Inventory of features actually implemented across CinemaUser, CinemaAdmin, and CinemaStaff, with code locations and status.
> Sources: cinemafe/projects/{CinemaUser,CinemaAdmin,CinemaStaff,CinemaLib}, E:\cinema\ROADMAP.md, cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/CinemaController.cs (`#region`s Gate/BoxOffice/Concession/StaffReport/Operations/Workforce), cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RoleNames.cs
> Last reviewed: 2026-11-04

Status legend: **complete** (wired end-to-end, used in production routes) · **partial** (data/API exists but UI/flow incomplete, or vice versa) · **stubbed** (code present but hidden/unused). Roadmap status (`E:\cinema\ROADMAP.md`, ✅/🟡/🆕) is cited where it adds context — in a few places the code is further along than the roadmap entry suggests (noted below).

## CinemaUser (customer app, port 4202)

| Feature | Code path | Status | Notes |
|---|---|---|---|
| Home / discover | `features/modules/discover/home` | complete | Now-showing/coming-soon, promotions banner, theaters list. |
| Splash banner popup | `discover/home` | complete | `SplashBanner` entity, no FKs; active banner = `IsPublished` and today (Vietnam-local date) within `[StartDate, EndDate]`, newest `StartDate` wins on overlap. Shown once per browser session via `sessionStorage` keyed by banner `Id`. |
| Movie browse & detail | `discover/movies/movie-list`, `discover/movies/movie-detail` | complete | Friendly slug routing (`movie-route.ts`); legacy `:legacyId/:legacySlug` route redirects to `:slug`. |
| Ratings & reviews | `movie-detail.component.ts` (`rateMovie`, `addComment` actions) | complete | ROADMAP.md marks this 🟡 ("not surfaced") — code shows it **is** surfaced: star rating widget, threaded comments (`recentComments`, `replies`), `canReview` gate. Comments go through admin moderation (see CinemaAdmin). |
| Theaters listing | `discover/theaters` | complete | |
| Promotions | `discover/promotions` | complete | |
| Membership / tiers | `discover/membership` | partial | Displays `MemberShipDTO` tiers (min points, highlighted middle tier); point accrual/voucher redemption logic lives in checkout, not this page. |
| Showtime selection | `modules/booking/booking-page` | complete | |
| Seat selection | `modules/booking/booking-selection` | complete | Shared seat map (`CinemaLib` `seat-map`) + `seat-lock-session.service.ts` over SignalR `/hubs/booking`. |
| Checkout (discounts, gift cards, loyalty points, food/drink combos) | `modules/booking/booking-checkout` | complete | Handles combo/food out-of-stock errors explicitly (`booking-checkout.component.ts` ~line 481). ROADMAP marks food/drink combos 🟡 ("wire into booking flow") — code shows this is wired. |
| Payment (VNPay sandbox) | redirect out + `modules/booking/payment-return` | complete | Polls `GetInvoice` for ~30s; IPN-authoritative per root `CLAUDE.md`. |
| Booking history / my tickets | `features/profile` | complete | |
| Cancel booking | `profile.component.ts` (`cancelBooking`, `cancelById`) | complete | ROADMAP marks "manage booked tickets (view/cancel)" 🟡 — code shows cancel is implemented via `PaymentServiceAgent.cancelBooking`. |
| E-tickets + QR codes | `CinemaLib` `components/ticket` (`ticket-card`), `components/qr` (`qr-code`) | complete | ROADMAP marks 🆕 but the QR component and ticket card exist and are used; <!-- TODO: confirm with team whether server-side scan validation (CinemaStaff gate) is the same feature or a separate build --> |
| Notification preferences | `features/profile` → `CinemaLib` `notification.actions.ts`/`notification.effects.ts` | complete | |
| Auth (login/register/forgot/reset/verify email, Google login) | `features/auth/*` | complete | Facebook login is ROADMAP 🆕, not found in `auth/` folders — not implemented. |

## CinemaAdmin (admin app, port 4201)

| Feature | Code path | Status | Notes |
|---|---|---|---|
| Dashboard | `modules/overview/dashboard` | complete | ROADMAP ✅. |
| Movies management | `modules/overview/movies` | complete | |
| Theaters management (+ tabs: info, rooms, room types, seat types, food, combos, time slots, ticket prices, loyalty, patron categories, staff, holidays) | `modules/overview/theaters/*` (`theater-detail.component.ts` + per-tab components/dialogs) | complete | Matches ROADMAP "Recently shipped": multi-theater catalog, seat-map editor, per-theater pricing. Staff tab (`theater-staff.component.ts`) lists/adds/edits/removes staff scoped to that cinema (`GetUsers` filtered by `theaterId`), reusing `user.dialog.ts` with the theater pre-filled and locked. |
| Showtimes management | `modules/overview/show-times` | complete | |
| Users management | `modules/overview/users` | complete | Lists a Cinema column (`UserDTO.TheaterName`, batch-resolved, blank for theater-less users). |
| Reports (sales/occupancy/KPIs) + CSV export | `modules/overview/reports` | complete | `GetSales`/`GetOccupancy`/`GetKpis` return structured DTOs; CSV built client-side (`_downloadCsv`, UTF-8 BOM, `Blob`/`a.download`) — there is no backend CSV-generation endpoint. |
| Reference-data CRUD: age-restrictions, discount-types, holidays, memberships, movie-types, news, user-types | `modules/categories/*` | complete | Each feature folder has its own component (no shared generic admin CRUD base). |
| Notification templates | `modules/categories/templates` | complete | Booking-confirmation message content, distinct from user notification preferences. |
| Comment moderation | `modules/operations/comments` | complete | Approve/reject/delete, admin-only. |
| Discounts | `modules/operations/discounts` | complete | Per-theater scoping (`TheaterId` on `Discount`) is ROADMAP 🆕 — <!-- TODO: confirm with team whether discount.dialog.ts currently exposes a theater scope field -->. |
| Gift cards | `modules/operations/gift-cards` | complete | |
| Invoices (admin list/status/void) | `modules/operations/invoices` | complete | |
| Loyalty settings | `modules/operations/loyalty-settings` | complete | |
| Promotion programs (+ banner upload) | `modules/operations/promotion-programs` | complete | |
| Splash banner management | `modules/operations/splash-banners` | complete | CRUD for the CinemaUser popup banner. |
| Audit log viewer | `modules/operations/audit-log` | complete | Admin-only, theater-filterable. |
| Dark mode (admin) | — | not found | ROADMAP 🆕; no theme-toggle UI found under CinemaAdmin features (shared `theme/theme.service.ts` exists in CinemaLib — <!-- TODO: confirm with team whether it's wired into CinemaAdmin's shell -->). |

## CinemaStaff (staff app, port 4203)

The `CinemaStaff` app and the `TheaterStaff` role both still exist (verified directly against `cinemafe/angular.json` and `CinemaController.cs`); several sub-features (refund/exchange, drawer close, customer-service desk, time-clock, tasks, checklists, inventory/storage-plan) were removed, and five granular roles (`TheaterManager`, `BoxOfficeStaff`, `GateStaff`, `KitchenStaff`, `RegionalManager`) were collapsed into one (`TheaterStaff`).

| Feature | Code path | Status | Notes |
|---|---|---|---|
| Login | `features/auth/login` | complete | |
| Home | `features/home` | complete | Role-based landing page (all current roles → `/home`). |
| Gate: ticket scan | `features/gate/gate-scan*` | complete | `CinemaController.Scan`: a refused scan is still HTTP 200 with `Outcome` carrying the reason (`NotFound`/`NotPaid`/`AlreadyUsed`/`WrongTheater`/`WrongShowTime`/`TooEarly`/`Expired`/`AgeCheckRequired`) — only an auth failure is a non-200 response. |
| Gate: lookup | `features/gate/gate-lookup*` | complete | Find paid tickets by invoice code or phone. |
| Counter sale (POS) | `features/pos` | complete | Quote/sell flow backed by `#region BoxOffice`. |
| Schedule board | `features/schedule` | complete | Rooms + today's showtimes + status. |
| Incidents (report/list/detail, seat/room block) | `features/incidents` | complete | Block actions, and resolve-with-unblock, require the `Approvers` role (Admin only) — no PIN-based alternate path. |
| Sales reports | `features/sales-reports` | complete | Management-role gated (`REPORTING_ROLES`). |
| Audit log (staff-facing) | `features/reports/audit-log` | complete | Theater-scoped, management roles. |
| Profile | `features/profile` | complete | Account summary. |
| Kitchen pickup queue | `features/kitchen` | **stubbed** | Code and routes exist (`kitchen.routes.ts`, guarded by `CONCESSION_ROLES`), but intentionally excluded from `staff-menu.ts` navigation — comment in that file confirms it's hidden, not deleted. |
| Cash drawer close / daily close | — | removed | UI removed from CinemaStaff; backend `DrawerReconcile`/`CashPayOut` audit actions remain (possibly vestigial). |
| Customer-service desk, time-clock, tasks, checklists, inventory/storage-plan | — | removed | Confirmed absent from current feature tree and sidebar. |
| Roster / shift scheduling, manager-override PIN | — | removed | Roster (`features/roster`, `StaffShift` table) and the manager-override-PIN mechanism (`ManagerOverrideService`, `User.OverridePin*` columns) were both removed outright; blocking/unblocking and POS price overrides now require the `Approvers` role directly. |

## Cross-cutting / not yet built (per ROADMAP.md)

| Feature | Status | Notes |
|---|---|---|
| Facebook login | 🆕 not started | Google login exists as the pattern to mirror. |
| Real payment gateway beyond VNPay sandbox (MoMo/Stripe) | 🟡 | `PaymentController` + gateway abstraction exist; only VNPay implemented. |
| Movie recommendations by preference | 🆕 not started | |
| Unified search/filter (theater/date/time/genre/language) | 🟡 partial | Some search exists; no unified filter UI confirmed. |
| Nearest theater by location | 🆕 not started | |
| Promotions scoped per-theater | 🆕 not started | |
| Revenue/advanced stats dashboard | 🆕 partial | Basic reports exist (`modules/overview/reports`); advanced charts not confirmed. |
| Messaging infra (real email/SMS providers) | 🟡 | `SmtpNotificationService` (email), `TwilioSmsNotificationService`/`DevLogSmsNotificationService` (SMS, dev fallback logs instead of sending) exist in `Cinema.Business/Notifications/*` — may be further along than ROADMAP's 🆕. |
| Multilingual (i18n) | partial | `@ngx-translate/core` is a dependency and used in components (e.g. `movie-detail.component.ts` uses `_translate.instant`) — <!-- TODO: confirm with team how many languages are actually translated -->. |
| Support chatbot | 🆕 not started | |
