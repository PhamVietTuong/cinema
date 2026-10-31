# Booking & Seat-Locking — Business Logic Reference

Source of truth: `cinemabe/Cinema/2-Business/Cinema.Business/Managers/BookingManager.cs` (1998 lines),
`IBookingManager.cs`, `BookingHub.cs`, `SignalRSeatNotificationService.cs`, `PaymentController.cs`,
`PendingBookingReaper.cs`, CinemaUser `booking-checkout`/`seat-selection`/`booking-hub.service.ts`.

## 1. Seat-locking mechanism — CURRENT state

**Still process-local, NOT distributed.** No `IDistributedCache`, Redis, or any distributed-lock class
exists anywhere in `cinemabe` (verified by repo-wide search — zero hits). Despite recent "seat lock
enforcement" / "seat hold timer" work, the storage is still two static, process-local, in-memory
`ConcurrentDictionary`s inside `BookingManager` (`cinemabe/Cinema/2-Business/Cinema.Business/Managers/BookingManager.cs:28,33`):

| Field | Type | Keyed by | Purpose |
|---|---|---|---|
| `_lockedSeats` | `ConcurrentDictionary<string,(string ConnectionId, DateTime LockedAt)>` | `"{showTimeId}:{roomId}:{seatId}"` | Advisory seat hold while a user is actively viewing/selecting (SignalR-driven) |
| `_bookingGates` | `ConcurrentDictionary<string, SemaphoreSlim>` | `"{showTimeId}:{roomId}"` | Serializes the booking-insert critical section per showtime+room (anti-double-sell) |

What changed recently (per code comments) is **defense in depth**, not a move to distributed state:
- A DB-level `SeatUnavailableException` path exists in `CreateBookingAsync`/`SellAtCounterAsync` for when
  a unique constraint on active `(ShowTimeId, RoomId, SeatId)` rejects a concurrent insert — explicitly
  documented as the safety net for **multi-instance deployment**, since the in-process semaphore gate
  only protects a single process. If this app is ever scaled horizontally, the in-memory gate alone is
  insufficient; the DB constraint is load-bearing.
  **Invariant/gotcha:** confirm a real unique index/constraint exists in the DB schema (`create_db.sql`/
  `upgrade_db.sql`) on active `(ShowTimeId, RoomId, SeatId)` for this safety net to actually work across
  instances — do not assume the semaphore alone is sufficient if this ever runs behind a load balancer.
- A separate background reaper (`PendingBookingReaper`, DB-backed, not memory-backed) now expires stale
  **Pending invoices**, independent of SignalR lock expiry (see §1 state machine below).

### 1.1 State machine (per seat, per `showTimeId:roomId:seatId`)

```
Available ──LockSeat(hub)──────────────▶ Locked (Reserved, advisory)
Locked ──UnlockSeat(hub) / disconnect──▶ Available
Locked ──5 min elapse (lazy check)─────▶ Available   (IsSeatLocked() purges on read, not a timer)
Locked/Available ──CreateBooking/SellAtCounter succeeds──▶ Booked (Invoice Pending or Paid)
Booked(Pending) ──ConfirmPayment/Callback success──▶ Booked(Paid)
Booked(Pending) ──CancelBooking(user) / 15-min reaper──▶ Available (seat freed, ticket deactivated)
Booked(Paid) ──ValidateTicket(QR, gate scan)──▶ Used (ticket.IsUsed=true), seat stays Booked
```

Seat `Status` returned by `GetSeatsAsync` (`BookingManager.cs:84`): `Occupied` (booked = Pending or Paid
invoice, per `GetBookedSeatIdsAsync`) takes priority over `Reserved` (locked), else `Available`.

### 1.2 Lock duration / expiry

- **Advisory SignalR lock**: 5 minutes, hard-coded in `IsSeatLocked` (`BookingManager.cs:1949`:
  `DateTime.UtcNow - info.LockedAt > TimeSpan.FromMinutes(5)`). Expiry is **lazy** — only evaluated when
  `IsSeatLocked` is called (i.e. on next `GetSeatsAsync`/`ComposeInvoiceAsync` check), not by a timer
  sweeping the dictionary. A stale entry sits in `_lockedSeats` until something reads it.
