# AI Usage Log

> Purpose: Evidence of how AI tools were used on this project, what they got wrong, how their output was checked, and which parts the author must be able to explain.
> Sources: `git log` (trailers, dates, paths) as of 2026-11-12, the audit commits cited per row, `.github/workflows/ci.yml`, `docs/05-process/testing.md`, `AGENTS.md`.
> Last reviewed: 2026-11-12

Everything below is derived from the repository and its git history. This document set (this log, the ADRs, the acceptance criteria, the NFRs and the diagrams) was itself drafted by an AI agent in the group E session. It is checked by the `Checked against` and `Source` citations in each file and by a hash and line-reference script run once at writing time, not by automated tests in CI. Counts are taken at commit `f20f799` (the baseline of this change; `git rev-list --count f20f799` is 502); recompute them with the commands in section 7. Anything that exists only in the author's head (the prompts typed, what was edited by hand, the licence decision) is a visible `Student input needed:` line, never a guess. Rule of thumb for reading the numbers: a `Co-Authored-By: Claude ...` trailer proves the AI co-authored that **commit**. It does not prove which individual lines are AI-written, and the absence of a trailer does not prove a commit has no AI content.

## 1. Tools and models

| Tool | Evidence | Role |
|---|---|---|
| Claude Code (CLI agent) | `Co-Authored-By: Claude ...` trailer on 379 of 502 commits at `f20f799`; `AGENTS.md` / `CLAUDE.md` are agent instruction files; `docs/` is maintained by an `/update-docs` workflow (`AGENTS.md`) | Wrote and edited code, tests, SQL and docs in the repository, ran builds and tests |
| BMAD agent framework (local, not committed) | `_bmad/` (config, `core`, `bmm`, 23 per-skill override files in `_bmad/custom/*.toml` plus `gen_model_overrides.py`), `_bmad-output/` (`implementation-artifacts/spec-*.md`, `deferred-work.md`, `planning-artifacts/`), and 29 `bmad-*` skills in `.claude/skills/` including the agent personas `bmad-agent-analyst`, `-architect`, `-dev`, `-pm`, `-ux-designer`. All three folders are git-ignored (`.gitignore:26`, `:30-31`), so they are not in the history | Structured the work as specs, builds and reviews. The group B to E specs (for example `spec-group-e-ai-log-and-design-evidence.md`) drove the audit fixes |

Models named in commit trailers (counted with `git log --format=%B | grep "^Co-Authored-By: Claude" | sort | uniq -c`):

| Trailer | Commits |
|---|---|
| Claude Sonnet 5 | 166 |
| Claude Sonnet 5.5 | 120 |
| Claude Opus 4.8 (1M context) | 67 |
| Claude Opus 5 (1M context) | 21 |
| Claude Opus 5 | 5 |
| **Total with a Claude trailer** | **379 of 502 (75.5%)** |

- History spans 2026-05-08 to 2026-11-12.
- 123 commits carry no Claude trailer, among them the first commit `ee4efc9` (2026-05-08), `bdc6ab4` ("backend init", 2026-05-24) and `d69136a` ("init fe", 2026-05-31).
- Commits are authored under two names, `VietTuong` (448) and `Phạm Viết Tường` (54). Author to confirm: both are the student's own identities.
- Student input needed: any AI tool used outside the repository (chat assistants, code completion, translation, image tools). Git cannot show that.

## 2. Scope: where the AI touched the code

Counted as the number of file touches in AI-co-authored commits per path, at `f20f799` (command in section 7), top areas. `QA-tests` (348, a folder that is git-ignored today) and the root `cinemabe` (160) are left out of the table:

| Path | File touches in AI-co-authored commits |
|---|---|
| `cinemabe/Cinema/2-Business` | 1210 |
| `cinemafe/projects/CinemaAdmin` | 716 |
| `cinemafe/projects/CinemaUser` | 624 |
| `cinemafe/projects/CinemaStaff` | 615 |
| `cinemabe/Cinema/3-Data` | 614 |
| `cinemabe/Cinema/1-Service` | 460 |
| `cinemafe/projects/CinemaLib` | 440 |
| `docs` | 195 |
| `e2e` | 49 |

(A file touched by several commits is counted once per commit, so these numbers rank areas, they are not line counts.)

