# UI / UX Notes

> Purpose: Screen inventory, routing structure, key user flows, and shared CinemaLib components across the three Angular apps.
> Sources: cinemafe/projects/{CinemaUser,CinemaAdmin,CinemaStaff,CinemaLib}, E:\cinema\CLAUDE.md, E:\cinema\business\admin-staff-and-frontend-flows.md
> Last reviewed: 2026-11-01

## Apps & routing structure

| App | Prefix | Port | Root routing file | Top-level guard |
|---|---|---|---|---|
| CinemaUser | `app` | 4202 | `app.routes.ts` | `authGuard` on `booking`, `profile` |
| CinemaAdmin | `app` | 4201 | `app.routes.ts` | `authGuard` + `adminGuard` on everything except `/auth/login`, `/forbidden` |
| CinemaStaff | `staff` | 4203 | `app.routes.ts` | `authGuard` + `roleGuard(STAFF_APP_ROLES)`; per-section `roleGuard` (gate/kitchen/reports/POS) |
| CinemaLib | `cl` | — | shared library, no routes | — |

### CinemaUser route tree
```
/login            -> redirect to /auth/login
/auth/*           login, register, forgot-password, reset-password, verify-email   (public)
/booking/seats     \
/booking/checkout   > authGuard
/booking/payment-return /
/profile           authGuard
/                  discover: home | movies | movies/:slug | theaters | promotions | membership  (public, matched last)
/**                -> redirect to /
```
Movie detail uses a friendly slug (`shared/movie-route.ts`); a legacy `:legacyId/:legacySlug` route redirects to `:slug`.

### CinemaAdmin route tree
```
/login             -> redirect to /auth/login
/auth/login        public
/forbidden         where adminGuard sends non-admins (outside the guarded tree, to avoid a redirect loop)
/                  authGuard + adminGuard
  ''               homeRedirectGuard -> /dashboard (Admin) or /forbidden (everyone else)
  profile
  (OverviewModule)    dashboard, reports, movies, theaters, theaters/:id, showtimes, users
  (CatalogAdminModule) age-restrictions, discount-types, holidays, memberships, movie-types, news, templates, user-types
  (OperationsModule)   audit-log, comments, discounts, gift-cards, invoices, loyalty-settings, promotion-programs, splash-banners
/**                -> /auth/login
```
Overview/Catalog/Operations are each one `NgModule` with its own internal route list, mounted via a shared `path: ''` pass-through entry — not one `.module.ts` per page.

### CinemaStaff route tree
```
/login             -> redirect to /auth/login
/auth/login        public
/forbidden         where roleGuard sends users without a staff role
/                  authGuard + roleGuard(STAFF_APP_ROLES)
  ''               homeRedirectByRole -> /home (Admin and TheaterStaff both land here today)
  home, profile
  gate/*           roleGuard(GATE_KEEPER_ROLES)   — gate-page, gate-scan, gate-lookup
  kitchen/*        roleGuard(CONCESSION_ROLES)    — routes exist but NOT linked from staff-menu.ts (hidden)
  reports/*        roleGuard(REPORTING_ROLES)     — sales-reports
  ''  (reports.routes)  roleGuard(REPORTING_ROLES) — audit-log
  schedule/*, incidents/*, roster/*   — no extra section guard (approver-only screens self-guard)
  ''  (pos.routes)      roleGuard(SELLER_ROLES)   — counter POS
/**                -> /auth/login
```
Current roles are exactly `Admin`, `Customer`, `TheaterStaff` (see business doc); all the `*_ROLES` constants above (`GATE_KEEPER_ROLES`, `SELLER_ROLES`, `REPORTING_ROLES`, `CONCESSION_ROLES`, `STAFF_APP_ROLES`) currently resolve to `Admin,TheaterStaff` — there's no longer a role-level split between e.g. a gate keeper and a seller.

## Key user flows

### CinemaUser — browse → book → pay
1. **Home** (`discover/home`) — now-showing/coming-soon, promotions banner, theaters; first non-`/booking`/`/auth` navigation shows a splash-banner popup (once per browser session, via `sessionStorage`).
2. **Movie detail** (`discover/movies/movie-detail`) — friendly slug URL; star rating + threaded comments (pending admin moderation before public visibility).
3. **Showtime selection** (`booking/booking-page`).
4. **Seat selection** (`booking/booking-selection`) — shared `seat-map` component + `seat-lock-session.service.ts`, live via SignalR (`/hubs/booking`).
5. **Checkout** (`booking/booking-checkout`) — discounts, gift cards, loyalty points, food/drink combos; surfaces out-of-stock errors for food/combo items.
6. **Payment** — redirect to VNPay sandbox; **payment-return** page polls `GetInvoice` for ~30s (IPN-authoritative, not redirect-authoritative — see root `CLAUDE.md`).
7. **Profile** — booking history, ticket view (QR via `ticket-card`/`qr-code` CinemaLib components), cancel booking, notification preferences.