- **Pending-invoice hold window**: 15 minutes, hard-coded in `PendingBookingReaper._holdWindow`
  (`cinemabe/.../Services/PendingBookingReaper.cs:13`). Reaper runs every 1 minute
  (`PendingBookingReaper._interval`), calling `IBookingManager.ExpireStalePendingBookingsAsync(15m)`,
  which cancels every Pending invoice older than the cutoff, deactivates its tickets, and restores
  points/gift-card/food-stock.
- **Frontend mirror**: `booking-checkout.component.ts` hardcodes `HOLD_SECONDS = 15 * 60` and runs its own
  `setInterval` countdown (`_startHoldCountdown`) purely for UX — it does **not** enforce anything; it is
  explicitly commented as mirroring the reaper's window. The actual authority is server-side (the reaper +
  the Pending invoice's `CreationTime`).
  **Gotcha:** if the reaper's `_holdWindow` is ever changed, `booking-checkout.component.ts`'s
  `HOLD_SECONDS` must be updated to match or the UI countdown will desync from the real expiry.

### 1.3 Disconnect handling

`BookingHub.OnDisconnectedAsync` (`BookingHub.cs:39`) calls `IBookingManager.ReleaseConnectionLocks(connectionId)`,
which scans `_lockedSeats` for every entry owned by that connection, removes them, and the hub broadcasts
`SeatUnlocked` for each to the relevant room group. SignalR itself removes the connection from its groups
automatically; no explicit group-leave call is made.

### 1.4 Concurrency handling (double-booking prevention)

Two independent layers, both must hold:
1. **In-process gate**: `BookingGate(showTimeId, roomId)` is a `SemaphoreSlim(1,1)` acquired before
   `ComposeInvoiceAsync` + invoice insert in both `CreateBookingAsync` and `SellAtCounterAsync`, released
   in `finally`. Serializes the "read booked seats → validate → insert" sequence for the SAME showtime+room
   within one process so two concurrent requests can't both pass the "seat free" check.
2. **DB constraint (cross-instance safety net)**: if a `SeatUnavailableException` surfaces (expected to be
   thrown by the data layer / a unique index violation), both booking paths catch it specifically and
   rethrow as a friendly `InvalidOperationException("One or more selected seats were just booked by
   someone else.")`, rolling back the transaction first.
3. **Double (paired) seats**: if any requested seat belongs to a `SeatGroupId`, every seat sharing that
   group must also be in the same request, else `InvalidOperationException("A double seat must be booked
   together with its partner.")` (`BookingManager.cs:468`).
4. **Advisory lock check**: a seat held by another connection (`IsSeatLocked(..., excludeConnectionId:
   myConnectionId)`) is rejected inside `ComposeInvoiceAsync` with `InvalidOperationException("Seat {id} is
   being held by another user.")` — but **only when the caller supplies `ConnectionId`** in
   `CreateBookingRequest`/counter sale request. A booking call with no `ConnectionId` skips this check
   entirely (by design, so a booker's own held seats still pass, and server/API-only flows aren't blocked).

## 2. Booking lifecycle — end to end