By activity, from commit subjects: feature work across all four layers and the three Angular apps, regenerating NSwag clients (`chore: regenerate NSwag clients ...`), the audit fix groups A to D of 2026-11-11 to 2026-11-12 (section 4), tests, CI gates, and the knowledge base under `docs/` (created by `7bd1808`, 2026-11-01, Claude Sonnet 5).

## 3. Prompts, generated code, and what the student changed

| Item | Status |
|---|---|
| Main prompts | Student input needed: the prompts you actually typed. They are not stored in the repository; Claude Code session transcripts live outside it and are not committed. This log does not reconstruct prompts. |
| AI-generated code | Commit-level evidence only: section 2 and the trailer counts in section 1. For one commit, `git show --stat <hash>` lists exactly what the AI session changed. |
| What the student changed by hand | Student input needed: which AI output you edited, rejected or rewrote, and why. Git shows every commit under your name, so it cannot separate your edits from the AI's. |
| Direction given to the AI | Recorded in the repository: `AGENTS.md` (policy and conventions the agent must follow, for example exactly three controllers, never hand-edit NSwag output, schema changes by hand into SQL scripts, callback-authoritative payments). These are decisions the student made and enforced. |

## 4. Defects caught in AI-assisted code (with root cause and fixing commit)

How these were found: the audit groups A to D (2026-11-11 to 2026-11-12) re-read the existing code against the business rules, fixed what did not hold, and added regression tests. "Introduced in" is the first commit that added the code, found with `git log -S"<symbol>" --reverse`, and the trailer on that commit is shown. A trailer is commit-level evidence, see the note at the top.

Every hash below was resolved with `git log` on 2026-11-12.

