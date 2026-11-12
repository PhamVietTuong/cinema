# ADR-0008: Contended state changes are single conditional UPDATE statements

- **Date:** 2026-11-11 to 2026-11-12 (`d1cb811`, `a3806cd`, `dfdcc2c`). Author to confirm that this is the decision date.
- **Status:** Proposed (draft reconstructed from the repository, not yet confirmed by the author)

## Context

- Several flows had a read-then-write gap that two requests could pass at the same time: a duplicate or racing payment callback, two bookings spending the same loyalty points or gift-card balance, two scanners admitting the same ticket, and a promotion use counter. The commit messages describe the fixes; for example `d1cb811`: "only the claim winner applies loyalty/payment side effects; the expiry job skips invoices it lost".
- The fixes all use the same shape, an `ExecuteUpdateAsync` whose `WHERE` clause carries the expected old state, with the number of affected rows as the verdict:
  - `InvoiceStore.TryTransitionStatusAsync` (`cinemabe/Cinema/3-Data/Cinema.Data/Stores/InvoiceStore.cs:181`) claims `Pending` to `Paid` or `Cancelled`.
  - `InvoiceStore.TryAdmitTicketAsync` (`InvoiceStore.cs:169`) admits a ticket once.
  - `UserStore.TryDeductPointsAsync` and `GiftCardStore.TryDrawBalanceAsync` spend points and balance only while enough remain.
  - `PromotionProgramStore.IncrementUsedCountAsync` adds one in SQL.
- Callers run the claim inside a transaction and roll back if they lose (`BookingManager.cs:1086`, `:1856`; `InvoiceAdminManager.cs:71`).

## Decision

Where two writers can contend for the same row, the winner is decided by one conditional `UPDATE` in the data layer, not by a read followed by a write in the manager. Only the winner applies side effects (loyalty points, payment rows, restocking). The loser gets an explicit outcome (`AlreadyConfirmed`, `NotPending`, "already used", "Not enough loyalty points.").

Author to confirm: why this pattern was preferred over row versioning (EF concurrency tokens) or stronger transaction isolation.

## Consequences

- The business-rule outcomes for concurrent callbacks are deterministic and documented (`business-rules.md` section 3).
- The claim is exercised only through mocks. There is no test against a real database that proves the `UPDATE ... WHERE` behaves as intended; `docs/05-process/testing.md` (rule BR-PAY-03) names this gap.
- Not every contended write uses the pattern. Example: the promotion `MaxUsage` check happens at booking time but `UsedCount` is incremented only when the invoice turns Paid, so in-flight bookings can exceed the cap by their number, a known, accepted limit (`business-rules.md` section 5).
- Defects 2, 4 and 8 in `docs/05-process/ai-usage-log.md` are the cases that led to this pattern.

## Alternatives considered

- **Read, check, then write in the manager.** The state before `d1cb811`, `a3806cd` and `dfdcc2c`; it let two requests pass the same check.
- **An in-process lock.** Used for booking (`SemaphoreSlim` per showtime and room, ADR-0004) but not for payment, points or check-in: it cannot protect two server instances. Author to confirm: whether that was the reason.
- **EF concurrency tokens, or a stronger transaction isolation level.** Not shown in the repository. Author to confirm: why the conditional `UPDATE` was preferred.