| # | Step | Endpoint / Hub method | Notes |
|---|---|---|---|
| 1 | Join room | Hub `JoinRoom(showTimeId, roomId)` | Adds connection to SignalR group `room-{showTimeId}-{roomId}`; must happen before locking to receive others' lock events |
| 2 | View seat map | `POST /api/Payment/GetSeats` (`[AllowAnonymous]`) | Returns `SeatDTO[]` with `Status` (Available/Reserved/Occupied), price, lock flag |
| 3 | Get price list | `POST /api/Payment/GetShowTimePrices` (`[AllowAnonymous]`) | Resolved price per PatronCategory for the showtime+room |
| 4 | Lock seat (advisory) | Hub `LockSeat(showTimeId, roomId, seatId)` | Rejects if already locked by someone else → `SeatLockFailed` to caller; else stores lock, broadcasts `SeatLocked` to group |
| 5 | Unlock seat | Hub `UnlockSeat(showTimeId, roomId, seatId)` | Only the owning connection's lock is removed; broadcasts `SeatUnlocked` to group |
| 6 | (Checkout page) client-side hold countdown starts | N/A (frontend only, `booking-checkout.component.ts`) | UX mirror of reaper's 15-min window; hub connection from step 1 stays open across navigation so locks persist |
| 7 | Validate discount code (optional) | `POST /api/Payment/ValidateDiscountCode` | Non-throwing; reports usability + amount |
| 8 | Validate gift card (optional) | `POST /api/Payment/ValidateGiftCard` | Non-throwing |
| 9 | Create booking | `POST /api/Payment/CreateBooking` | Creates `Invoice` **Status=Pending**, `Channel=Online`; re-validates everything server-side (seats free, patron category, stock, points, gift card, promos); unlocks the booker's own advisory locks, broadcasts `SeatBooked` to the room; invoice created inside `BookingGate` + DB transaction |
| 10 | Initiate payment | `POST /api/Payment/InitiatePayment` | Resolves gateway by `provider` (or default); if `FinalAmount <= 0` (e.g. fully covered by gift card) finalizes immediately with no gateway call; else returns `RedirectUrl` + `PaymentReference` |
| 11a | Confirm payment (sandbox/dev path) | `POST /api/Payment/ConfirmPayment` | Only succeeds for providers whose `VerifyPaymentAsync` can confirm synchronously (Sandbox). **VNPay always returns `false`** here — real confirmation is callback-only |
| 11b | Payment callback (IPN/webhook, authoritative for real gateways) | `POST`/`GET /api/Payment/PaymentCallback?provider=...` (`[AllowAnonymous]`) | Signature-verified per gateway; idempotent (`AlreadyConfirmed`); amount-checked; only this path can flip VNPay/MoMo/Stripe invoices to Paid |
| 12 | Finalize paid invoice | internal `FinalizePaidInvoiceAsync` (called by 11a/11b) | Sets `Status=Paid`, `PaidAt`, adds `InvoicePayment`, accrues loyalty points, re-evaluates membership tier, marks promo code consumed, sends confirmation email/SMS (e-ticket QR) |
| 13 | Gate check-in | `POST /api/Payment/ValidateTicket` (`[Authorize(Roles=Admin)]`) | Validates QR, requires `Invoice.Status==Paid`, rejects already-used ticket, marks `ticket.IsUsed=true` |
| 14a | Cancel (user-initiated) | `POST /api/Payment/CancelBooking` | Only a **Pending** invoice owned by the caller; sets `Status=Cancelled`, deactivates tickets, restores points/gift-card/food stock |
| 14b | Expire (automatic) | background `PendingBookingReaper` → `IBookingManager.ExpireStalePendingBookingsAsync(15m)` | Same cancel side effects, batch-applied to every Pending invoice older than 15 minutes, runs every 1 minute |
| 15 | Refund | **Not implemented** | `InvoiceStatus` enum explicitly reserves value 4 ("was Refunded — do not reuse") but no refund code path exists anywhere in `Cinema.Business`. A Paid invoice has no cancel/refund transition in this codebase today |

Counter (box-office) sale is a parallel, single-step path: `QuoteCounterAsync` (dry run, no writes) and
`SellAtCounterAsync` (creates the invoice **already Paid**, with tenders, in one transaction) — same seat
validation/pricing/stock/points/gift-card logic as online booking, reused via `ComposeInvoiceAsync`.

## 3. SignalR — `BookingHub` (route `/hubs/booking`, `[Authorize]`)

| Method / Event | Direction | Payload | Broadcast scope |
|---|---|---|---|
| `JoinRoom(showTimeId, roomId)` | Client → Hub (invoke) | 2 Guids | Adds caller to group `room-{showTimeId}-{roomId}` |
| `LockSeat(showTimeId, roomId, seatId)` | Client → Hub (invoke) | 3 Guids | — |
| `SeatLockFailed` | Hub → Caller only | `(seatId: Guid, message: string)` | `Clients.Caller` |
| `SeatLocked` | Hub → Group | `(seatId: Guid, connectionId: string)` | `Clients.Group(room-{st}-{r})` — includes the locker's own connection, which the frontend uses `connectionId` to self-filter |
| `UnlockSeat(showTimeId, roomId, seatId)` | Client → Hub (invoke) | 3 Guids | — |
| `SeatUnlocked` | Hub → Group | `(seatId: Guid)` | `Clients.Group(room-{st}-{r})` |
| `SeatBooked` | Hub → Group (server push via `ISeatNotificationService`) | `(seatIds: Guid[])` | `Clients.Group(room-{st}-{r})`, fired after a committed `CreateBooking`/`SellAtCounter` |
| `OnDisconnectedAsync` | Hub lifecycle | — | Releases every lock the connection held, emits `SeatUnlocked` per seat to each affected group |

