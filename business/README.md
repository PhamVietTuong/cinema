# Cinema Platform — Business Logic Reference

This folder is a **business-rules knowledge base** for the Cinema booking platform, extracted
directly from source (`cinemabe/` .NET 9 backend + `cinemafe/` Angular 21 frontend) as of
2026-10-31, against `master`. It exists so an AI assistant can understand *what the system does
and why* before touching code — **read the relevant file(s) here before planning or implementing
a change.**

This is a documentation layer, not a source of truth for syntax/structure — `CLAUDE.md` at the
repo root still governs conventions (layering, controller rules, query efficiency, etc.). This
folder governs **behavior**: states, transitions, validation rules, and invariants that must not
be broken.

> This is the second generation of this folder. The first was written to disk but never
> committed, and was lost across branch switches before the codebase absorbed a large wave of
> merged work (promotion programs, gift cards, loyalty/points, multi-theater & patron-category
> pricing, OMDb/IMDb matching, booking/seat-lock hardening, a built-then-fully-reverted refund
> feature, and more). This version was regenerated against current `master` and is committed —
> see "Keeping this current" below for why that matters.

## How to use this folder

1. Identify which business area your task touches (table below).
2. Read that file fully before coding — each one is dense/table-based, built to be skimmed by an LLM.
3. Cross-references: `booking.md` pricing math defers to `catalog.md` §5; `payments.md` invoice
   states are defined once in `domain-model.md` and only *triggered* by `payments.md`; `auth.md`
   §4 is the single source of truth for the current role model.
4. If a file says something that looks stale vs. the current code (renamed class, moved endpoint),
   **trust the code** and treat the doc as describing intent/history — then flag it for a refresh.

## Files

| File | Covers | Read this before touching... |
|---|---|---|
| [`domain-model.md`](domain-model.md) | All EF Core entities (fields, relationships, FK cascade behavior, unique indexes), all enums with every value, entity→DTO mapping, schema-level invariants. | Any entity, migration/SQL script change, or new DTO. |
| [`auth.md`](auth.md) | Login (password/Google/Facebook — account auto-link-by-email, 2FA bypass on social login), registration, JWT claims/expiry, the **current** role model (just `Admin` / `Customer` / `TheaterStaff` — staff sub-roles collapsed into capability composites in `RoleNames.cs`), password reset/email verification/2FA (OTP, not TOTP), SignalR token-via-querystring, frontend guards/interceptors, the deliberate 403-not-401 convention. | `IdentityController`, `AuthManager`, any `[Authorize]` attribute, auth guards/interceptors in `CinemaLib`. |
| [`booking.md`](booking.md) | The seat-lock state machine (**confirmed still process-local**, in-memory `ConcurrentDictionary` — no distributed lock exists despite a `distributed-seat-lock` branch name; real cross-instance safety net is a DB filtered-unique-index + a separate 15-min `PendingBookingReaper`), the full booking→payment→ticket lifecycle, every `BookingHub` method/broadcast, double-booking prevention, and hard gotchas. | `BookingManager`, `BookingHub`, seat-selection/checkout UI, anything claiming to fix "seat lock" bugs. |
| [`payments.md`](payments.md) | Invoice status lifecycle, the generic multi-gateway `PaymentCallback` design, the **critical callback-authoritative rule** (VNPay/MoMo/Stripe all hardcode redirect verification to fail — only the signed server-to-server call confirms payment), exact VNPay IPN contract, gift card redemption mechanics, and **refund: built across several commits, then fully reverted — zero refund capability exists today**. | `PaymentController`, any `IPaymentGateway` implementation, invoice status handling, anything assuming refunds work. |
| [`catalog.md`](catalog.md) | Movie/cast-crew/review rules, OMDb/IMDb rating matching, Theater→Room→Seat→PatronCategory hierarchy with two-layer seat-type gating, ShowTime scheduling validation chain, the **exact seat-pricing formula** (room-type price × holiday/weekday factor + 3D surcharge + base price), promotion-program auto-apply scoring, loyalty points earn/redeem, gift card issue/validate/redeem, and every list-endpoint filter key in use. | `MovieManager`, `ShowTimeManager`, `RoomManager`, `PromotionProgramManager`, any pricing/loyalty/gift-card logic. |
| [`admin-staff-and-frontend-flows.md`](admin-staff-and-frontend-flows.md) | **Correction to a prior assumption**: staff/box-office/gate/workforce features were NOT removed — only the 5 granular staff roles were collapsed into one `TheaterStaff` role (refund/drawer-close/time-clock UI *was* removed, matching `payments.md`). Non-catalog `CinemaController` regions (BoxOffice, Gate, Workforce, StaffReport, Operations), the single shared `AuditLog` table, and numbered user-journey flows for all three Angular apps (CinemaUser/Admin/Staff) plus shared `CinemaLib` state. | Staff/admin-facing endpoints, any `CinemaUser`/`CinemaAdmin`/`CinemaStaff` feature component. |

