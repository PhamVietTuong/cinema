# Implemented Features

> Purpose: Inventory of features actually implemented across CinemaUser, CinemaAdmin, and CinemaStaff, with code locations and status.
> Sources: cinemafe/projects/{CinemaUser,CinemaAdmin,CinemaStaff,CinemaLib}, E:\cinema\ROADMAP.md, cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/CinemaController.cs (`#region`s Gate/BoxOffice/StaffReport/Operations/Workforce), cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RoleNames.cs
> Last reviewed: 2026-11-06

Status legend: **complete** (wired end-to-end, used in production routes) · **partial** (data/API exists but UI/flow incomplete, or vice versa) · **stubbed** (code present but hidden/unused). Roadmap status (`E:\cinema\ROADMAP.md`, ✅/🟡/🆕) is cited where it adds context — in a few places the code is further along than the roadmap entry suggests (noted below).

## CinemaUser (customer app, port 4202)

| Feature | Code path | Status | Notes |
|---|---|---|---|
| Home / discover | `features/modules/discover/home` | complete | Now-showing/coming-soon, promotions banner, theaters list. ROADMAP marks "movie recommendations by preference" 🆕 — code shows it's implemented: `CinemaController.GetRecommendedMovies` → `MovieManager.GetRecommendedAsync` personalizes for a signed-in user (anonymous callers get top-rated picks), wired into this page via `home.component.ts`'s `getRecommendedMovies(8)`. |
| Splash banner popup | `shared/splash-banner` | complete | `SplashBanner` entity, no FKs; active banner = `IsPublished` and today (Vietnam-local date) within `[StartDate, EndDate]`, newest `StartDate` wins on overlap. Shown once per browser session via `sessionStorage` keyed by banner `Id`. |
| Movie browse & detail | `discover/movies/movie-list`, `discover/movies/movie-detail` | complete | Friendly slug routing (`movie-route.ts`); legacy `:legacyId/:legacySlug` route redirects to `:slug`. |
| Ratings & reviews | `movie-detail.component.ts` (`rateMovie`, `addComment` actions) | complete | ROADMAP.md marks this 🟡 ("not surfaced") — code shows it **is** surfaced: star rating widget, threaded comments (`recentComments`, `replies`), `canReview` gate. Comments go through admin moderation (see CinemaAdmin). |
| Theaters listing | `discover/theaters` | complete | ROADMAP marks "nearest theater by location" 🆕 — code shows it's implemented: `theaters.component.ts` sorts by distance via `navigator.geolocation` + an inline Haversine calc, shows a "X km" chip per theater, with a `geoNotSupported`/`geoFailed` fallback when permission is denied/unavailable. Coordinates are manually entered per theater in CinemaAdmin (`Theater.Latitude`/`Longitude`), not geocoded automatically. |
| Promotions | `discover/promotions` | complete | |
| Membership / tiers | `discover/membership` | partial | Displays `MemberShipDTO` tiers (min points, highlighted middle tier); point accrual/voucher redemption logic lives in checkout, not this page. |
| Showtime selection | `modules/booking/booking-page` | complete | |
| Seat selection | `modules/booking/booking-selection` | complete | Shared seat map (`CinemaLib` `seat-map`) + `seat-lock-session.service.ts` over SignalR `/hubs/booking`. |
| Checkout (discounts, gift cards, loyalty points, food/drink combos) | `modules/booking/booking-checkout` | complete | Handles combo/food out-of-stock errors explicitly (`booking-checkout.component.ts` ~line 481). ROADMAP marks food/drink combos 🟡 ("wire into booking flow") — code shows this is wired. |
| Payment (VNPay sandbox) | redirect out + `modules/booking/payment-return` | complete | Polls `GetInvoice` for ~30s; IPN-authoritative per root `CLAUDE.md`. |
| Booking history / my tickets | `features/profile` | complete | |
| E-tickets + QR codes | `features/profile` (`profile.component.ts`'s `qrMap`/`_buildQrCodes`) | complete | ROADMAP marks 🆕 but it's implemented — however not via the shared `CinemaLib` `ticket-card`/`qr-code` components the previous version of this row cited: those are dead code in CinemaUser (`cl-qr-code` has zero usages anywhere; `cl-ticket-card` is only used by CinemaStaff's POS receipt). CinemaUser renders its own QR images inline in the profile/booking-history view. |
| Notification preferences | `features/profile` → `CinemaLib` `notification.actions.ts`/`notification.effects.ts` | complete | |
| Auth (login/register/forgot/reset/verify email, Google login, Facebook login) | `features/auth/*`, `IdentityController.FacebookLogin` | complete | ROADMAP marks Facebook login 🆕 — it's actually implemented end-to-end (`login.component.ts`'s `loginWithFacebook()`, FB JS SDK, backend `FacebookLogin` action) but gated behind `facebookEnabled = !!environment.facebookAppId`, and both environment files ship an empty `facebookAppId` — so the button is hidden in practice until a real Facebook App ID is configured, which is presumably why it reads as "not implemented". |
| Support chatbot | `shared/chatbot` | complete | ROADMAP marks 🆕 — code shows it's implemented: rule-based, no LLM/API key, keyword-matched against a 7-topic FAQ array (booking, showtimes, cancel, membership, nearby theater, payment, combo) plus 5 quick-reply chips. Mounted globally within CinemaUser only (`app.html`), not in CinemaAdmin/CinemaStaff. Fallback re-prompts with topic suggestions rather than a literal "contact support" message, since no support email/phone/ticket endpoint exists in the app for such a CTA to target. Matching is plain Vietnamese-keyword substring matching (order-dependent, first match wins) — a user typing a question in English rather than using the pre-translated quick-reply chips will mostly hit the fallback, despite the app being bilingual. |

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
| Discounts | `modules/operations/discounts` | complete | Per-theater scoping is already implemented: `Discount.ApplyToAllTheaters`/`DiscountTheaters` (entity), `DiscountDTO.TheaterIds`, and `discount.dialog.ts`'s `selectedTheaterIds` multi-select. ROADMAP's "Promotions scoped per-theater" 🆕 entry refers to the separate `PromotionProgram` entity, which genuinely has no theater scoping yet — easy to conflate since both live in the Operations module. |
| Gift cards | `modules/operations/gift-cards` | complete | |
| Invoices (admin list/status/void) | `modules/operations/invoices` | complete | |
| Loyalty settings | `modules/operations/loyalty-settings` | complete | |
| Promotion programs (+ banner upload) | `modules/operations/promotion-programs` | complete | |
| Splash banner management | `modules/operations/splash-banners` | complete | CRUD for the CinemaUser popup banner. |
| Audit log viewer | `modules/operations/audit-log` | complete | Admin-only, theater-filterable. |
| Dark mode (admin) | `CinemaLib` `theme/theme.service.ts` + `components/shell/app-shell.component` | complete | ROADMAP marks 🆕 — code shows it's implemented: the shared `cl-app-shell` component (used as CinemaAdmin's root shell) has a built-in dark-mode toggle button wired to `ThemeService`; `CinemaAdmin/src/styles.scss` and `CinemaLib`'s `_marquee-ledger.scss` already define full `[data-theme="dark"]` variants for every `--ad-*` CSS variable, so the toggle works with zero CinemaAdmin-specific CSS. |