`SignalRSeatNotificationService` (the only non-dev implementation of `ISeatNotificationService`) wraps the
broadcast in try/catch and only logs (`LogProvider.Current.Fatal`) on failure — **a broadcast failure never
fails an already-committed booking**. Dev/test default is `NoOpSeatNotificationService`.

`RoomGroup` naming: `$"room-{showTimeId}-{roomId}"` — exact string, case-sensitive, defined once in
`BookingHub.RoomGroup` and reused by the notification service; never hand-construct this string elsewhere.

## 4. Business rules / validations (in `ComposeInvoiceAsync`, shared by online + counter)

| Rule | Behavior |
|---|---|
| Room must be `RoomStatus.Active` | else `InvalidOperationException("This room is not open for booking.")` |
| Counter-only: showtime must belong to staff's own theater | else `AccessDeniedException` |
| Counter-only: showtime must be active and not yet ended | else `InvalidOperationException("This showtime is not open for sale.")` |
| Seat already booked (Pending or Paid invoice) | `InvalidOperationException($"Seat {id} is already booked.")` |
| Seat locked by another connection (only checked if caller passes `ConnectionId`) | `InvalidOperationException($"Seat {id} is being held by another user.")` |
| Seat must exist / belong to the room / be active | `KeyNotFoundException` / `InvalidOperationException` |
| Double seat must be booked with its full group | `InvalidOperationException("A double seat must be booked together with its partner.")` |
| Every seat needs a `PatronCategoryId` whose `Kind` matches the seat's kind | `InvalidOperationException` (two distinct messages: missing category vs. mismatched category) |
| Price override (counter only) must be between 0 and list price | `InvalidOperationException` |
| Price override present → requires `ApproverUserId` (manager approval) | `AccessDeniedException("Manager approval is required for a price override.")` |
| Food item must belong to the theater and be available | `InvalidOperationException` (two messages) |
| Food/combo stock insufficient | `InvalidOperationException($"'{name}' is out of stock or has insufficient quantity.")` — dry run only checks, real sale calls `TryApplyStockDeltaAsync` which fails atomically |
| Gift card invalid/expired | `InvalidOperationException("Invalid or expired gift card.")` |
| Discount code invalid/expired/exhausted/out of scope | `InvalidOperationException("Discount code is invalid or no longer available.")` (explicit code path); auto-apply promos never throw, just skipped |
| Points redemption capped by balance AND order total | silently clamped, never throws |
| Payment: only a `Pending` invoice can be paid/confirmed | `ConfirmPaymentAsync`/`InitiatePaymentAsync` return `false`/`null` (not an exception) if not Pending or not owned by caller |
| Payment callback idempotency | `AlreadyConfirmed` outcome if invoice already Paid; `NotPending` if in an unexpected status (e.g. Cancelled) |
| Payment callback amount mismatch | rejected as `InvalidAmount`, comparing `(decimal)(long)Math.Round(invoice.FinalAmount)` to match VNPay's own rounding |
| Cancel/Expire only affects `Pending` invoices | Paid/Cancelled/Failed invoices are untouched |
| Ticket validation (gate) | must be `Paid`, not already `IsUsed`; both checked via explicit exceptions |

**Max seats per transaction**: no explicit cap on seat count found in `BookingManager`; `CreateBookingRequest.Seats`
only requires `MinLength(1)`. (`PromotionProgram.MaxTicketsPerTransaction` caps how many seats a *promotion*
discounts, not how many a customer may book.)

## 5. Error conditions / messages (exact strings worth matching in UI/tests)

