# Requirements

> Purpose: What the system currently lets each user type do, derived from the live API surface and role model.
> Sources: `docs/03-technical/api-spec.md`, `docs/01-business/roles-permissions.md`, `docs/01-business/business-rules.md`, `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/CinemaController.cs`
> Last reviewed: 2026-11-12

This is a capability inventory of what exists today (verified against code), not a product backlog. Items the business docs call out as removed are listed under "Explicitly not supported" so they aren't mistaken for gaps to fill.

## 1. Customer-facing capabilities (CinemaUser)

| Area | Capability | Backing endpoint(s) |
|---|---|---|
| Account | Register, log in (password/Google/Facebook), 2FA (email/SMS OTP), forgot/reset password, verify email, manage notification preferences, change password | `IdentityController.*` |
| Discovery | Browse now-showing/coming-soon/recommended movies, view movie detail + external ratings (OMDb), browse theaters (incl. by movie), view showtimes | `CinemaController` Movies/Theaters/ShowTimes |
| Reviews | Rate and comment on a movie, but only after watching it (a Paid invoice for a past showtime of that movie) | `CinemaController.RateMovie`/`AddComment` |
| Promotions | View published promotions, see the active splash banner once per browser session | `GetPublishedPromotionPrograms`, `GetActiveSplashBanner` |
| Booking | View seat map + live seat-lock state (SignalR), view prices, lock/unlock a seat in real time, apply a discount code / gift card / loyalty points, create a booking, pay via a gateway (VNPay/Sandbox), poll invoice status post-redirect | `PaymentController.*`, `BookingHub` |
| Post-purchase | View own invoices/tickets, see e-ticket confirmation (email/SMS) | `GetMyInvoices`, `GetInvoice` |
| Profile | Update profile, set notification preferences | `IdentityController.UpdateProfile`/`UpdateNotificationPreferences` |

### Explicitly not supported (Customer)
- Self-service refund or cancellation of a `Paid` booking — no refund feature exists in current code.
- Self-service cancellation of a still-Pending booking — `CancelBookingAsync`/`PaymentController.CancelBooking` were removed outright; a Pending booking now only clears via the 15-min stale-booking reaper (`ExpireStalePendingBookingsAsync`), not customer action.
- Purchasing a gift card as a customer — gift cards are admin-issued only.

## 2. Theater staff capabilities (CinemaStaff)

| Area | Capability | Backing endpoint(s) / role |
|---|---|---|
| Box office | Quote and ring up a counter sale (always `Paid` at creation), look up a customer by phone, see today's showtimes for the theater | `RoleNames.Sellers` |
| Gate | Scan a QR ticket for admission, look up today's paid tickets by invoice code/phone | `RoleNames.GateKeepers` |
| Operations | View the schedule board, report an incident, view/resolve incidents (resolve-with-unblock needs `Approvers`) | `RoleNames.StaffApp` (+`Approvers` for unblock) |
| Seat map | View (not edit) the room seat map | `RoleNames.StaffApp` |

### Explicitly not supported (TheaterStaff)
- Cash drawer close / daily close, time clock, tasks/checklists, inventory/storage-plan management, customer-service/after-sales desk, refund/exchange UI (exchange field `ExchangedFromInvoiceId` still exists on `Invoice`, but no exchange workflow UI was found), kitchen pickup queue / low-stock alerts (removed along with `RoleNames.Concession`) — all confirmed removed from `CinemaStaff`'s current feature tree and sidebar.
- Running sales/occupancy/KPI reports (`StaffReport` region is Admin-only despite the name).
- Any catalog/pricing/theater/movie/promotion write — all gated `Admin` literal, not `TheaterStaff`.

## 3. Admin capabilities (CinemaAdmin)

