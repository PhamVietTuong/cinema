# ADR-0008: Contended state changes are single conditional UPDATE statements

- **Date:** 2026-11-11 to 2026-11-12 (`d1cb811`, `a3806cd`, `dfdcc2c`). Author to confirm that this is the decision date.
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- Several flows had a read-then-write gap that two requests could pass at the same time: a duplicate or racing payment callback, two bookings spending the same loyalty points (or gift-card balance, since removed), two scanners admitting the same ticket, and a promotion use counter. The commit messages describe the fixes; for example `d1cb811`: "only the claim winner applies loyalty/payment side effects; the expiry job skips invoices it lost".
- The fixes all use the same shape, an `ExecuteUpdateAsync` whose `WHERE` clause carries the expected old state, with the number of affected rows as the verdict:
  - `InvoiceStore.TryTransitionStatusAsync` (`cinemabe/Cinema/3-Data/Cinema.Data/Stores/InvoiceStore.cs:181`) claims `Pending` to `Paid` or `Cancelled`.
  - `InvoiceStore.TryAdmitTicketAsync` (`InvoiceStore.cs:169`) admits a ticket once.
  - `UserStore.TryDeductPointsAsync` spends points only while enough remain. (It was joined by `GiftCardStore.TryDrawBalanceAsync` for gift-card balances; gift cards were removed on 2026-11-14.)
  - `PromotionProgramStore.TryReserveUseAsync` reserves a promotion use only while `MaxUsage` allows (amended 2026-11-16; it used to be `IncrementUsedCountAsync`, run when the invoice turned Paid).
- Callers run the claim inside a transaction and roll back if they lose (`BookingManager.cs:1086`, `:1856`; `InvoiceAdminManager.cs:71`).

## Decision

Where two writers can contend for the same row, the winner is decided by one conditional `UPDATE` in the data layer, not by a read followed by a write in the manager. Only the winner applies side effects (loyalty points, payment rows, restocking). The loser gets an explicit outcome (`AlreadyConfirmed`, `NotPending`, "already used", "Not enough loyalty points.").

Author to confirm: why this pattern was preferred over row versioning (EF concurrency tokens) or stronger transaction isolation.

## Consequences

- The business-rule outcomes for concurrent callbacks are deterministic and documented (`business-rules.md` section 3).
- The claim is exercised only through mocks. There is no test against a real database that proves the `UPDATE ... WHERE` behaves as intended; `docs/05-process/testing.md` (rule BR-PAY-03) names this gap.
- Amendment 2026-11-16: the promotion `MaxUsage` was the example of a write that did not use the pattern (the check ran at booking time but `UsedCount` was incremented only when the invoice turned Paid, so in-flight bookings could exceed the cap, a limit accepted at the time). Browser testing showed two Pending bookings both taking the last use of a one-use code and both getting paid, so the use is now reserved with a conditional UPDATE when the booking is created and released when it is cancelled or expires (`business-rules.md` section 5).
- Defects 2, 4 and 8 in `docs/05-process/ai-usage-log.md` are the cases that led to this pattern.

## Alternatives considered

- **Read, check, then write in the manager.** The state before `d1cb811`, `a3806cd` and `dfdcc2c`; it let two requests pass the same check.
- **An in-process lock.** Used for booking (`SemaphoreSlim` per showtime and room, ADR-0004) but not for payment, points or check-in: it cannot protect two server instances. Author to confirm: whether that was the reason.
- **EF concurrency tokens, or a stronger transaction isolation level.** Not shown in the repository. Author to confirm: why the conditional `UPDATE` was preferred.
