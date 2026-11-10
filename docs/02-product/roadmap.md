# Roadmap

> Purpose: Feature backlog and status for the multi-theater cinema platform, restructured from the repo-root roadmap.
> Sources: E:\cinema\ROADMAP.md
> Last reviewed: 2026-11-06

Status verified directly against the code on 2026-11-06.

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
| Facebook login | Done | - | Implemented end-to-end (`login.component.ts`'s `loginWithFacebook()`, backend `IdentityController.FacebookLogin`) but gated behind `facebookEnabled = !!environment.facebookAppId`; both env files ship an empty App ID, so the button stays hidden until a real Facebook App ID/secret is configured. |
| Role-based access (admin / theater staff / customer) | Done | - | **Note**: this roadmap entry is stale — `TheaterStaff` is already a live third role (`RoleNames.cs`, `UserType.Name`), always theater-scoped via a `theaterId` JWT claim, with a full box-office/gate/workforce feature set wired into `CinemaController` and the `CinemaStaff` app. See `docs/01-business/roles-permissions.md` for the full role model and capability composites (see open questions for the stale-roadmap discrepancy). |

## Booking & tickets

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Seat hold/lock during checkout | Done | - | Static `ConcurrentDictionary` in `BookingManager` (process-local). |
| Real-time seat availability | Done | - | SignalR `BookingHub`. |
| Order food/drink combos with tickets | Done | - | `InvoiceFoodAndDrink` is wired into `booking-checkout.component.ts` end-to-end (out-of-stock handling included) — this roadmap entry was stale. |
| Manage booked tickets (view / cancel) | Done | - | `profile.component.ts`'s `cancelBooking`/`cancelById` via `PaymentServiceAgent.cancelBooking` — this roadmap entry was stale. |
| E-tickets + QR codes | Done | - | Rendered inline in `profile.component.ts` (`qrMap`/`_buildQrCodes`), not via the `CinemaLib` ticket-card/qr-code components (those are dead code in CinemaUser, used only by CinemaStaff's POS receipt) — this roadmap entry was stale. |
| Payment gateway (VNPay/MoMo/Stripe) | Partial | L | VNPay and MoMo are both fully wired (backend gateway + selectable checkout option); `StripeGateway.cs` exists and is DI-registered but has no frontend entry point in `booking-checkout.component.ts` yet. |
| Order confirmation email/SMS | New | M | Overlaps with reminders — see messaging infra below. |

## Discovery & engagement

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Ratings & reviews | Done | - | Surfaced in `movie-detail.component.ts`: star rating widget, threaded comments (`recentComments`/`replies`), `canReview` gate; comments go through admin moderation — this roadmap entry was stale. |
| Movie recommendations by preference | Done | - | `CinemaController.GetRecommendedMovies` → `MovieManager.GetRecommendedAsync`, personalized for signed-in users (top-rated for anonymous), wired into CinemaUser's home page. |
| Search & filter movies/showtimes (theater, date, time, genre, language) | Partial | M | Some search exists (`GetTheatersByMovie`); build a unified filter API + UI. Confirm "language" = movie audio/subtitle field (add if missing). |
| Nearest theater by location | Done | - | `Theater.Latitude`/`Longitude` (manually entered per theater in CinemaAdmin, no geocoding API) + browser geolocation + Haversine distance sort on CinemaUser's theaters list. |

## Membership & promotions

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Membership / points / vouchers | Done | - | `MemberShip` tiers + `PromotionProgram` (code/auto-apply, merged from the former `Discount`/`DiscountType`) exist; point accrual on purchase and tier upgrades are wired (`BookingManager.ApplyPaidSideEffectsAsync`). |
| Promotions scoped system-wide OR per-theater | Done | - | `PromotionProgramSchedule` rows scope a program to specific theaters/days/times (zero rows = system-wide); enforced at redemption in `BookingManager.MatchesProgramScope`. |

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

1. Messaging infra (New, M) — unblocks order confirmation + reminders.
2. Search & filter movies/showtimes (Partial, M) — some search exists, build a unified filter UI.
3. Revenue statistics & detailed reports (New, M) — basic reports exist, add the full report set.

Ratings & reviews, Facebook login, food/drink combos, movie recommendations, nearest theater, and the support chatbot are all already done. Larger tracks (payments, i18n) are best scheduled on their own.