## CinemaStaff (staff app, port 4203)

The `CinemaStaff` app and the `TheaterStaff` role both still exist (verified directly against `cinemafe/angular.json` and `CinemaController.cs`); five granular roles (`TheaterManager`, `BoxOfficeStaff`, `GateStaff`, `KitchenStaff`, `RegionalManager`) were collapsed into one (`TheaterStaff`).

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

## Cross-cutting / not yet built (per ROADMAP.md)

| Feature | Status | Notes |
|---|---|---|
| Real payment gateway beyond VNPay sandbox (Stripe) | 🟡 partial | `StripeGateway.cs` exists and is DI-registered (`Program.cs`) alongside VNPay/MoMo, but CinemaUser's checkout UI (`booking-checkout.component.ts`) only offers VNPay and MoMo as selectable payment methods — Stripe has no frontend entry point yet. MoMo itself is fully wired (backend `MoMoGateway.cs` + a selectable `'Momo'` checkout option), not just backend-only as previously noted here. |
| Unified search/filter (theater/date/time/genre/language) | 🟡 partial | Some search exists; no unified filter UI confirmed. |
| Promotions scoped per-theater (`PromotionProgram`) | 🆕 not started | Separate from `Discount`, which already has working per-theater scoping (see CinemaAdmin's "Discounts" row above) — easy to conflate since both live in the Operations module. |
| Revenue/advanced stats dashboard | 🆕 partial | Basic reports exist (`modules/overview/reports`); advanced charts not confirmed. |
| Messaging infra (real email/SMS providers) | 🟡 | `SmtpNotificationService` (email), `TwilioSmsNotificationService`/`DevLogSmsNotificationService` (SMS, dev fallback logs instead of sending) exist in `Cinema.Business/Notifications/*` — may be further along than ROADMAP's 🆕. |
| Multilingual (i18n) | partial | `@ngx-translate/core` is a dependency and used in components (e.g. `movie-detail.component.ts` uses `_translate.instant`) — <!-- TODO: confirm with team how many languages are actually translated -->. |