### CinemaAdmin — catalog & operations management
- **Catalog**: Movies / Theaters (with nested tabs: rooms, room types, seat types, food, combos, time slots, ticket prices, loyalty, patron categories, holidays) / Showtimes / Users — standard list + dialog CRUD pattern, each with its own list component + `*.dialog.ts`.
- **Operations**: comment moderation, discounts, gift cards, invoices, loyalty settings, promotion programs (with banner upload), splash banners, audit log — all admin-only.
- **Reports**: sales/occupancy/KPI charts; CSV export is generated client-side from the JSON response (no backend file endpoint).

### CinemaStaff — box office / gate flows
- **Counter sale (POS)**: `pos.component.ts` — quote then sell a ticket (always resolves to a Paid invoice directly, never leaves a Pending one), reuses the shared seat map (`room-seat-picker`) and `counter-cart`/`counter-summary`/`counter-tender-panel` core services.
- **Gate**: `gate-scan` (QR scan; a refused scan is still HTTP 200 with an `Outcome` reason) and `gate-lookup` (find today's paid tickets by invoice code or phone).
- **Incidents / blocking**: report/list/detail an incident; `BlockSeat`/`BlockRoom` require the `Approvers` role or a manager-override PIN (`manager-override.dialog`, `manager-approval.service`, `override-flow.service`).
- **Schedule board**: today's rooms/showtimes + status, live via `staff-live.service` (SignalR).
- **Roster**: shift scheduling (`shift.dialog`); approvers manage the roster, all staff read their own shifts.

## Shared components (CinemaLib) — used by 2+ apps

Per root `CLAUDE.md`'s rule ("menus/shell/topbar/pills/dialogs/tables/seat map used by 2+ apps = ONE configurable CinemaLib component, never copy-paste per app"), the following live in `projects/CinemaLib/src/lib/components/`:

| Component | Path | Used by |
|---|---|---|
| Seat map | `seat-map/` (`seat-map.component.ts`, `seat-selection.ts`) | CinemaUser (customer booking) and CinemaStaff (`room-seat-picker` wraps it for counter sales) |
| App shell | `shell/app-shell.component.ts` | Admin + Staff (and User) shells/topbar |
| Dialogs | `dialogs/` — `confirm.dialog`, `reason.dialog`, `trailer.dialog`, `manager-override.dialog`, `dialog.service.ts` | Shared across apps; `manager-override.dialog` specifically backs the Staff PIN-override flow |
| Table base | `table/base-table.component.ts`, `base-reactive.component.ts` | CinemaAdmin + CinemaStaff list pages |
| Filter bar | `filter-bar/filter-bar.component.ts` (+ `audit-log-filter-fields.ts`) | Admin audit-log + Staff audit-log views |
| Status pill | `status-pill/status-pill.component.ts` | Admin + Staff status badges (invoice/incident/shift states) |
| Ticket card / QR | `ticket/ticket-card.component.ts`, `qr/qr-code.component.ts` | CinemaUser (e-ticket display) and CinemaStaff (gate scan reference) |
| Price formatting | `price/` | Admin + User currency display |
| Empty state | `empty-state/` | Admin + Staff + User list/empty views |
| Forbidden page | `forbidden/` | Admin + Staff `/forbidden` routes |
| Profile shared bits | `profile/` | User + Staff profile pages |
| Bar chart | `bar-chart/` | Admin reports + Staff sales-reports |

## Shared services & state (CinemaLib)

- **Seat lock**: `services/seat-lock-session.service.ts` — sole client of the backend's SignalR seat-hold broadcasts (`booking-hub.service.ts`); shared between CinemaUser and CinemaStaff.
- **Staff live updates**: `services/staff-hub.service.ts` — SignalR for staff-side live state (schedule board, incidents).
- **Theming**: `theme/theme.service.ts` — single shared theme service; dark-mode toggle for CinemaAdmin (ROADMAP 🆕) would plug in here — <!-- TODO: confirm with team whether it's currently wired into any app's shell -->.
- **NgRx stores actually mounted**: per `app.module.ts` in each app —
  - CinemaUser: `StoreModule.forRoot({ auth: authReducer, movies: moviesReducer })`
  - CinemaAdmin: `StoreModule.forRoot({ auth, movies, searchState: searchReducer, loading: loadingReducer })`
  - CinemaStaff: `StoreModule.forRoot({ auth, movies, searchState: searchReducer, loading: loadingReducer })`
  - `store/index.ts` re-exports `auth`, `movies` (per-feature folders) plus the legacy `reducers/`/`actions/`/`selectors/` folder's `searchReducer`, notification actions/effects, and `loadingReducer`.
- **Enum label lookups**: centralized in `cinema.model.ts` (per the project's "centralize enum label lookups" convention) — new admin dropdowns should look there before inlining a label map.

## Notable UX details

- **Legacy URL compatibility**: CinemaUser's `:legacyId/:legacySlug` movie route exists purely to redirect old links to the new `:slug` route.
- **Splash banner**: dismissed state tracked per banner `Id` in `sessionStorage`, so it reappears once per browser session or when a new banner is activated.
- **Payment pending state**: `payment-return` showing "Payment pending" after ~30s is expected behavior (not a bug) when VNPay's IPN hasn't reached the backend yet — see root `CLAUDE.md`.
- **Kitchen pickup queue**: fully coded (`features/kitchen`, guarded by `CONCESSION_ROLES`) but deliberately not linked from `staff-menu.ts` — reachable only by direct URL today.