| Area | Capability |
|---|---|
| Catalog | Full CRUD: movies (incl. cast/crew credits, genres), theaters, rooms, room types (+ patron-category price overrides), seat types (rename/recolor only — no create/delete), patron categories, time slots, ticket-price matrix, showtimes, age restrictions, movie types, holidays, people (cast/crew), food & drink (incl. combos) |
| Pricing & promotions | Manage discounts (code-based), promotion programs (fixed-price/percent-off, schedule-scoped, auto-apply rules), per-theater loyalty rates (default 1,000 VND = 1 point; 100 points = 1,000 VND) |
| Content ops | Moderate comments (approve/reject/delete), manage news, manage notification templates, manage splash banners, upload images |
| Users | List/create/update/delete users, assign roles (theater-scoped roles require a valid theater) |
| Invoices | List invoices, override invoice status (constrained transitions; see business-rules), delete an invoice (restores food stock, redeemed points and gift-card balance if it was Pending), view revenue reports (by day/movie/theater) |
| Gift cards | Issue, list, activate/deactivate gift cards |
| Staff/ops oversight | Everything TheaterStaff can do, plus: run sales/occupancy/KPI reports, view audit log, approve seat/room blocking and POS price overrides |

### Explicitly not supported (Admin)
- Issuing a refund or reversing a `Paid` invoice's financial state through any dedicated mechanism — only `UpdateInvoiceStatus` exists; moving a `Paid` invoice to `Cancelled/Failed` frees its seats but doesn't refund points, gift-card spend or touch any gateway.
- Server-side CSV/report file export — reports return JSON; CSV is built client-side in the Admin UI.

## 4. Cross-cutting / platform requirements

- All list/search reads go through `POST` + `PagingSearchDTO` + `DefaultSearchResults<T>`, even though they're reads.
- Every payment gateway is callback-authoritative — a browser redirect never confirms payment; only a signed server-to-server callback does.
- Seat-lock state is process-local (in-memory); the only durable anti-double-booking guarantee is a DB-level unique index plus the 15-minute pending-invoice reaper.
- Role/scope mismatches on an authenticated request always return 403, never 401 (401 is reserved for "no valid token," which force-logs-out the frontend client).


## 5. Quantified non-functional requirements

Every value in the first two tables is read from code or configuration and cites its file:line. The third table lists the figures a reader would expect and that nobody has measured; they are left as open input, not estimated.

### 5.1 Enforced by the application

