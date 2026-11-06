# Roadmap

> Purpose: Feature backlog and status for the multi-theater cinema platform, restructured from the repo-root roadmap.
> Sources: E:\cinema\ROADMAP.md
> Last reviewed: 2026-11-06

Status reflects the codebase as of the `feature/multi-theater-catalog-pricing` branch (per
`ROADMAP.md`). See `docs/00-overview/open-questions.md` for a note on this roadmap possibly being
stale relative to later work (e.g. the staff role).

**Legend** — done · partial (data layer exists, needs business/API/UI) · new
Size: S (~half day) · M (1-2 days) · L (3+ days / infra)

## Recently shipped (multi-theater-catalog-pricing branch)

- Multi-theater catalog: seat types, food & drinks, rooms are per-theater.
- Theater detail page with tabs (info, rooms, room types, seat types, food, time slots, ticket prices).
- Ticket pricing: explicit price by room type x seat type x time slot x holiday, per theater.
- Screening room types (2D/3D/IMAX/4DX) per theater.
- Seat-map editor with add/remove rows & columns.

> Pending action noted in `ROADMAP.md`: these schema changes require a DB reset & reseed
> (`create_db.sql` + `insert_db.sql`) before running.

## Auth & accounts

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Google login | Done | - | `AuthManager` + `GoogleTokenValidator`; OTP/2FA, email verify, lockout also done. |
| Facebook login | New | S | Mirror the Google flow: add a Facebook token validator + `AuthManager` path + FE button. Needs a Facebook App ID/secret. |
| Role-based access (admin / theater staff / customer) | Done | - | **Note**: this roadmap entry is stale — `TheaterStaff` is already a live third role (`RoleNames.cs`, `UserType.Name`), always theater-scoped via a `theaterId` JWT claim, with a full box-office/gate/workforce feature set wired into `CinemaController` and the `CinemaStaff` app. See `docs/01-business/roles-permissions.md` for the full role model and capability composites (see open questions for the stale-roadmap discrepancy). |

## Booking & tickets

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Seat hold/lock during checkout | Done | - | Static `ConcurrentDictionary` in `BookingManager` (process-local). |
| Real-time seat availability | Done | - | SignalR `BookingHub`. |
| Order food/drink combos with tickets | Partial | M | `InvoiceFoodAndDrink` join exists; wire into the booking flow + UI (add items to a booking, sum totals). |
| Manage booked tickets (view / cancel) | Partial | M | Invoices exist; add customer "my tickets" view + cancel flow. Depends on cancellation policy below. |
| E-tickets + QR codes | New | M | `InvoiceTicket.QrCode` column exists; generate a QR (booking ref) + render on ticket + a scan/validate endpoint. |
| Payment gateway (VNPay/MoMo/Stripe) | Partial | L | `PaymentController` + `SandboxPaymentGateway` abstraction exist; implement a real provider. Pick provider(s); handle callbacks/webhooks + PCI scope (prefer hosted redirect). |
| Order confirmation email/SMS | New | M | Overlaps with reminders — see messaging infra below. |

## Discovery & engagement

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Ratings & reviews | Partial | S-M | `Evaluation` (Score + Review) and threaded `Comment` entities + stores exist — not surfaced. Add managers/endpoints/UI. Decide: only buyers can review? moderation? |
| Movie recommendations by preference | New | L | Needs a signal source (ratings, genres watched) + algorithm (start simple: by favorite genres / top-rated). |
| Search & filter movies/showtimes (theater, date, time, genre, language) | Partial | M | Some search exists (`GetTheatersByMovie`); build a unified filter API + UI. Confirm "language" = movie audio/subtitle field (add if missing). |
| Nearest theater by location | Done | - | `Theater.Latitude`/`Longitude` (manually entered per theater in CinemaAdmin, no geocoding API) + browser geolocation + Haversine distance sort on CinemaUser's theaters list. |

## Membership & promotions

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Membership / points / vouchers | Partial | M | `MemberShip` tiers + `Discount`/`DiscountType` exist; wire point accrual on purchase, tier upgrades, voucher redemption at checkout. |
| Promotions scoped system-wide OR per-theater | New | M | Add an optional `TheaterId` to `Discount` (null = global). Enforce scope at redemption. |

## Admin & reporting

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Dashboard | Done | - | Basic dashboard exists. |
| Revenue statistics & detailed reports | New | M | Revenue by movie / theater / time slot, ticketing trends. Define report set + date ranges + charts. |
| Advanced stats dashboard | New | M | Extends the above with richer charts. |
| Dark mode (admin) | New | S | Theme toggle; admin already uses CSS variables (`--ad-*`). |

## Platform / infra

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Messaging (email/SMS) | New | M | Underpins order confirmation + showtime reminders. `INotificationService` (`DevLogNotificationService`) abstraction exists — implement a real email provider + SMS provider. Pick providers. |
| Showtime reminders | New | M | Depends on messaging; needs a scheduler (e.g. background job) to send before showtime. |
| Multilingual (i18n) | New | L | Angular i18n or a runtime translation lib across CinemaUser + CinemaAdmin. Decide languages + approach. |
| Support chatbot | Done | - | Rule-based, no LLM/API key: keyword-matched against a canned FAQ (booking, showtimes, cancel, membership, nearby theater, payment, combo) in `CinemaUser/shared/chatbot`. |

## Suggested next steps (small, high-value, low-dependency)

1. Ratings & reviews (Partial, S-M) — entities already exist; fast win.
2. Facebook login (New, S) — mirrors the done Google flow.
3. Order food/drink combos with tickets (Partial, M) — completes the booking value chain.
4. Messaging infra (New, M) — unblocks order confirmation + reminders.

Larger tracks (payments, recommendations, i18n) are best scheduled on their own.