- `"ShowTime/Room combination not found."`
- `"This room is not open for booking."`
- `"This showtime does not belong to your theater."` (counter)
- `"This showtime is not open for sale."` (counter)
- `"Seat {id} is already booked."`
- `"Seat {id} is being held by another user."`
- `"Seat {id} not found."` (KeyNotFoundException)
- `"Seat {row}{col} does not belong to this room."`
- `"Seat {row}{col} is blocked and cannot be booked."`
- `"A double seat must be booked together with its partner."`
- `"A patron category is required for every seat."`
- `"Seat {id} is not available for the selected patron category."`
- `"The price override for {what} must be between 0 and its list price."`
- `"Manager approval is required for a price override."` (AccessDeniedException)
- `"'{name}' is not available at this theater."` / `"'{name}' is no longer available."`
- `"'{name}' is out of stock or has insufficient quantity."`
- `"Invalid or expired gift card."`
- `"Discount code is invalid or no longer available."`
- `"One or more selected seats were just booked by someone else."` (wraps `SeatUnavailableException`, both booking paths)
- `"A showtime and room are required to sell seats."` (counter, seats present but ShowTimeId/RoomId missing)
- Hub: `SeatLockFailed(seatId, "Seat is already locked.")`
- `CancelBooking`/`InitiatePayment`/`ConfirmPayment` return `false`/`null`/generic `{ error }` JSON rather than throwing when the business condition isn't met (not-owner, wrong status) — controller maps these to `400 BadRequest`, not exceptions.
- VNPay IPN responses use the dedicated `{RspCode, Message}` shape (`VnPayGateway.BuildIpnResponse`) driven off `PaymentCallbackOutcome`; all other providers get a generic `Ok`/`BadRequest`.

## 6. Key invariants / gotchas for future changes

1. **Lock state is process-local.** Horizontal scaling (multiple API instances) breaks the advisory
   SignalR lock entirely (two instances have independent `_lockedSeats`/`_bookingGates`) — only the DB
   unique-constraint path (`SeatUnavailableException`) protects against double-booking across instances.
   Do not assume seat locks are visible across instances without first adding a distributed store.
2. **Lock expiry is lazy, not swept.** A seat can show as locked in `_lockedSeats` indefinitely past 5
   minutes if nothing calls `IsSeatLocked`/`GetSeatsAsync` for it — it self-heals only on next read. There
   is no background timer purging `_lockedSeats` (unlike `PendingBookingReaper` for Pending invoices).
3. **Two different timers, two different owners.** The 5-minute advisory SignalR lock (in-memory,
   connection-scoped) and the 15-minute Pending-invoice hold (DB-backed, reaper-driven) are independent
   and must not be conflated — a seat can be "unlocked" (advisory) while its invoice is still Pending and
   blocking rebooking via `GetBookedSeatIdsAsync`, or vice versa.
4. **`ConnectionId` is opt-in for the "held by another user" check.** Any caller that omits it (e.g. a
   server-to-server or API-only booking) bypasses the advisory-lock rejection — this is intentional (so
   the booker's own locks don't block their booking) but means the advisory lock is not a hard guarantee
   against a parallel request without a connection id.
5. **No refund flow exists.** `InvoiceStatus` value 4 is explicitly reserved as dead (comment: "was
   Refunded — do not reuse") — don't repurpose it, and don't assume a Paid invoice can be reversed; that
   requires new business logic if ever requested.
6. **VNPay is callback-authoritative; `ConfirmPaymentAsync`'s synchronous path always fails for it**
   (`VerifyPaymentAsync` hardcoded to return false) — only `HandlePaymentCallbackAsync` (the IPN) can mark
   a VNPay invoice Paid. Don't "fix" `ConfirmPaymentAsync` to trust the browser redirect.
7. **Booking and counter-sale share `ComposeInvoiceAsync`.** Any new seat/food/pricing rule must be added
   there once, not duplicated per path, or online booking and counter sale will silently diverge.
8. **Frontend hold countdown is cosmetic.** `booking-checkout.component.ts`'s `HOLD_SECONDS` constant is a
   hardcoded mirror of the server's 15-minute reaper window — changing one without the other desyncs the
   UI from actual expiry, but does not change correctness (server is authoritative either way).
9. **Group name string `room-{showTimeId}-{roomId}`** is defined once in `BookingHub.RoomGroup` — reuse it,
   don't hand-build it, or group broadcasts silently go nowhere.
10. **Seat `SeatGroupId` (double seats) must always be booked/locked as a pair** at the booking-validation
    layer; the advisory lock/hub methods, however, operate per individual seat id — a future "lock the
    pair atomically" feature does not currently exist and would need new hub logic.
11. **`BookingGate` semaphores are created lazily and never removed** (`ConcurrentDictionary.GetOrAdd`,
    no eviction) — a very large number of distinct showtime/room combinations over the app's lifetime
    accumulates `SemaphoreSlim` objects for the process lifetime. Not currently a practical problem given
    the cinema's bounded showtime/room count, but worth knowing before assuming it's cleaned up.