## Load-bearing facts worth repeating here (appear in multiple files)

- **Roles are coarse**: only `Admin`, `Customer`, and `TheaterStaff` exist today. Named "role
  groups" in `RoleNames.cs` (e.g. `Sellers`, `GateKeepers`, `Approvers`) are capability composites
  that all resolve to `Admin` or `Admin,TheaterStaff` — there is no finer-grained staff permission
  system. See `auth.md` §4 and `admin-staff-and-frontend-flows.md`.
- **Seat lock ≠ booking guard, and it is still process-local**: the SignalR seat lock is a
  5-minute *advisory* UI signal held in an in-memory `ConcurrentDictionary` (lost on API restart,
  not safe across multiple API instances — confirmed no Redis/distributed-lock code exists
  anywhere in the backend). The actual anti-double-booking mechanism is a DB-level filtered unique
  index plus the independent 15-minute `PendingBookingReaper` expiry on the invoice. These are two
  separate timers — don't conflate them. See `booking.md` §1.
- **Every payment gateway (VNPay, MoMo, Stripe) is callback-authoritative**: never trust a browser
  redirect to confirm payment — only the signed server-to-server IPN/webhook can mark an invoice
  `Paid`. See `payments.md` §3.
- **There is no refund flow**: it was built (multiple commits), then fully reverted (see
  `payments.md` §6 for the commit trail). `InvoiceStatus` reserves ordinal 4 for a former
  `Refunded` value, explicitly commented "do not reuse." If a task references refunds, it is
  either describing a feature that doesn't exist yet or referring to the plain `Cancelled` status
  (which restores gift-card balance but does not integrate with any payment gateway).
- **No generic `CatalogManager<T>` base class exists** — each catalog manager hand-implements the
  same `GetQuery`/`FilterQuery`/`OrderQuery` + `PagingHelper` convention independently. See
  `catalog.md` §1.
- **Single shared `AuditLog` table**: every audit/log need reuses one table with an `AuditAction`
  discriminator enum (several ordinals explicitly retired/"do not reuse") — never create a new log
  table. See `admin-staff-and-frontend-flows.md` §A.2.
- **Enums are append-only**: several enums carry explicit "never renumber" intent in the domain
  model (values are persisted as integers in the DB) — only add new values at the end, never
  reorder or reuse a retired number. See `domain-model.md`.

## Keeping this folder current

These files are a snapshot, not a live view. When a change you make alters a state machine,
validation rule, role mapping, enum meaning, or pricing/promotion formula documented here,
**update the relevant `.md` file in the same commit** — treat it like updating a test: the doc is
wrong the moment behavior diverges from it, and a stale business doc is worse than no doc for a
future AI reading it first.

**This folder must be committed to git**, not left as working-tree state. The first generation of
this documentation was lost precisely because it was written to disk but never committed, and
disappeared across branch switches. Always `git add business/` alongside any change that touches
it.
