# API Specification

> Purpose: Enumerate every endpoint on the three backend controllers, with route, auth, request/response shape, and notable error cases.
> Sources: `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/{CinemaController,IdentityController,PaymentController,ApiControllerBase}.cs`, `cinemabe/Cinema/2-Business/Cinema.Business/Managers/BookingManager.cs`, `cinemabe/Cinema/2-Business/Cinema.Business/Managers/Auth/AuthManager.cs`, `cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RoleNames.cs`
> Last reviewed: 2026-11-04

## Conventions (verified against code)

- Every controller is `[ApiController]`, `[Route("api/[controller]/[action]")]`. All three controllers (`CinemaController`, `IdentityController`, `PaymentController`) still exist — no 4th controller (`ControllerArchitectureTests.OnlyThreeControllersExist`).
- List/search endpoints are `[HttpPost]` taking `PagingSearchDTO { PageIndex, PageSize, Filters: Dictionary<string,string>, Sort: SortDTO{Field,Ascending} }` in the body, returning `DefaultSearchResults<T>`, even though they're reads.
- `PaymentController` has a **class-level `[Authorize]`** — any action without its own `[AllowAnonymous]`/`[Authorize(Roles=...)]` still requires a valid JWT. `IdentityController` and `CinemaController` have no class-level auth attribute — an action with no attribute is public.
- Standard error mapping (`ApiControllerBase.HandleException`): `KeyNotFoundException`→404, `AccessDeniedException`→**403** (never 401 — the frontend force-logs-out on 401), `UnauthorizedAccessException`→401, `InvalidOperationException`→400, anything else→500. Body shape: `{ error, statusCode }`. A plain `[Authorize(Roles=...)]` pipeline rejection (no controller code reached) returns ASP.NET's framework default (401 no token / 403 wrong role) with an empty body.
- `CinemaController`'s one-line CRUD actions use `Run`/`RunNoContent`/`EnsureExistsAsync` helpers (`#region Catalog helpers`) that wrap the same try/catch + `LogProvider` + 404 pattern as the more verbose hand-written actions.
- `_adminRole` is a local `"Admin"` string constant in each controller (not `RoleNames.Admin`, though equivalent) — used for most catalog/admin writes. `RoleNames.*` composites are used for staff-area endpoints only (Gate/BoxOffice/Concession/Operations/StaffReport).

---

## 1. IdentityController (`/swagger/identity`) — `api/Identity/*`

No class-level `[Authorize]`; actions default to anonymous unless tagged.

### Auth (`#region`-less, banner `// ── Auth ──`)

| Action | Route verb | Auth | Rate limit | Request (key fields) | Response | Notable errors |
|---|---|---|---|---|---|---|
| `Login` | POST | anonymous | `Auth` (10/min/IP) | `LoginRequest{EmailOrPhone, Password}` | `AuthResponse{Token, ExpiresAt, RequiresTwoFactor, User}` — `Token=""` if 2FA required | 401 invalid credentials / locked out / unconfirmed email |
| `Register` | POST | anonymous | `Auth` | `RegisterRequest{Name, Email, Phone, Password, ConfirmPassword}` | `AuthResponse` with usable JWT immediately (even if unconfirmed) | 400 email/phone in use, missing `Customer` UserType |
| `ForgotPassword` | POST | anonymous | `AuthEmail` (3/15min/IP) | `ForgotPasswordRequest{Email}` | 204 always (no enumeration) | — |
| `ResetPassword` | POST | anonymous | `Auth` | `ResetPasswordRequest{Email, Token, NewPassword}` | 200 | 400 invalid/expired token |
| `ConfirmEmail` | POST | anonymous | `AuthEmail` | `ConfirmEmailRequest{Email, Token}` | 200 | 400 invalid/expired token |
| `ResendVerification` | POST | anonymous | `AuthEmail` | `ResendVerificationRequest{Email}` | 204 always (no enumeration) | — |
| `GoogleLogin` | POST | anonymous | `Auth` | `GoogleLoginRequest{IdToken}` | `AuthResponse` (2FA always bypassed) | 401 invalid token / email not verified; 400 not configured |
| `FacebookLogin` | POST | anonymous | `Auth` | `FacebookLoginRequest{AccessToken}` | `AuthResponse` (2FA always bypassed) | 401 invalid token (same message whether bad token or missing email permission) |
| `VerifyTwoFactor` | POST | anonymous | `Auth` | `VerifyTwoFactorRequest{EmailOrPhone, Code}` (`Code` must match `^\d{6}$`) | `AuthResponse` with full JWT | 401 invalid/expired code |

