# Requirements

> Purpose: What the system currently lets each user type do, derived from the live API surface and role model.
> Sources: `docs/03-technical/api-spec.md`, `docs/01-business/roles-permissions.md`, `docs/01-business/business-rules.md`, `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/CinemaController.cs`
> Last reviewed: 2026-11-04

This is a capability inventory of what exists today (verified against code), not a product backlog. Items the business docs call out as removed are listed under "Explicitly not supported" so they aren't mistaken for gaps to fill.

## 1. Customer-facing capabilities (CinemaUser)

| Area | Capability | Backing endpoint(s) |
|---|---|---|
| Account | Register, log in (password/Google/Facebook), 2FA (email/SMS OTP), forgot/reset password, verify email, manage notification preferences, change password | `IdentityController.*` |
| Discovery | Browse now-showing/coming-soon/recommended movies, view movie detail + external ratings (OMDb), browse theaters (incl. by movie), view showtimes | `CinemaController` Movies/Theaters/ShowTimes |
| Reviews | Rate and comment on a movie, but only after watching it (a Paid invoice for a past showtime of that movie) | `CinemaController.RateMovie`/`AddComment` |
| Promotions | View published promotions, see the active splash banner once per browser session | `GetPublishedPromotionPrograms`, `GetActiveSplashBanner` |
| Booking | View seat map + live seat-lock state (SignalR), view prices, lock/unlock a seat in real time, apply a discount code / gift card / loyalty points, create a booking, pay via a gateway (VNPay/MoMo/Stripe/Sandbox), poll invoice status post-redirect, cancel a still-Pending booking | `PaymentController.*`, `BookingHub` |
| Post-purchase | View own invoices/tickets, see e-ticket confirmation (email/SMS) | `GetMyInvoices`, `GetInvoice` |
| Profile | Update profile, set notification preferences | `IdentityController.UpdateProfile`/`UpdateNotificationPreferences` |

### Explicitly not supported (Customer)
- Self-service refund or cancellation of a `Paid` booking — no refund feature exists in current code.
- Purchasing a gift card as a customer — gift cards are admin-issued only.

## 2. Theater staff capabilities (CinemaStaff)

| Area | Capability | Backing endpoint(s) / role |
|---|---|---|
| Box office | Quote and ring up a counter sale (always `Paid` at creation), look up a customer by phone, see today's showtimes for the theater | `RoleNames.Sellers` |
| Gate | Scan a QR ticket for admission, look up today's paid tickets by invoice code/phone | `RoleNames.GateKeepers` |
| Concession | View pickup queue, update a food order's status, check low-stock items, look up a pickup by reference | `RoleNames.Concession` |
| Operations | View the schedule board, report an incident, view/resolve incidents (resolve-with-unblock needs `Approvers`) | `RoleNames.StaffApp` (+`Approvers` for unblock) |
| Seat map | View (not edit) the room seat map | `RoleNames.StaffApp` |

### Explicitly not supported (TheaterStaff)
- Cash drawer close / daily close, time clock, tasks/checklists, inventory/storage-plan management, customer-service/after-sales desk, refund/exchange UI (exchange field `ExchangedFromInvoiceId` still exists on `Invoice`, but no exchange workflow UI was found) — all confirmed removed from `CinemaStaff`'s current feature tree and sidebar.
- Running sales/occupancy/KPI reports (`StaffReport` region is Admin-only despite the name).
- Any catalog/pricing/theater/movie/promotion write — all gated `Admin` literal, not `TheaterStaff`.

## 3. Admin capabilities (CinemaAdmin)

| Area | Capability |
|---|---|
| Catalog | Full CRUD: movies (incl. cast/crew credits, genres), theaters, rooms, room types (+ patron-category price overrides), seat types (rename/recolor only — no create/delete), patron categories, time slots, ticket-price matrix, showtimes, age restrictions, movie types, holidays, people (cast/crew), food & drink (incl. combos) |
| Pricing & promotions | Manage discounts (code-based), promotion programs (fixed-price/percent-off, schedule-scoped, auto-apply rules), loyalty settings (global + per-theater override) |
| Content ops | Moderate comments (approve/reject/delete), manage news, manage notification templates, manage splash banners, upload images |
| Users | List/create/update/delete users, assign roles (theater-scoped roles require a valid theater) |
| Invoices | List invoices, override invoice status (no allow-list enforced), delete an invoice (restores food stock if it was Pending), view revenue reports (by day/movie/theater) |
| Gift cards | Issue, list, activate/deactivate gift cards |
| Staff/ops oversight | Everything TheaterStaff can do, plus: run sales/occupancy/KPI reports, view audit log, approve seat/room blocking and POS price overrides |

### Explicitly not supported (Admin)
- Issuing a refund or reversing a `Paid` invoice's financial state through any dedicated mechanism — only the unconstrained `UpdateInvoiceStatus` exists, which doesn't reverse points/gift-card/stock or touch any gateway.
- Server-side CSV/report file export — reports return JSON; CSV is built client-side in the Admin UI.

## 4. Cross-cutting / platform requirements

- All list/search reads go through `POST` + `PagingSearchDTO` + `DefaultSearchResults<T>`, even though they're reads.
- Every payment gateway is callback-authoritative — a browser redirect never confirms payment; only a signed server-to-server callback does.
- Seat-lock state is process-local (in-memory); the only durable anti-double-booking guarantee is a DB-level unique index plus the 15-minute pending-invoice reaper.
- Role/scope mismatches on an authenticated request always return 403, never 401 (401 is reserved for "no valid token," which force-logs-out the frontend client).