| # | Defect | Root cause (as stated in the fixing commit or visible in its diff) | Introduced in | Fixed in | Regression test |
|---|---|---|---|---|---|
| 1 | The unsigned Sandbox payment gateway was registered in every environment, including Production | `Program.cs` registered `SandboxPaymentGateway` unconditionally; a callback or confirm was not required to come from the gateway that owns the invoice, so an unsigned Sandbox callback could finalize an invoice. Fix: register Sandbox only in Development or with `Payments:EnableSandbox=true`; require the resolved gateway to own the invoice. | `3c92257` 2026-07-19 (Claude Opus 4.8) | `0f83416` | `PaymentCallbackTests.HandlePaymentCallbackAsync_SandboxCallbackOnVnPayInvoice_IsRejected_NoWrites`, `ConfirmPaymentAsync_InvoiceOwnedByUnavailableProvider_DoesNotFallBackToSandbox` |
| 2 | Duplicate payment callbacks (or a callback racing the expiry job) could apply loyalty points and the payment row twice | The Pending to Paid / Pending to Cancelled change was a read-then-write, not atomic. Fix: `InvoiceStore.TryTransitionStatusAsync` (a conditional `UPDATE ... WHERE Status = Pending`), finalize inside a transaction, only the claim winner applies side effects, the expiry job skips invoices it lost. | `FinalizePaidInvoiceAsync` first added in `3c92257` (Claude Opus 4.8) | `d1cb811` | `PaymentCallbackTests.HandlePaymentCallbackAsync_LosesTheClaimRace_ReturnsAlreadyConfirmed_NoSideEffects`, `PaymentCallbackTests.HandlePaymentCallbackAsync_TwoConcurrentDeliveries_ApplyTheSideEffectsExactlyOnce` (added in `12bd355`) |
| 3 | A 6-digit two-factor code could be guessed without limit; disabled accounts could still sign in through some paths; the last active Admin could be demoted or deleted | 2FA failures were not counted toward the login lockout; sign-in paths did not check `User.Status`; no guard on the last Admin. Fix: wrong codes share the lockout and burn the code at the threshold, `EnsureAccountActive` on every sign-in path, `EnsureAnotherActiveAdminRemainsAsync`. | 2FA first added in `f8acfc7` 2026-07-10 (Claude Opus 4.8); the status check and last-Admin guard are new code in the fix | `ab9058b` | `AuthServiceTests.VerifyTwoFactor_FiveWrongCodes_LocksAccountAndBurnsTheCode`, `Login_DisabledAccount_ThrowsUnauthorized_NoToken`, `DeleteUser_LastActiveAdmin_Throws_AndStaysActive` |
| 4 | Loyalty points and gift-card balance could be overspent by two concurrent bookings; auto-apply promotions ignored `MaxUsage` and movie scope | Points and gift-card spend were read-modify-write; the auto-apply candidate filter did not check the usage cap or the program's movie. Fix: atomic conditional `UPDATE ... WHERE Points >= n` / `Balance >= a`, filters added. | Points redemption first added in `4ea1e63` 2026-07-19 (Claude Opus 4.8); gift cards in `d5ab544` 2026-07-25 (Claude Opus 4.8) | `a3806cd` | `BookingServiceTests.CreateBookingAsync_PointsAlreadySpentByAConcurrentBooking_IsRejectedAndRolledBack`, `..._GiftCardAlreadyDrawnByAConcurrentBooking_...`, `..._AutoApplyProgramAtItsUsageCap_IsNotApplied`, `..._AutoApplyProgramScopedToAnotherMovie_IsNotApplied` |
| 5 | VNPay rejected a repeated payment attempt, timestamps were wrong in the payment URL, and the 15-minute reaper could cancel an invoice whose payment was still in flight | `vnp_TxnRef` was reused across attempts (VNPay rejects a reused reference), `vnp_CreateDate`/`vnp_ExpireDate` were not in GMT+7, the reaper had no grace for a started payment. Fix: per-attempt TxnRef, GMT+7 timestamps, an extra 15-minute `_paymentGrace`, API runs in GMT+7. | `vnp_TxnRef` first added in `3c92257` (Claude Opus 4.8); reaper in `ed1a067` 2026-07-05 (Claude Opus 4.8) | `5cca599` | `PaymentCallbackTests.ExpireStalePendingBookingsAsync_GivesAnInFlightPaymentTheExtraGraceWindow` |
| 6 | The admin status override and invoice delete did not give back what a Pending booking had reserved (seats, redeemed points, gift-card balance, food stock) | The override changed `Status` only; reversal logic lived inside `BookingManager`. Fix: extract `InvoiceLifecycle`, constrained atomic transitions, restore reservations on cancel and delete. | `UpdateStatusAsync` first in `a9c19d6` 2026-06-13 (no Claude trailer) | `0862cb2` | `InvoiceAdminManagerTests.UpdateStatus_PendingToCancelled_FreesSeatsAndRestoresReservations`, `Delete_PendingInvoice_RestoresPointsAndGiftCard` |
| 7 | The VNPay IPN answered with a generic body, so VNPay never saw success and retried for up to 50 minutes; the callback amount was never compared with the invoice | The IPN endpoint returned `{message}` / `{error}` for every provider. Fix: `VnPayGateway.BuildIpnResponse` maps outcomes to the required `{"RspCode","Message"}` JSON (00/01/02/04/97/99) and `ParseCallback` validates `vnp_Amount`. The commit subject calls the fix "spec-compliant". | `3c92257` (Claude Opus 4.8) | `2df415b` 2026-10-23 | `PaymentControllerCallbackTests`, `VnPayGatewayTests` (pinned later in `9dcb577`) |
| 8 | Admin ticket check-in could admit one ticket twice under a race; the promotion `UsedCount` could lose increments; the payment-return page did not show the cancelled state | Check-in and `UsedCount += 1` were read-modify-write. Fix: `InvoiceStore.TryAdmitTicketAsync` (conditional `UPDATE ... WHERE IsUsed = 0`) and `PromotionProgramStore.IncrementUsedCountAsync` (`UsedCount + 1` in SQL). | `ValidateTicketAsync` first in `a7784e1` 2026-07-10 (Claude Opus 4.8); `UsedCount += 1` first in `3bcc898` 2026-07-05 (Claude Opus 4.8) | `dfdcc2c` | `PaymentCallbackTests.ValidateTicketAsync_LosingTheAdmitRace_ReportsAlreadyUsed`, `HandlePaymentCallbackAsync_ValidPayment_CountsThePromotionUseWithAnAtomicIncrement` |
| 9 | The advisory seat lock was not enforced for a booking with no `ConnectionId`, a re-lock renewed the hold forever, and the hub broadcast "unlocked" even when nothing was released | Enforcement was skipped when the client sent no connection id; `LockSeat` overwrote the timestamp; `UnlockSeat` returned nothing. Fix in the diff: enforce always, `AddOrUpdate` that keeps the original lock time, `UnlockSeat` returns whether it removed the caller's own lock. | Seat lock first in `bdc6ab4` 2026-05-24 ("backend init", no Claude trailer). Not attributed to the AI by the trailer evidence | `5901ccf` | `BookingServiceTests.CreateBookingAsync_NoConnectionId_RejectsSeatHeldByAnyone`, `LockSeat_ReLockBySameConnection_DoesNotRenewTheHoldWindow`, `BookingHubTests.UnlockSeat_ByANonOwner_BroadcastsNothing` |
| 10 | The AI-written knowledge base described the code incorrectly (`GetShowTime` auth, `StaffHub` events, the `ConfirmEmail` rate-limit policy, `GetAuditLog` range, controller line count, `Seat.Kind`/Aisle and others) | The docs generated on 2026-11-01 (`7bd1808`, Claude Sonnet 5) were not checked claim by claim against the code. Fix: each claim re-verified at the cited code. | `7bd1808` 2026-11-01 (Claude Sonnet 5) | `6aa0b9a`, `f20f799` | None (documentation). Verified by re-reading the cited code |