| ID | Category | Requirement | Value | Source (file:line) |
|---|---|---|---|---|
| NFR-01 | Security | Login, Register, ResetPassword, ConfirmEmail, GoogleLogin, FacebookLogin and VerifyTwoFactor are rate limited per client | 10 requests per 1 minute (fixed window, no queue; overflow answers 429) | `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Helpers/RateLimitPolicies.cs:46-47`; applied in `IdentityController.cs:32`, `:49`, `:83`, `:100`, `:134`, `:151`, `:168` |
| NFR-02 | Security | ForgotPassword and ResendVerification (they send an email) are rate limited more tightly | 3 requests per 15 minutes | `RateLimitPolicies.cs:55-56`; applied in `IdentityController.cs:66`, `:117` |
| NFR-03 | Security | Account lockout after consecutive failed sign-ins (wrong 2FA codes count too) | 5 failures, then locked for 15 minutes | `cinemabe/Cinema/2-Business/Cinema.Business/Managers/Auth/AuthManager.cs:42-43` |
| NFR-04 | Security | Access token lifetime, no refresh and no revocation | 24 hours (`JWT:ExpiryHours`, default 24) | `cinemabe/Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs:50`; `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/appsettings.json:19` |
| NFR-05 | Security | Two-factor code lifetime (6 digits, SHA-256 hashed, single use) | 5 minutes | `AuthManager.cs:221` |
| NFR-06 | Security | Password storage cost | PBKDF2-SHA256, 100000 iterations | `cinemabe/Cinema/2-Business/Cinema.Business/Security/PasswordHasher.cs:14` |
| NFR-07 | Security | Minimum password length at registration | 8 to 100 characters | `cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RegisterRequest.cs:21` |
| NFR-08 | Data integrity | Advisory seat lock lifetime (in memory, one process) | 5 minutes, not renewed by re-locking | `cinemabe/Cinema/2-Business/Cinema.Business/Managers/BookingManager.cs:1883` |
| NFR-09 | Data integrity | A Pending (unpaid) booking holds its seats, then the reaper cancels it | 15 minutes; the reaper runs every 1 minute | `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Services/PendingBookingReaper.cs:12-13` |
| NFR-10 | Data integrity | Extra hold for a booking whose payment was started (matches the VNPay payment page life) | +15 minutes, so up to 30 minutes in all | `BookingManager.cs:1831`; `cinemabe/Cinema/2-Business/Cinema.Business/Payments/VnPayGateway.cs:74` |
| NFR-11 | Data integrity | One seat can be sold to only one active ticket, across server instances | Unique filtered index on `InvoiceTicket(ShowTimeId, RoomId, SeatId)` where `IsActive = 1` | `cinemabe/sql/create_db.sql:717` |
| NFR-12 | Operations | Gate admission window before the showtime starts (configurable `Gate:AdmitBeforeMinutes`); a movie with minimum age 16 or more prompts an age check | 30 minutes before start; age prompt from 16 | `cinemabe/Cinema/2-Business/Cinema.Business/Managers/GateManager.cs:17-18` |
| NFR-13 | Operations | Maximum date range of a sales, occupancy or KPI report | 92 days | `cinemabe/Cinema/2-Business/Cinema.Business/Managers/StaffReportManager.Sales.cs:11` |
| NFR-14 | Performance | External movie-ratings call (OMDb) must not stall a page | HTTP timeout 3 seconds; cached 24 hours when found, 6 hours when not found, 10 minutes on failure | `cinemabe/Cinema/2-Business/Cinema.Business/DependencyInjection.cs:25`; `cinemabe/Cinema/2-Business/Cinema.Business/ExternalRatings/OmdbRatingsProvider.cs:30-32` |
| NFR-15 | Performance | Largest page a search can return | 100 showtime rows, 50 comments | `cinemabe/Cinema/2-Business/Cinema.Business/Managers/MovieManager.cs:141`, `:386` |
| NFR-16 | Operations | Showtime reminder emails | Job runs every 5 minutes, reminds about 60 minutes before start, scans a 15 minute band | `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Services/ShowtimeReminderService.cs:18-20` |
| NFR-17 | Correctness | The business-day calendar uses Vietnam time | UTC+7 (`Asia/Ho_Chi_Minh`, fixed-offset fallback when the OS time zone is missing) | `cinemabe/Cinema/2-Business/Cinema.Business/Helpers/BusinessCalendar.cs:31` |

### 5.2 Enforced by the build pipeline

| ID | Category | Requirement | Value | Source (file:line) |
|---|---|---|---|---|
| NFR-18 | Quality | Line coverage of the `Cinema.Business` package may not drop below the gate | 60% (raise it, never lower it) | `.github/workflows/ci.yml:49`; `.github/scripts/check-coverage.py` |
| NFR-19 | Quality | Backend analyzer warnings may not grow past the baseline | Baseline 641 warnings, at most 5% growth | `.github/workflows/ci.yml:28`; `.github/scripts/analyzer-baseline.txt:1` |
| NFR-20 | Quality | Frontend lint warnings may not grow past the cap | At most 1437 warnings, any error fails | `cinemafe/package.json:11` |

### 5.3 Not measured

| Quality | Figure | Status |
|---|---|---|
| Response time of the booking and payment endpoints (for example p95) | none | Student input needed: not measured. No load or latency test exists in the repository. |
| Number of concurrent users or concurrent seat selections the system supports | none | Student input needed: not measured. The seat lock is process-local (`BookingManager.cs:1883`), so the one-process limit has never been tested against real load. |
| Availability or uptime target | none | Student input needed: not measured, and no deployment pipeline exists (`docs/05-process/deployment.md`). |
| Recovery objectives (backup frequency, restore time) | none | Student input needed: not measured, and no backup procedure is recorded in the repository. |
| Browser and device support matrix beyond the responsive rule in `AGENTS.md` (phone, small tablet, tablet widths) | none | Student input needed: which browsers and devices you tested. |
