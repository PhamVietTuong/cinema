# Roles & Permissions

> Purpose: Describe the current role model, capability composites, and how each is enforced in code.
> Sources: `cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RoleNames.cs`; `cinemabe/Cinema/2-Business/Cinema.Business/Managers/Auth/AuthManager.cs`; `cinemabe/Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs`; controller `[Authorize]` attributes in `CinemaController.cs`/`IdentityController.cs`/`PaymentController.cs`; `cinemafe/projects/CinemaLib/src/lib/models/roles.models.ts`
> Last reviewed: 2026-11-04

## 1. The role model

Exactly **3 roles** exist, each a `UserType.Name` value and the sole JWT role claim (`ClaimTypes.Role`):

| Role | Meaning | Theater-scoped? |
|---|---|---|
| `Admin` | Full access to everything; no theater scoping. | No |
| `TheaterStaff` | Single consolidated staff role. Absorbed what used to be 5 separate roles (box office, gate, concession, warehouse, manager) — those are now capability composites in code, not distinct UserTypes/roles. The concession/kitchen-pickup-queue composite itself was later removed along with the feature. | Yes — requires a `theaterId` JWT claim |
| `Customer` | Default role for self-registered / social-login users. | No |

There is **no permission table in the database** — `[Authorize(Roles=...)]` composites are hard-coded string constants in `RoleNames.cs`. A future reintroduction of finer-grained staff roles would only require editing that file, not every controller action.

## 2. RoleNames.cs capability composites (verified against code)

| Composite | Resolves to | Gates |
|---|---|---|
| `RoleNames.Admin` | `Admin` | Same as `Admin` literal |
| `RoleNames.Customer` | `Customer` | Not used in any `[Authorize]` attribute seen — customer-only actions use plain `[Authorize]` + ownership checks by user id instead |
| `RoleNames.TheaterStaff` | `TheaterStaff` | — |
| `RoleNames.StaffApp` | `Admin,TheaterStaff` | Login to CinemaStaff app; general staff endpoints (seat-map admin read, schedule board, incidents read/report, workforce self-service reads) |
| `RoleNames.Sellers` | `Admin,TheaterStaff` | `BoxOffice` region: `Quote`, `Sell`, `GetShowtimesToday`, `FindCustomer` |
| `RoleNames.GateKeepers` | `Admin,TheaterStaff` | `Gate` region: `Scan`, `Lookup` |
| `RoleNames.BackOffice` | `Admin,TheaterStaff` | Documented intent for warehouse/inventory staff-app access; narrowed to the caller's own theater in code — no endpoint in the current controller is gated by this constant as of this review |
| `RoleNames.StockApprovers` | `Admin` only | Documented intent: approve/reject storage plans, change inventory tracking — storage-plan/inventory management was removed from `CinemaStaff`'s current feature set (confirmed absent from the controller and the staff sidebar); no live endpoint uses this constant currently |
| `RoleNames.Approvers` | `Admin` only | `Operations` region: `BlockSeat`/`BlockRoom`/incident resolve-with-unblock require this role outright (no PIN-based alternate path); POS price overrides (`Sell`) also require it when any line carries an `OverrideUnitPrice` |
| `RoleNames.Reporting` | `Admin` only | `StaffReport` region: `GetAuditLog`, `GetSales`, `GetOccupancy`, `GetKpis` |
| `RoleNames.TheaterScopedRoles` | `TheaterStaff` | Not an authz filter — used by `AuthManager.ResolveTheaterIdAsync` to decide which `UserType`s *require* a non-null `TheaterId` on create/update |

**Since `TheaterStaff` is the only non-Admin staff value today, every composite other than the three Admin-only ones (`StockApprovers`, `Approvers`, `Reporting`) is behaviorally identical to `StaffApp` (`Admin,TheaterStaff`)** — there is no way today to grant a staff account gate-scanning access without also granting it box-office access.

## 3. What each role can / cannot do