Reading the table honestly:

- Rows 1, 2, 3, 4, 5, 7, 8 and 10 trace to commits that carry a Claude trailer. Row 9 traces to a commit without one, and row 6 to a commit without one, so those two are defects found in the audit, not demonstrated AI errors.
- The audit itself was also AI-assisted (the fixing commits carry Claude Sonnet 5.5 trailers). The independent check on that is the regression tests, which fail without the fix, and the CI gates in section 5.
- Review passes found further gaps in AI output: `20e672b` (group B test gaps), `4e0c51e` (group C: analyzer ratchet, db-init marker, pinned gitleaks), `f20f799` (group D doc corrections). The review findings themselves are not committed. Student input needed: where the review findings are kept, if you want them cited.

## 5. How AI output is controlled

What the repository itself shows (each item can be checked):

1. **Policy given to the agent up front.** `AGENTS.md` lists hard rules the agent must follow, for example the three-controller rule (`ControllerArchitectureTests.OnlyThreeControllersExist`), never hand-edit NSwag output, layer dependency direction, callback-authoritative payments, and never commit secrets.
2. **Tests that fail without the fix.** `docs/05-process/testing.md` ("Defect tracking"): every fix lands with a regression test tagged with the rule it protects (`[Trait("Req", "BR-xx-nn")]`). Section 4 lists them per defect.
3. **Automated gates on every push and pull request** (`.github/workflows/ci.yml`): backend build plus the analyzer warning ratchet, `dotnet test` for both test projects, a 60% line-coverage floor for `Cinema.Business`, frontend lint ratchet, unit tests and production builds for all three apps, a Docker image build, and a Playwright e2e job.
4. **Secret hygiene.** `.gitleaks.toml` plus a `gitleaks` CI job scanning full history (informational for now, `continue-on-error`); `appsettings.Development.json` is git-ignored (`.gitignore:18`); `AGENTS.md` forbids committing secrets. The committed `ci.yml` and `docker-compose.yml` use throw-away values only.
5. **Cross-check against official documentation.** Evidence: `2df415b` rebuilt the VNPay IPN handling against VNPay's published `{RspCode, Message}` contract, and `docs/03-technical/integrations.md` records the sandbox test procedure. Student input needed: which other vendor documents (VNPay, Google/Facebook OAuth, Twilio, OMDb) you read yourself before accepting AI output.
6. **Documentation kept in step with code.** `AGENTS.md` requires the matching `docs/` file to change in the same change; group D (`6aa0b9a`) re-verified every claim.
7. **Review before merge.** Audit groups were followed by review commits (`20e672b`, `4e0c51e`, `f20f799`).
8. **BMAD model policy** (local files, not committed). `_bmad/custom/bmad-build.toml:1-2` states: Opus for important work (planning, review), Sonnet for everything else, generated by `_bmad/custom/gen_model_overrides.py`; line 14 sends review, audit and judge subagents to `opus` and search, read and summarize subagents to `sonnet`. The commit trailers show both families in use (section 1). Student input needed: whether the policy was applied to every session, since the trailers alone cannot show that.

What the repository cannot show, and the author must state:

- Student input needed: the review checklist you personally apply to an AI diff (what you read, what you run before committing).
- Student input needed: licence check. No `LICENSE` file exists in the repository root and no licence review of AI-suggested code or of added dependencies is recorded. State whether you checked (for example the licences of NuGet and npm packages the AI added, and whether any generated code was copied from a licensed source).
- Student input needed: confirmation that no personal data, credentials or real customer data were pasted into prompts. The repository side is covered by item 4 (nothing sensitive is committed), but prompts are not in the repository.

## 6. Modules the student must be able to explain

These carry the most AI-written logic and the defects in section 4. The author should be able to explain each without the AI: what it guarantees, which invariant it protects, and how it fails.

| Module | Where | Why it matters |
|---|---|---|
| Atomic invoice status claim | `InvoiceStore.TryTransitionStatusAsync` (`cinemabe/Cinema/3-Data/Cinema.Data/Stores/InvoiceStore.cs:181`), used at `BookingManager.cs:1086`, `:1856`, `InvoiceAdminManager.cs:71` | Prevents double payment side effects (defects 2, 6) |
| Payment callback handling | `BookingManager.HandlePaymentCallbackAsync`, `VnPayGateway` (signature, amount x100, `BuildIpnResponse`), `PaymentController.PaymentCallback` | Money path and the `{RspCode, Message}` contract (defects 1, 7) |
| Pending-booking reaper and grace | `PendingBookingReaper.cs:12-13`, `BookingManager.cs:1831` | Why a seat can stay held up to 30 minutes (defect 5) |
| Seat lock and booking gate | `BookingManager.LockSeat/UnlockSeat/IsSeatLocked` (`:1883` onward), `BookingHub`, unique index `IX_InvoiceTicket_ActiveSeat` (`cinemabe/sql/create_db.sql:717`) | Advisory in-memory lock versus the DB guarantee (defect 9) |
| Login lockout and 2FA | `AuthManager.LoginAsync`, `VerifyTwoFactorAsync` (`:42-43`, `:221`), `PasswordHasher` (PBKDF2, 100000 iterations, `PasswordHasher.cs:14`) | Account security (defect 3) |
| Atomic spend of points and gift cards | `UserStore.TryDeductPointsAsync`, `GiftCardStore.TryDrawBalanceAsync`, `BookingManager.CreateBookingAsync` | Defect 4 |
| Gate scan | `GateManager.EvaluateScanAsync` (`GateManager.cs`), `InvoiceStore.TryAdmitTicketAsync` (`InvoiceStore.cs:169`) | Nine scan outcomes (one admit, eight denials) plus an atomic admit (defect 8) |
| Pricing formula | `BookingManager.BuildSeatPricingContextAsync` | Holiday versus weekday matrix, flat surcharges |
| Endpoint guard coverage | `ControllerAuthorizationCoverageTests`, `StaffToolsetAuthorizationTests` | Why a new endpoint fails CI unless guarded or allow-listed |

Student input needed: which of these you can explain unaided today, and which you still need to study.

## 7. Reproducing the numbers

Run at the baseline commit `f20f799` (the HEAD before the group E documentation commits). At a later HEAD the numbers include the commits added since.

```bash
B=f20f799
git rev-list --count $B                                                       # 502 commits
git log $B --grep="^Co-Authored-By: Claude" --format=%H | wc -l               # 379 commits with a Claude trailer
git log $B --format=%B | grep "^Co-Authored-By: Claude" | sort | uniq -c | sort -rn   # per model (one trailer per commit)
git log $B --format=%ad --date=short | sort | sed -n '1p;$p'                  # 2026-05-08 .. 2026-11-12
git log $B --format='%an' | sort | uniq -c                                    # author names: 448 and 54
# section 2 path table (file touches in AI-co-authored commits)
git log $B --grep="^Co-Authored-By: Claude" --name-only --format= \
  | sed -E 's#^((cinemabe/Cinema/[^/]+|cinemafe/projects/[^/]+|[^/]+)).*#\1#' | sort | uniq -c | sort -rn | head -14
# first commit that added a symbol, with its trailer
git log -S"FinalizePaidInvoiceAsync" --reverse --format='%h %ad %s | %(trailers:key=Co-Authored-By,valueonly)' --date=short -- cinemabe | head -1
```
