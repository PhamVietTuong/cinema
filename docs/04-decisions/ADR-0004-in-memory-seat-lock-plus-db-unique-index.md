# ADR-0004: Advisory in-memory seat lock, with a database unique index as the real guarantee

- **Date:** Author to confirm. First evidence in git: the in-memory lock exists from `bdc6ab4` (2026-05-24); the database index was added by `333e8d8` (2026-07-25).
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- While a customer chooses seats, the other viewers of the same showtime must see which seats are held. `BookingHub` (`/hubs/booking`) broadcasts `SeatLocked` and `SeatUnlocked`, backed by static `ConcurrentDictionary` fields in `BookingManager` (`business-rules.md` section 1). No Redis, distributed cache or distributed lock exists anywhere in the backend.
- That state is lost on restart and not shared between server instances.
- `333e8d8` states the reason for the second mechanism: "so two server instances can't double-sell a seat (the in-memory semaphore only guards one process)". It added `InvoiceTicket.IsActive` and a filtered unique index over active `(ShowTimeId, RoomId, SeatId)` rows (`IX_InvoiceTicket_ActiveSeat`, `cinemabe/sql/create_db.sql:717`).
- A violation of the index is translated to `SeatUnavailableException` and shown as "One or more selected seats were just booked by someone else." after the transaction rolls back.

## Decision

Two layers with different jobs:

1. An advisory lock in process memory, lifetime 5 minutes, not renewed by re-locking (`BookingManager.cs:1883`), plus a per-showtime-room `SemaphoreSlim` that serializes the check-then-insert inside one process. It improves the experience but guarantees nothing across instances.
2. A database unique index on active tickets. It is the only guarantee against double booking across instances.

Author to confirm: why an in-memory lock was chosen over a shared store, and whether a single server instance is the intended deployment.

## Consequences

- Correctness does not depend on the lock; a lost lock only makes a booking fail late, with the "just booked" message.
- A stale lock can keep showing a seat as held to other viewers until it is read again, because expiry is lazy (`business-rules.md` section 1).
- In a multi-instance deployment the lock would not be shared, so viewers on different instances would not see each other's holds. The index would still prevent double sale.
- An unpaid booking holds its seat for 15 minutes (30 once payment starts) until the reaper cancels it, a separate DB-backed timer (`PendingBookingReaper.cs:12-13`).
- Defect `5901ccf` (see `docs/05-process/ai-usage-log.md`) fixed three holes in the lock itself: no enforcement without a `ConnectionId`, renewal by re-locking, and a wrong unlock broadcast.
