# Cinema Platform — Admin/Staff Operations & Frontend Business Flows

Audience: AI agents working on this codebase. Verified directly against code as of 2026-10-31 (branch `feature/business-docs`). This supersedes assumptions from the "remove-staff-features"/"remove-staff-roles" effort — **staff/box-office/gate/workforce were trimmed, not eliminated.**

---

## IMPORTANT — correcting the premise

The task brief assumed staff/box-office/workforce features might have been fully removed and that the platform might now be CinemaUser + CinemaAdmin only. **That is not what the code shows:**

- `CinemaStaff` is still a live Angular app in `cinemafe/angular.json` (`projects.CinemaStaff`, port 4203) with a real feature set: POS/counter sale, gate scan/lookup, schedule board, incidents, roster, sales reports, kitchen pickup queue, profile override-PIN.
- The backend still has `#region BoxOffice`, `#region Gate`, `#region Workforce`, `#region StaffReport`, `#region Operations` inside `CinemaController.cs`, backed by `IBoxOfficeManager`, `IWorkforceManager`, `GateManager`, `IncidentManager`.
- What **was** actually removed (per git history, `feature/remove-staff-features` + `feature/remove-staff-roles` + follow-ups): after-sales/refund-exchange UI, cash-drawer close/daily-close, customer-service desk, time-clock, tasks, checklists, inventory/storage-plan management, and the granular roles `TheaterManager`, `BoxOfficeStaff`, `GateStaff`, `KitchenStaff`, `RegionalManager` — all of these were collapsed into a single `TheaterStaff` role (see `RoleNames.cs`). Kitchen pickup queue UI was hidden from the staff sidebar (`c21da1f`) but the component files remain (`features/kitchen/*`).
- Current roles are exactly three: `Admin`, `Customer`, `TheaterStaff` (`cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RoleNames.cs`). All staff-capability constants (`Sellers`, `GateKeepers`, `Concession`, `BackOffice`, `StaffApp`) now resolve to `Admin,TheaterStaff` — i.e. there is no longer any role-level distinction between "a seller" and "a gatekeeper"; the authorization model is now all-staff-can-do-all-staff-things plus an `Approvers`/`Reporting`/`StockApprovers` tier that is `Admin`-only.
- The 3-controller rule still holds (`CinemaController`, `IdentityController`, `PaymentController` — enforced by `ControllerArchitectureTests.OnlyThreeControllersExist`); `WorkforceController` was merged into `CinemaController` (`25f1e14`) and Concession/Gate/BoxOffice/CustomerService/StaffReport were consolidated into regions (`f8a6742`), confirming the "single-cinema-controller" refactor did happen, as a consolidation rather than a deletion.

So: **CinemaStaff app and the staff role both still exist**; what changed is scope (fewer staff sub-features) and role granularity (one role instead of five).

---

# Part A — Backend admin / staff operations

## A.1 CinemaController region inventory

`CinemaController.cs` (2481 lines) is organized in `#region` blocks. Catalog/booking/payment regions are out of scope here (owned by the normal CRUD/booking docs); the ones below are the admin/staff-only surface:

| Region | Lines | Role gate | What it does |
|---|---|---|---|
| `Comment moderation (admin)` (inline, before the numbered regions) | ~379-430 | `_adminRole` (Admin) | `GetCommentsForModeration`, `ModerateComment` (approve/reject), `DeleteComment`. |
| `Template` | 722-779 | `_adminRole` | CRUD for `Template` entities — booking-confirmation / notification message templates (`TemplateDTO`), not UI page templates. Admin screen: `features/modules/categories/templates`. |
| `Invoice (admin: list / status / delete)` | 1697-1723 | Admin | List/inspect/void invoices outside the normal checkout path. |
| `Loyalty` | 1723-1778 | Admin | Loyalty program settings (points rules). Admin screen: `operations/loyalty-settings`. |
| `Gate` | 1920-1969 | `RoleNames.GateKeepers` = `Admin,TheaterStaff` | `Scan` (QR ticket scan; a refused scan is still HTTP 200 with `Outcome` carrying the reason — only auth failures are 403), `Lookup` (find today's paid tickets by invoice code or phone). |
| `BoxOffice` | 1970-2056 | `RoleNames.Sellers` = `Admin,TheaterStaff` | `Quote` (price a counter sale without committing), `Sell` (rings up a Paid invoice + tenders directly — never leaves a Pending invoice), `GetShowtimesToday`, `FindCustomer` (by exact phone, for loyalty attach). |
| `StaffReport` | 2057-2161 | `RoleNames.Reporting` = `Admin` only | `GetAuditLog` (paged, theater-scoped), `GetSales` / `GetOccupancy` / `GetKpis` — all take a date range capped at 92 days and a theater scope; a theater outside the caller's scope is refused with 403. CSV export is done **client-side** in the admin Reports page, not by a backend endpoint (see A.4). |
| `Operations` | 2162-2330 | `RoleNames.StaffApp` = `Admin,TheaterStaff` | `GetScheduleBoard` (rooms + today's showtimes + status), incident lifecycle: `ReportIncident`, `GetIncidents`, `GetIncident`, `ResolveIncident` (with optional `Unblock` to reopen a blocked seat/room), `BlockSeat`, `BlockRoom` (both require either the `Approvers` role or a manager-override PIN — 403 otherwise). |
| `Workforce` | 2331-2481 (end of file) | Mixed: `Approvers` (Admin) for roster CRUD and PIN set, `StaffApp` for read-your-own | `SetMyOverridePin`, `GetOverrideApprovers`, `GetTheaterStaff`, `GetRoster`, `SaveShift`, `DeleteShift`, `GetMyShifts` (every staff member can read their own shifts; only approvers manage the roster). |

Explicitly **removed** (verified absent from current controller and current `CinemaStaff` sidebar, confirmed by git history): `CustomerService`/after-sales desk, `DrawerClose`/`DailyClose` (cash drawer reconciliation), `TimeClock`, `Tasks`, `Checklists`, inventory/warehouse `StorageStorage`-plan endpoints. `AuditAction` enum still reserves slots `4` (was `Refund`) and `16` (was `UserTheatersChanged`) with an explicit "do not reuse" comment — direct evidence those actions existed and were retired, consistent with the removed refund/exchange and per-theater-assignment-change features.

No "box-office/gate/workforce regions were removed" — they were **not** removed; only specific actions inside them were retired (notably the old `Refund` action, replaced by `Exchange`/`VoidSale`).

## A.2 AuditLog — single shared table, confirmed

`AuditLog` (`cinemabe/Cinema/3-Data/Cinema.Data.Entities/Entities/AuditLog.cs`) is one insert-only table with no foreign keys (by design, so the trail survives deletion of the referenced user/theater/entity). Discriminator is the `AuditAction` enum (`Cinema.Data.Enums.AuditAction`), stored as `int`, with an explicit "never renumber" contract. Current live values:

| Value | Meaning |
|---|---|
| 0 | `Other` |
| 1 | `OverrideFailed` — wrong PIN / refused approver |
| 2 | `OverridePinChanged` |
| 3 | `PriceOverride` |
| ~~4~~ | reserved, was `Refund` — retired, do not reuse |
| 5 | `Exchange` |
| 6 | `Reprint` |
| 7 | `VoidSale` |
| 8 | `CashPayOut` |
| 9 | `DrawerReconcile` |
| 10 | `Compensation` |
| 11 | `PointsAdjust` |
| 12 | `ResendTicket` |
| 13 | `BlockSeat` |
| 14 | `BlockRoom` |
| 15 | `TicketAdmitOverride` |
| ~~16~~ | reserved, was `UserTheatersChanged` — retired, do not reuse |
| 17 | `GateScan` — logs every scan outcome, not just admits; "best-effort, not transactional" per the doc comment |

This matches the "single log table" memory rule (`feedback-single-log-table.md`): one `AuditLog` table, discriminated by `Action`, no per-feature log tables. `AuditLogger.cs` (`Cinema.Business/Managers/AuditLogger.cs`) is the write path; `AuditLogStore.cs` / `AuditLogSearchCriteria.cs` back `GetAuditLog`. The admin UI for it is `CinemaAdmin/features/modules/operations/audit-log`; there is also a `CinemaStaff/features/reports/audit-log` view (manager-facing, theater-scoped).

## A.3 CinemaStaff app — current reality

`cinemafe/angular.json` still lists `CinemaStaff` as a full application project (port 4203, prefix `staff`). Current feature folders under `CinemaStaff/src/app/features`:

- `auth/login`
- `gate/` — `gate-page`, `gate-scan`, `gate-lookup`
- `home/`
- `incidents/` — `incident-list`, `incident-detail`, `report-incident` dialog
- `kitchen/` — pickup-queue state/component (present in code, hidden from sidebar navigation per `c21da1f`)
- `pos/` — counter sale page
- `profile/` — includes `override-pin`
- `reports/audit-log`
- `roster/` — shift scheduling, `shift.dialog`
- `sales-reports/`
- `schedule/` — `schedule-board`

Shared `core/` services: `counter-cart`, `counter-summary`, `counter-tender-panel`, `manager-approval.service` (PIN override flow), `override-flow.service`, `price-override.dialog`, `room-seat-picker` (reuses the seat map), `staff-live.service` (SignalR), `theater-context.service` (scope resolution mirroring the backend's `TryGetStaffScope`/`scope.Resolve`).

## A.4 Reports / CSV export

There is **no backend CSV endpoint**. `GetSales`/`GetOccupancy`/`GetKpis` return structured DTOs (`SalesReportDTO`, `OccupancyReportDTO`, `StaffKpisDTO`); the Admin "Reports" page (`CinemaAdmin/features/modules/overview/reports/reports.component.ts`) builds the CSV client-side (`_downloadCsv` helper, UTF-8 BOM prefixed, `Blob`/`a.download`) from that JSON for revenue-by-day, revenue-by-movie, revenue-by-theater. If a server-side export is ever wanted, it does not exist yet and `GetSales` et al. would need a new shape rather than reuse.

## A.5 Notification templates vs. notification preferences — two different features

These are commonly conflated; they are separate:
- **Notification preferences** (user-level, per-channel opt in/out) — `IdentityController.UpdateNotificationPreferences`, backed by `UpdateNotificationPreferencesRequest` / `AuthManager.UpdateNotificationPreferencesAsync`. Frontend: `CinemaUser/features/profile`, dispatched through `CinemaLib`'s `notification.actions.ts` / `notification.effects.ts`.
- **Notification templates** (admin-authored message templates, e.g. booking confirmation) — the `Template` entity/region in `CinemaController` (CRUD under `_adminRole`). Frontend: `CinemaAdmin/features/modules/categories/templates`. Backed by `BookingConfirmationTemplateTests.cs` and `BookingConfirmationRow.cs` in the data layer — confirms templates currently drive booking-confirmation messages specifically, not a generic template engine.

Delivery itself goes through `Cinema.Business/Notifications/*`: `SmtpNotificationService` (email), `TwilioSmsNotificationService` / `DevLogSmsNotificationService` (SMS, dev fallback logs instead of sending), `NoOpSeatNotificationService` / `NoOpStaffNotificationService` (SignalR seat/staff push no-ops for tests), plus `ShowtimeReminderService` as a background reminder job.

---

# Part B — Frontend business flows

## B.1 CinemaUser — customer journey

Feature folders: `features/modules/discover` (home, movies, theaters, promotions, membership), `features/modules/booking` (booking-page, booking-selection, booking-checkout, payment-return), `features/auth` (login/register/forgot-password/reset-password/verify-email), `features/profile`.

### B.1.1 Browse → book → pay (numbered flow)

1. **Home / Discover** (`discover/home`) — lists now-showing/coming-soon movies, promotions banner, theaters.
2. **Movie detail** — reached via a friendly slug route. `CinemaUser/shared/movie-route.ts` builds/parses the slug (title-derived, human-readable) used by `discover.routes.ts` and `movie-card.component.ts`; `movie-detail.component.ts` resolves the slug back to a movie id for the API call. → verify: navigating to a movie's card link lands on a `/movies/<slug>` URL, not a raw GUID route.
3. **Showtime selection** (`booking/booking-page`) — pick theater/date/showtime for the resolved movie.
4. **Seat selection** (`booking/booking-selection`) — seat map shared via `CinemaLib`'s `seat-lock-session.service.ts`, which calls the backend's process-local seat-lock (`BookingManager`'s static `ConcurrentDictionary`) over SignalR (`/hubs/booking`) so concurrent shoppers see live seat state.
5. **Checkout** (`booking/booking-checkout`) — applies discounts, gift cards, and loyalty points against the booking total before payment; also attaches combos/concessions.
6. **Payment** — redirect to the payment gateway (VNPay sandbox etc.); return handled by `booking/payment-return`, which polls `GetInvoice` for ~30s because VNPay is IPN-authoritative, not redirect-authoritative (see root `CLAUDE.md` — unchanged by this review).
7. **Tickets / bookings history** — under `profile` (booking history + ticket view), reachable post-login.
8. **Comments/reviews** — `AddComment` on a movie (`CinemaController`, non-admin region before line 379), threaded via `ParentId`; comments then await moderation (Part A.1) before public visibility — confirm the exact gate (pending-by-default vs. visible-then-removable) by reading `MovieManager.AddCommentAsync`/`ModerateCommentAsync` if this distinction matters to a change.
9. **Notification preferences** — set in `profile.component.ts`, dispatched via `CinemaLib`'s `notification.actions.ts` to `IdentityController.UpdateNotificationPreferences`.

## B.2 CinemaAdmin — admin journeys

Feature tree: `features/modules/categories/*` (reference-data CRUD: age-restrictions, discount-types, holidays, memberships, movie-types, news, templates, user-types) and `features/modules/operations/*` (audit-log, comments, discounts, gift-cards, invoices, loyalty-settings, promotion-programs), plus `features/modules/overview/*` (dashboard, movies, reports, show-times, theaters, users).

### B.2.1 Catalog & theater/showtime/pricing management
Standard CRUD screens under `overview/movies`, `overview/theaters`, `overview/show-times`, plus pricing-adjacent categories (`discount-types`, `age-restrictions`, `user-types`, `memberships`). Each talks to the generic catalog endpoints noted in root `CLAUDE.md` (POST-everywhere, `PagingSearchDTO`/`DefaultSearchResults<T>`); the "catalog-crud-base-removal" refactor is reflected in there being no shared generic `CatalogManager<>`-driven admin component abstraction left in the frontend — each admin feature folder has its own component, not a shared base (confirm by diffing two category components if you need the exact shared/non-shared boundary before changing one).

### B.2.2 Promotion program management
`operations/promotion-programs` — CRUD + banner image upload (per recent commits `930f2f1`, `a9c7977`), backed by `CinemaController`'s `#region PromotionProgram`.

### B.2.3 Comment moderation
`operations/comments` → `GetCommentsForModeration` (paged), `ModerateComment` (approve/reject a single comment), `DeleteComment`. Admin-only (`_adminRole`).

### B.2.4 Reports / CSV export
`overview/reports` → calls `GetSales`/`GetOccupancy`/`GetKpis` (Admin-only via `RoleNames.Reporting`), then exports CSV **client-side** (see A.4) — no backend file-generation endpoint to look for.

### B.2.5 Notification templates
`categories/templates` → full CRUD against the `Template` region (A.5). This is about booking-confirmation message content, not channel on/off preferences.

### B.2.6 Audit log viewer
`operations/audit-log` → `GetAuditLog`, Admin-only, theater-filterable.

## B.3 CinemaLib — shared infrastructure used by the above

- **Seat lock**: `services/seat-lock-session.service.ts` — the only client of the backend's SignalR seat-hold broadcasts; shared between `CinemaUser` (customer booking) and `CinemaStaff` (`room-seat-picker` for counter sales), confirming the "share repeated UI via CinemaLib" rule is followed for this component.
- **NgRx**: `store/auth` and `store/movies` are the two feature stores actually mounted (`StoreModule.forRoot({ auth, movies })` per app, per root `CLAUDE.md`); `store/actions`, `store/effects`, `store/reducers`, `store/selectors` hold a broader set including `notification.actions.ts`/`notification.effects.ts` for the preferences flow — check `store/index.ts` for what's currently wired into each app's root reducer before assuming a given slice is live in all three apps.
- **Theming / dark mode**: `theme/theme.service.ts` — single shared theme service; no per-app duplication found, consistent with "frontend-library-css-refactor."
- **Enum label lookups**: per the memory rule, these belong in `cinema.model.ts` in CinemaLib — spot-check any new admin dropdown against that file rather than inlining a label map in the component.

---

## Summary of explicitly-verified removals vs. survivals

| Feature | Status |
|---|---|
| CinemaStaff app | **Survives** — full Angular app, port 4203 |
| `TheaterStaff` role | **Survives** — single merged role (was 5 granular roles, now 1) |
| Box office (counter sale) | **Survives** — `#region BoxOffice` |
| Gate scanning | **Survives** — `#region Gate`, `AuditAction.GateScan` |
| Workforce/roster | **Survives** — `#region Workforce` |
| Incidents / seat-block / room-block | **Survives** — `#region Operations` |
| Granular roles (TheaterManager, BoxOfficeStaff, GateStaff, KitchenStaff, RegionalManager) | **Removed** — collapsed into `TheaterStaff` |
| Refund (as its own action) | **Removed** — `AuditAction` slot 4 retired, replaced by `Exchange`/`VoidSale` |
| Cash drawer close / daily close UI | **Removed** from CinemaStaff UI (backend `DrawerReconcile`/`CashPayOut` audit actions remain, so some server-side plumbing may be vestigial — verify before relying on it) |
| Customer-service desk, time-clock, tasks, checklists, inventory/storage-plan mgmt | **Removed** |
| Kitchen pickup queue | Code present, **hidden from navigation** (not deleted) |
| WorkforceController (separate controller) | **Removed** — merged into `CinemaController` |
| 4th controller for staff | **Never allowed to exist** — enforced by `ControllerArchitectureTests.OnlyThreeControllersExist` |