### Admin
- Everything: all catalog/theater/pricing/promotion CRUD, user management, invoice admin (list/status-override/delete), all staff-area actions (reporting, approvers), audit log.
- Only role that can access `RoleNames.StockApprovers`/`Approvers`/`Reporting`-gated actions.
- Not theater-scoped — can act across all theaters.

### TheaterStaff
- Box office counter sale (`Quote`/`Sell`), gate ticket scan/lookup, schedule board, incident reporting (not resolving with unblock, which needs `Approvers`), seat-map read (`GetRoomSeatMap`).
- Cannot: block/unblock a seat or room, apply a POS price override, run staff reports (`StaffReport` region is `Admin`-only despite the staff-sounding name), write any catalog/pricing/theater/movie data (all gated `_adminRole`="Admin" literal, not `RoleNames.TheaterStaff`).
- Always theater-scoped — JWT carries exactly one `theaterId` claim; `TryGetBackOfficeScope`/`TryGetStaffScope` pin the caller to that theater, 403 if attempting cross-theater access.

### Customer
- Browse movies/theaters/showtimes (all public/anonymous reads), book seats, pay, manage own profile/notification prefs/2FA, rate/comment on watched movies, view own invoices/tickets.
- Cannot access any Admin- or staff-gated action. No ownership bypass — `InitiatePayment`/`ConfirmPayment`/`CancelBooking`/`GetMyInvoices` all filter by the caller's own `userId` from the JWT.

## 4. Enforcement mechanics

- **JWT claim**: `ClaimTypes.Role` = `user.UserType?.Name ?? "Customer"` — exactly one role per token (`JwtTokenService.GenerateToken`).
- **`theaterId` claim**: present only when `user.TheaterId` is set (i.e. `TheaterStaff` accounts). Absent for `Admin`/`Customer`.
- **Scope helpers** (`ClaimsPrincipalExtensions`): `TryGetBackOfficeScope` / `TryGetStaffScope` — Admin gets `isAdmin:true` (unrestricted); a theater-scoped role gets pinned to its `theaterId` claim(s), failing (403, not 401) if the claim is missing or the role doesn't qualify.
- **403 vs 401 convention (deliberate)**: an authenticated user hitting an endpoint outside their role/theater scope gets **403** (`AccessDeniedException` → `ApiControllerBase.HandleException`, or the framework's own 403 for a plain `[Authorize(Roles=...)]` mismatch). **401 is reserved for "no valid token at all."** This matters because the frontend's `errorInterceptor` force-logs-out the user on any 401 — a role/scope mismatch must not trigger that.
- **User creation/update** (`AuthManager.ResolveTheaterIdAsync`): any `UserType` in `RoleNames.TheaterScopedRoles` (today, only `TheaterStaff`) requires a valid, existing `TheaterId` → 400 otherwise. Non-scoped roles (`Admin`, `Customer`) force `TheaterId = null` regardless of what the request supplied.
- **Frontend mirror** (`cinemafe/projects/CinemaLib/src/lib/models/roles.models.ts`): mirrors `RoleNames.cs` 1:1 — `UserRoles.{Admin,Customer,TheaterStaff}` plus the same named composite arrays. Guards: `authGuard` (any authenticated user), `adminGuard` (Admin only, redirects to `/forbidden`, deliberately outside the guarded route tree to avoid redirect loops), `roleGuard(roles)` (factory for arbitrary role lists).

## 5. Verified discrepancy

`PaymentController.ValidateTicket` is gated `[Authorize(Roles="Admin")]` (the controller's local `_adminRole` constant), **not** `RoleNames.GateKeepers` — meaning a `TheaterStaff` account cannot call it, even though `CinemaController`'s `Gate/Scan` action (apparently the staff-facing gate-scan flow) is gated `RoleNames.GateKeepers` (`Admin,TheaterStaff`). Confirm with the team whether these are two distinct features (an admin manual ticket-validation tool vs. the staff gate terminal) or whether `ValidateTicket`'s role gate is a bug. <!-- TODO: confirm with team -->