### Profile (banner `// ── Profile ──`)

| Action | Verb | Auth | Request | Response | Notes |
|---|---|---|---|---|---|
| `GetProfile` | GET | `[Authorize]` | — | `UserDTO` | any authenticated role |
| `UpdateProfile` | PUT | `[Authorize]` | `UpdateProfileRequest` | `UserDTO` | |
| `UpdateNotificationPreferences` | POST | `[Authorize]` | `UpdateNotificationPreferencesRequest` | 200 | per-channel opt in/out (distinct from admin `Template` CRUD) |
| `ChangePassword` | POST | `[Authorize]` | `ChangePasswordRequest` | 200 | |
| `SetTwoFactor` | POST | `[Authorize]` | `SetTwoFactorRequest{Enabled}` | 200 | disabling clears any outstanding code |

### Admin (banner `// ── Admin ──`, `[Authorize(Roles="Admin")]` local const)

| Action | Verb | Auth | Request | Response |
|---|---|---|---|---|
| `GetUsers` | POST | Admin | `PagingSearchDTO` | `DefaultSearchResults<UserDTO>` — `UserDTO.TheaterName` is batch-resolved from `TheaterId` (null if unset or stale); `Filters["theaterId"]` scopes results to one theater (e.g. a cinema's Staff tab) |
| `CreateUser` | POST | Admin | `CreateUserRequest` | `UserDTO` — theater-scoped roles require valid `TheaterId` (400 otherwise) |
| `UpdateUser` | POST | Admin | `UpdateUserRequest` | `UserDTO` |
| `DeleteUser` | POST | Admin | `[FromQuery] Guid id` | 200/204 |

---

## 2. PaymentController (`/swagger/payment`) — `api/Payment/*`

Class-level `[Authorize]` — every action requires a JWT unless marked `[AllowAnonymous]`.

### Booking (banner `// ── Booking ──`)

| Action | Verb | Auth | Request (key fields) | Response | Notable errors |
|---|---|---|---|---|---|
| `GetSeats` | POST | `[AllowAnonymous]` | `PagingSearchDTO` (filters: showTimeId, roomId) | `DefaultSearchResults<SeatDTO>` — `Status` Available/Reserved/Occupied | |
| `GetShowTimePrices` | POST | `[AllowAnonymous]` | `showTimeId`, `roomId` (query) | per-PatronCategory resolved price list | |
| `CreateBooking` | POST | `[Authorize]` (owner = caller) | `CreateBookingRequest{ShowTimeId, RoomId, Seats[], ConnectionId?, DiscountCode?, GiftCardCode?, PointsToRedeem?, Foods[], DryRun?}` | invoice/pricing result | 400 with one of the exact strings: `"Seat {id} is already booked."`, `"Seat {id} is being held by another user."` (only checked when `ConnectionId` is supplied), `"A double seat must be booked together with its partner."`, `"A patron category is required for every seat."` / `"Seat {id} is not available for the selected patron category."`, `"Invalid or expired gift card."`, `"Discount code is invalid or no longer available."`; a DB unique-index race surfaces as `"One or more selected seats were just booked by someone else."` |
| `ConfirmPayment` | POST | `[Authorize]` (owner) | `ConfirmPaymentRequest{InvoiceId, PaymentReference}` | bool/invoice result | only succeeds for Sandbox gateway — VNPay/MoMo/Stripe always return false (callback-authoritative) |
| `CancelBooking` | POST | `[Authorize]` (owner) | `CancelBookingRequest{InvoiceId}` | bool | only a `Pending` invoice owned by caller; no-op otherwise |
| `InitiatePayment` | POST | `[Authorize]` (owner) | `InitiatePaymentRequest{InvoiceId, Provider?}` | `PaymentInitiationDTO{Provider, PaymentReference, RedirectUrl?}` | `FinalAmount<=0` (gift card/points fully cover) finalizes immediately, no gateway call, `Provider="None"`, `PaymentReference="GIFTCARD-FULL"` |
| `PaymentCallback` | GET/POST | `[AllowAnonymous]` | `provider` query + gateway-specific query/form/raw-body fields | VNPay: `{"RspCode","Message"}`; others: generic `{message}`/`{error}` | signature-verified per gateway; idempotent; amount cross-checked; `[AllowAnonymous]` by design (called by the gateway's servers) |
| `ValidateDiscountCode` | POST | `[Authorize]` | `ValidateDiscountCodeRequest` | non-throwing usability/amount result | |
| `ValidateGiftCard` | POST | `[Authorize]` | `ValidateGiftCardRequest{Code}` | `GiftCardValidationDTO{Valid, Message, Balance}` | non-throwing |

### Gift cards (banner `// ── Gift cards ──`, Admin-only)

| Action | Verb | Auth | Request | Response |
|---|---|---|---|---|
| `GetGiftCards` | POST | Admin | `PagingSearchDTO` | `DefaultSearchResults<GiftCardDTO>` |
| `IssueGiftCard` | POST | Admin | `IssueGiftCardRequest{Amount, ExpiresAt?, IssuedToEmail?}` | `GiftCardDTO` — `Code` auto-generated `"GC-"+12hex` |
| `SetGiftCardActive` | POST | Admin | `SetGiftCardActiveRequest{Id, IsActive}` | 200 |
| `ValidateTicket` | POST | Admin | `ValidateTicketRequest{QrCode or similar}` | ticket validation result | requires `Invoice.Status==Paid`, rejects already-used ticket; this is the gate check-in endpoint but gated plain `Admin`, not `RoleNames.GateKeepers` — cross-check against `Gate/Scan` in `CinemaController` which IS staff-accessible (see discrepancy note below) |

### Invoices (banner `// ── Invoices ──`)

| Action | Verb | Auth | Request | Response |
|---|---|---|---|---|
| `GetMyInvoices` | POST | `[Authorize]` (self) | `PagingSearchDTO` | `DefaultSearchResults<InvoiceDTO>` scoped to caller |
| `GetInvoice` | GET | `[Authorize]` (self) | `[FromQuery] Guid id` | `InvoiceDTO` |
| `GetInvoices` | POST | Admin | `PagingSearchDTO` | `DefaultSearchResults<InvoiceAdminDTO>` — admin-wide list (separate from `CinemaController`'s `Invoice (admin)` region, which also exposes status update/delete) |
| `GetRevenue` | GET | Admin | `from`, `to` (query) | revenue summary |
| `GetRevenueByDay` | GET | Admin | `days` (query, default 7) | |
| `GetRevenueByMovie` | GET | Admin | `from`, `to` | |
| `GetRevenueByTheater` | GET | Admin | `from`, `to` | |

<!-- TODO: confirm with team — PaymentController has its own GetRevenue*/GetInvoices(Admin) family that overlaps with CinemaController's StaffReport (GetSales/GetOccupancy/GetKpis) and Invoice-admin regions; not yet resolved whether one is canonical. -->

---

## 3. CinemaController (`/swagger/cinema`) — `api/Cinema/*`

2549 lines, organized by `#region`. Reads are public unless noted; writes are `[Authorize(Roles="Admin")]` (local `_adminRole` const) unless a `RoleNames.*` composite is shown.

### Movies / Theaters / ShowTimes (no `#region`, top of file)

| Action | Verb | Auth | Request | Response |
|---|---|---|---|---|
| `UploadImage` | POST | Admin | `IFormFile` (jpg/png/webp/gif, ≤5MB) | `UploadResultDTO{Url}` |
| `GetRecommendedMovies` | POST | anonymous | `count` query (default 8) | `List<MovieDTO>` — personalized if signed in |
| `GetMovies` | POST | anonymous | `PagingSearchDTO` | `DefaultSearchResults<MovieDTO>` |
| `GetNowShowingMovies` | POST | anonymous | `PagingSearchDTO` | same — `IsNowShowing` filter |
| `GetComingSoonMovies` | POST | anonymous | `PagingSearchDTO` | same — `IsComingSoon` filter |
| `GetMovie` | GET | anonymous | `id` query | `MovieDetailDTO` (personalized `CanReview` if signed in) |
| `GetMovieBySlug` | GET | anonymous | `slug` query | `MovieDetailDTO` |
| `CreateMovie` | POST | Admin | `CreateMovieRequest` | `MovieDetailDTO` |
| `UpdateMovie` | PUT | Admin | `UpdateMovieRequest` | `MovieDetailDTO` (slug never regenerated) |
| `DeleteMovie` | DELETE | Admin | `id` query | 204 — soft delete (`IsActive=false`) |
| `AddComment` | POST | `[Authorize]` | `movieId` query + `AddCommentRequest{Content, ParentId?}` | `CommentDTO` | gated by `EnsureCanReviewAsync` (must have watched the movie) |
| `RateMovie` | POST | `[Authorize]` | `movieId` query + `RateMovieRequest{Score, Review?}` | 204 | same watch-gate |
| `GetTheaters` | POST | anonymous | `PagingSearchDTO` | `DefaultSearchResults<TheaterDTO>` |
| `GetTheater` | GET | anonymous | `id` query | `TheaterDTO` |
| `GetTheatersByMovie` | POST | anonymous | `PagingSearchDTO` | theaters currently screening a movie |
| `CreateTheater` / `UpdateTheater` / `DeleteTheater` | POST/PUT/DELETE | Admin | `Create/UpdateTheaterRequest` / `id` | `TheaterDTO` / 204 |
| `GetShowTimes` | POST | anonymous | `PagingSearchDTO` | `DefaultSearchResults<ShowTimeListDTO>` |

### Comment moderation (admin, inline before numbered regions)

| Action | Verb | Auth | Request | Response |
|---|---|---|---|---|
| `GetCommentsForModeration` | POST | Admin | `PagingSearchDTO` | `DefaultSearchResults<CommentModerationDTO>` |
| `ModerateComment` | POST | Admin | `ModerateCommentRequest{CommentId, Approved}` | `{message}` / 404 |
| `DeleteComment` | POST | Admin | `DeleteCommentRequest{CommentId}` | `{message}` / 404 — cascades to direct replies |

### Catalog lookup CRUD regions (list is anonymous, Get-by-id anonymous, Create/Update/Delete Admin — unless noted)

| Region | List/Get | Write | Notable actions beyond basic CRUD |
|---|---|---|---|
| `AgeRestriction` | anonymous | Admin | — |
| `DiscountType` | anonymous | Admin | — |
| `MovieType` | anonymous | Admin | — |
| `Template` | **Admin-only even to list/get** | Admin | notification-template CRUD (booking-confirmation content) |
| `SeatType` | anonymous | Admin (Update only) | no Create/Delete — seeded 2 rows/theater (Standard+Double) at theater creation |
| `PatronCategory` | anonymous (+`GetPatronCategoriesByTheater`) | Admin | |
| `TimeSlot` | anonymous | Admin | |
| `TicketPrice` | anonymous | Admin | + `UpsertTicketPrices` (batch, one multiplier across several days) |
| `UserType` | **Admin-only even to list/get** | Admin | |
| `MemberShip` | anonymous | Admin | |
| `Holiday` | anonymous | Admin | |
| `News` | anonymous | Admin | |
| `Person` | **Admin-only** for CRUD; `GetPeopleByNames` is `[AllowAnonymous]` | Admin | |
| `Discount` | anonymous | Admin | |
| `PromotionProgram` | `GetPublishedPromotionPrograms` `[AllowAnonymous]`; `GetPromotionPrograms`/`GetPromotionProgram` Admin | Admin | |
| `SplashBanner` | `GetActiveSplashBanner` `[AllowAnonymous]` (returns `List<SplashBannerDTO>`, 0 or 1 items); rest Admin | Admin | |
| `FoodAndDrink` | anonymous | Admin | |
| `Room` | anonymous | Admin | |
| `RoomType` | anonymous | Admin | + `GetRoomTypePatronCategoryPrices` / `SaveRoomTypePatronCategoryPrices` (Admin) |
| `ShowTime` | anonymous list (`GetShowTimeList`); **`GetShowTime`/Update/Delete are Admin-only** | Admin | |
| `MovieTypeDetail` | anonymous list | Admin (Create/Delete) | Movie↔MovieType join |
| `Room seat map` | `GetRoomSeatMap` = `RoleNames.StaffApp` | Admin | `SaveRoomSeatMap`, `ResizeRoomSeatGrid`, `RemoveRoomSeat` |
| `Invoice (admin)` | Admin | Admin | `GetInvoices`, `UpdateInvoiceStatus` (no enum allow-list — can write reserved value 4), `DeleteInvoice` (restores food stock if was Pending) |
| `Loyalty` | `GetEffectiveLoyaltyRate` anonymous; rest Admin | Admin | `SaveLoyaltySetting`, `ClearTheaterLoyaltyOverride` |
| `Combo` | `GetComboComponents` `[AllowAnonymous]` | `RoleNames.Admin` | `SaveComboComposition` |

### Staff-area regions (gated by `RoleNames.*` composites, not `_adminRole`)

| Region | Role gate | Actions |
|---|---|---|
| `Concession` | `RoleNames.Concession` (`Admin,TheaterStaff`) | `GetPickupQueue`, `SetFoodStatus`, `GetLowStock`, `LookupPickup` |
| `Gate` | `RoleNames.GateKeepers` (`Admin,TheaterStaff`) | `Scan` (QR scan — a refused scan is still HTTP 200 with `Outcome` field; only auth failures are 403), `Lookup` (find today's paid tickets by invoice code/phone) |
| `BoxOffice` | `RoleNames.Sellers` (`Admin,TheaterStaff`) | `Quote` (dry-run price), `Sell` (creates `Paid` invoice directly), `GetShowtimesToday`, `FindCustomer` (by exact phone) |
| `StaffReport` | `RoleNames.Reporting` (`Admin` only) | `GetAuditLog`, `GetSales`, `GetOccupancy`, `GetKpis` — date range capped at 92 days, theater-scoped |
| `Operations` | `RoleNames.StaffApp` (`Admin,TheaterStaff`); `BlockSeat`/`BlockRoom` are `RoleNames.Approvers` (`Admin`) only | `GetScheduleBoard`, `ReportIncident`, `GetIncidents`, `GetIncident`, `ResolveIncident`, `BlockSeat`, `BlockRoom` (`BlockSeat`/`BlockRoom` require the `Approvers` role outright — 403 otherwise, no PIN-based alternate path; they set `Incident.BlocksSeat`/`BlocksRoom`, and `ResolveIncident`'s optional `Unblock` flag, which also needs `Approvers`, reopens the seat/room) |

---

## 4. SignalR hubs (not controllers, but part of the API surface)

| Hub | Route | Auth | Notes |
|---|---|---|---|
| `BookingHub` | `/hubs/booking` | `[Authorize]` | `JoinRoom`, `LockSeat`, `UnlockSeat` (client→hub); `SeatLockFailed` (caller-only), `SeatLocked`/`SeatUnlocked`/`SeatBooked` (group broadcast) |
| `StaffHub` | `/hubs/staff` | `[Authorize(Roles=RoleNames.StaffApp)]` | `FoodOrderQueued`, `FoodOrderUpdated`, `StockLow`, `IncidentRaised`; `JoinTheater(theaterId)` |

JWT for both is read from the `access_token` query string for any path starting with `/hubs` (configured in `Program.cs`), not the `Authorization` header.

---

## Discrepancies / things to verify further

- `PaymentController.ValidateTicket` is gated plain `[Authorize(Roles="Admin")]` (local const), while `CinemaController`'s `Gate/Scan` region (the apparent gate-scan flow used by staff) is gated `RoleNames.GateKeepers` (`Admin,TheaterStaff`). If `TheaterStaff` is meant to do gate check-in in practice (the CinemaStaff app's gate-scan UI calls `Gate/Scan`, not `PaymentController.ValidateTicket`), confirm whether the two are the same logical operation exposed twice with different role gates, or two distinct code paths (admin manual override vs. staff gate terminal). <!-- TODO: confirm with team -->
- `PaymentController` has its own `GetInvoices`(Admin)/`GetRevenue*` family that overlaps in purpose with `CinemaController`'s `Invoice (admin)` region and `StaffReport.GetSales`. Not necessarily a bug (different DTOs/scopes), but worth a deliberate check before assuming one is canonical. <!-- TODO: confirm with team -->
