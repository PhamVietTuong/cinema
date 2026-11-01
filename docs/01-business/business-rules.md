# Business Rules

> Purpose: Scannable index of validations, calculations, state machines, limits, and allowed/blocked conditions, with pointers to full detail.
> Sources: `business/domain-model.md`, `business/auth.md`, `business/booking.md`, `business/payments.md`, `business/catalog.md`, `business/admin-staff-and-frontend-flows.md`
> Last reviewed: 2026-11-01

Each row: **Rule → Description → Enforced at (file:function) → Full detail in business/**. This file is a summary — read the cited `business/*.md` section before changing any of these.

## 1. Seat lock / booking concurrency

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| Advisory seat lock is process-local | `_lockedSeats`/`_bookingGates` are static in-memory `ConcurrentDictionary`s in `BookingManager` — not distributed, lost on restart, not shared across instances | `BookingManager.cs:28,33` | `booking.md` §1 |
| Lock duration = 5 min, lazy expiry | Checked only on next `IsSeatLocked` read, not swept by a timer | `BookingManager.IsSeatLocked` (`:1949`) | `booking.md` §1.2 |
| Pending-invoice hold = 15 min | Independent DB-backed timer; reaper runs every 1 min | `PendingBookingReaper` | `booking.md` §1.2 |
| DB-level anti-double-booking | Unique filtered index on `InvoiceTicket(ShowTimeId,RoomId,SeatId) WHERE IsActive=1` — the only cross-instance guarantee | `create_db.sql`/`upgrade_db.sql`; `InvoiceTicket` config | `domain-model.md` §5, `booking.md` §1.4 |
| In-process booking gate | `SemaphoreSlim(1,1)` per `(showTimeId,roomId)`, serializes the read-validate-insert sequence within one process | `BookingManager.BookingGate` | `booking.md` §1.4 |
| Double (paired) seats booked together | All seats sharing a `SeatGroupId` must be requested together | `BookingManager.cs:468` | `booking.md` §1.4 |
| Advisory lock check is opt-in | Only enforced if caller supplies `ConnectionId` | `ComposeInvoiceAsync` | `booking.md` §1.4, §6.4 |

## 2. Booking → payment → ticket lifecycle

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| State machine | Available → Locked (advisory) → Booked(Pending) → Booked(Paid) → Used; or → Available on unlock/cancel/expiry | `BookingManager`, `BookingHub` | `booking.md` §1.1, §2 |
| Online booking starts Pending | `CreateBookingAsync` creates `Invoice{Status=Pending, Channel=Online}` | `BookingManager.CreateBookingAsync` (~line 144) | `booking.md` §2 step 9 |
| Counter sale is Paid at creation | `SellAtCounterAsync` never passes through Pending | `BookingManager.SellAtCounterAsync` (~line 246) | `payments.md` §1.2 |
| Only Pending can be paid/cancelled | `InitiatePayment`/`ConfirmPayment`/`CancelBooking` require `Status==Pending` and ownership; no-op otherwise | `BookingManager` | `payments.md` §1.2 |
| Stale-Pending auto-expiry | `ExpireStalePendingBookingsAsync(15m)`, runs every 1 min, same side effects as user cancel | `PendingBookingReaper` | `booking.md` §1.2, `payments.md` §1.4 |
| Cancel/expire side effects | Deactivate tickets, restore points, restore gift-card balance, restore food stock, all in one transaction | `BookingManager.CancelBookingAsync` | `payments.md` §1.4 |
| Paid side effects | Stamp `PaidAt`, add `InvoicePayment`, accrue loyalty points, re-evaluate membership tier, mark promo code used, queue food order, send confirmation | `BookingManager.FinalizePaidInvoiceAsync` | `payments.md` §1.3 |
| Gate check-in | Requires `Status==Paid`, rejects already-used ticket, sets `IsUsed=true` | `PaymentController.ValidateTicket` / `CinemaController.Gate.Scan` | `booking.md` §2 step 13 |
| No refund flow exists | `InvoiceStatus` value 4 ("was Refunded") is reserved, never reused; no refund endpoint/entity/gateway-call anywhere in current code | n/a (absence) | `payments.md` §6, `domain-model.md` §5 |
| `InvoiceStatus.Failed` is dead code | Defined (value 3) but no code path currently sets it | `InvoiceStatus` enum | `payments.md` §1.1, §9.4 |
| Admin status override is unconstrained | `InvoiceAdminManager.UpdateStatusAsync` has no enum allow-list — could theoretically write the reserved value 4 | `InvoiceAdminManager.UpdateStatusAsync` | `payments.md` §1.1 |

## 3. Payments — VNPay/MoMo/Stripe callback authority

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| **Callback-authoritative, not redirect-authoritative** | Real gateways' `VerifyPaymentAsync` (synchronous browser-confirm path) is hardcoded to fail; only the signed server-to-server callback can mark `Paid` | `VnPayGateway`/`MoMoGateway`/`StripeGateway.VerifyPaymentAsync` | `payments.md` §3 (CRITICAL) |
| VNPay IPN response contract | Exact JSON `{"RspCode","Message"}`; wrong shape/status → VNPay retries up to 50 min (10×, 5 min apart) | `VnPayGateway.BuildIpnResponse` | `payments.md` §4 |
| VNPay amount is ×100 | `vnp_Amount` = VND × 100; divided by 100 before compare | `VnPayGateway` | `payments.md` §4, §9.8 |
| Callback idempotency | Already-`Paid` invoice → `AlreadyConfirmed`, no re-run of side effects | `BookingManager.HandlePaymentCallbackAsync` | `payments.md` §2.3, §8 |
| Amount cross-check | `ReceivedAmount` vs `invoice.FinalAmount`, both rounded to whole VND the same way as the gateway's own rounding | `BookingManager.HandlePaymentCallbackAsync` | `payments.md` §8 |
| `PaymentCallback` is `[AllowAnonymous]` | Trust comes entirely from per-gateway signature verification in `ParseCallback`, not ASP.NET auth | `PaymentController.PaymentCallback` | `payments.md` §9.7 |
| Zero-amount checkout skips gateway | Gift card/points fully covering the price finalizes `Paid` immediately with `PaymentReference="GIFTCARD-FULL"`, no gateway call | `BookingManager.InitiatePaymentAsync` | `payments.md` §9.5 |

## 4. Pricing formula

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| **Pricing formula** | `price = round(roomTypePrice(category, roomType) * priceFactor + threeDSurcharge + showTimeRoom.BasePrice, 2)` | `BookingManager.BuildSeatPricingContextAsync` | `catalog.md` §5 |
| Category has no fallback price | `RoomTypePatronCategoryPrice` is the only price read at booking time — no row = category unbookable in that RoomType; `PatronCategory.Price` is seed-only | same | `catalog.md` §3, §5, §11.2 |
| Holiday overrides weekday matrix, never stacks | If the date is a `Holiday`, `TicketPrice`/`TimeSlot` lookup is skipped entirely | `BuildSeatPricingContextAsync` | `catalog.md` §5, §11.3 |
| 3D surcharge & per-showtime BasePrice are flat adds | Applied after the multiplier, immune to holiday/weekday surge | same | `catalog.md` §5, §11.4 |
| Seat-kind gating (2 layers) | (1) `PatronCategory` row must exist per `(category, SeatKind)` at the theater; (2) `RoomTypePatronCategoryPrice` row must exist per `(RoomType, category)` — both by omission, no allow-list table | `PatronCategoryManager`, `BuildSeatPricingContextAsync` | `catalog.md` §3 |
| ShowTime scheduling validation chain | Window validity → movie runtime fit → movie availability (active, release/end date) → room availability/status → 3D format support → room-overlap with turnover buffer | `ShowTimeManager` (`ValidateWindow`→`HasRoomOverlapAsync`) | `catalog.md` §4 |

## 5. Promotions / loyalty / gift cards

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| Two promotion mechanisms, never combined | Older `Discount` (code/auto-apply, %) vs. newer `PromotionProgram` (FixedPrice/PercentOff, schedule-scoped); pricing computes both scenarios and picks the cheaper | `BookingManager.ComputePricingAsync` | `catalog.md` §6, §11.8 |
| `AutoApply` restrictions | Only `Everyone`/`Members` eligibility can auto-apply; `Info` mechanic never auto-applies; `AutoApply` + `LinkedDiscountId` mutually exclusive | `PromotionProgramManager.ValidateAsync` | `catalog.md` §6 |
| `StacksWithMembershipDiscount` | Lets a PromotionProgram additionally apply the membership % on top of itself — never combined with an actual `Discount` row | `BookingManager.ComputePricingAsync` | `catalog.md` §6 |
| Loyalty points earned | `pointsEarned = (int)(FinalAmount / PointsPerUnit)` (integer division, post-discount amount); membership tier re-evaluated to highest qualifying tier, no downgrade logic | `BookingManager.ApplyPaidSideEffectsAsync` | `catalog.md` §8 |
| Loyalty points redeemed | `pointsRedeemed = min(requested, user.Points, floor(amountSoFar/PointValueVnd))`; reserved immediately, restored on cancel/expire | `BookingManager` (booking-time) | `catalog.md` §8 |
| Points/value rates are independent | `PointsPerUnit` (earn rate) and `PointValueVnd` (redeem rate) are not required to be inverses | `LoyaltySetting`/`LoyaltyRates` | `catalog.md` §8, §11.12 |
| Gift card redemption | `min(balance, finalAmount)`, floored; decremented immediately (reserved), restored on cancel/expire only — **no restore path from Paid** (no refund feature) | `BookingManager` | `catalog.md` §8, `payments.md` §7 |
| Gift card validation vs. redemption | `ValidateAsync` returns a soft DTO (never throws); the actual charge path throws `InvalidOperationException("Invalid or expired gift card.")` on a bad code | `GiftCardManager` / `BookingManager` | `catalog.md` §8 |

## 6. Auth / account rules

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| Account lockout | 5 consecutive failed logins → 15-minute lockout; resets on success | `AuthManager.LoginAsync` | `auth.md` §1.1 |
| Password hashing | PBKDF2-SHA256 (100k iter); legacy single-round HMAC-SHA512 transparently verified then rehashed | `AuthManager`/`PasswordHasher` | `auth.md` §1.1 |
| Email confirmation gates password login only | Checked after password verify (no enumeration); registration still issues a usable JWT immediately | `AuthManager.LoginAsync`/`RegisterAsync` | `auth.md` §1.1, §2 |
| Social login bypasses 2FA | Google/Facebook always call `BuildAuthResponse` directly, never check `TwoFactorEnabled` | `AuthManager` | `auth.md` §1.2/1.3, §6.3 |
| Account linking by email | Google/Facebook: no match → auto-create Customer; match unconfirmed → auto-confirm; match confirmed → login as-is (whatever role) | `AuthManager` | `auth.md` §1.2/1.3 |
| 2FA code | 6-digit, SHA-256-hashed, 5-min expiry, single-use | `AuthManager` | `auth.md` §6.3 |
| JWT has no refresh/revocation | Fixed expiry (`JWT:ExpiryHours`, default 24h); client must re-login after expiry; `jti` is informational only | `JwtTokenService` | `auth.md` §3 |
| 403 vs 401 convention | Role/scope mismatch on an authenticated token → 403 always (never 401, which triggers frontend force-logout) | `ApiControllerBase.HandleException`, `ClaimsPrincipalExtensions` | `auth.md` §4, §8 |

## 7. Append-only enums / schema invariants

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| Enums are append-only, never renumber | `AuditAction`, `FoodOrderStatus`, `IncidentCategory/Severity/Status`, `InvoiceStatus`, `PaymentTender`, `SalesChannel`, `SalesGroupBy`, `ScanOutcome`, `StockMovementType/ReasonCode` are all persisted as `int` | entity/enum definitions | `domain-model.md` §9, §10 |
| Retired ordinals, never reuse | `InvoiceStatus` value 4 (was `Refunded`); `AuditAction` values 4 (was `Refund`) and 16 (was `UserTheatersChanged`) | enum source comments | `domain-model.md` §10, `admin-staff-and-frontend-flows.md` §A.2 |
| Single shared `AuditLog` table | One insert-only table, no FKs (survives deletion of referenced rows), discriminated by `AuditAction`; never create a per-feature log table | `AuditLog` entity, `AuditLogger` | `admin-staff-and-frontend-flows.md` §A.2 |
| `datetime` not `datetime2` | `CinemaContext.OnModelCreating` remaps every `DateTime`/`DateTime?` globally | `CinemaContext` | `domain-model.md` §10 |
| Schema via hand-edited SQL, not EF migrations | `create_db.sql`/`upgrade_db.sql` must stay in sync; both end with the baseline stamp | n/a | root `CLAUDE.md`, `catalog.md` §7 |

## 8. Operational / staff rules

| Rule | Description | Enforced at | business/ ref |
|---|---|---|---|
| Block seat/room requires Approver or manager PIN | `BlockSeat`/`BlockRoom`/incident-resolve-with-unblock — 403 otherwise | `CinemaController.Operations` | `admin-staff-and-frontend-flows.md` §A.1 |
| Staff reports capped at 92 days, theater-scoped | `GetSales`/`GetOccupancy`/`GetKpis` reject out-of-scope theater with 403 | `CinemaController.StaffReport` | `admin-staff-and-frontend-flows.md` §A.1 |
| CSV export is client-side only | No backend file-generation endpoint — Admin Reports page builds CSV from the JSON DTOs | `CinemaAdmin reports.component.ts` | `admin-staff-and-frontend-flows.md` §A.4 |
| Notification templates vs. preferences are distinct features | Templates = admin-authored message content (`Template` region, Admin CRUD); preferences = per-user channel opt-in/out (`IdentityController.UpdateNotificationPreferences`) | both | `admin-staff-and-frontend-flows.md` §A.5 |
| Splash banner "active" rule | `IsPublished` AND current Vietnam-local date within `[StartDate,EndDate]`; ties broken by latest `StartDate`, then `CreationTime`, then `Id` (desc) | `SplashBannerManager` | `catalog.md` §7 |

## 9. Known discrepancies surfaced during this review

- `PaymentController.ValidateTicket` is `Admin`-only while `CinemaController.Gate.Scan` (apparent staff gate-scan flow) is `RoleNames.GateKeepers` (`Admin,TheaterStaff`) — see `docs/01-business/roles-permissions.md` §5. <!-- TODO: confirm with team -->
- `RoleNames.BackOffice` and `RoleNames.StockApprovers` are defined but no live `[Authorize]` attribute in the current `CinemaController`/`PaymentController`/`IdentityController` uses them (storage-plan/inventory-approval features referenced by their doc comments appear to have been removed). Confirm whether these constants are intentionally kept for a future feature or are now dead code. <!-- TODO: confirm with team -->
