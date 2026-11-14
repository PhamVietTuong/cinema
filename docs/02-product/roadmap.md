# Roadmap

> Purpose: Feature backlog and status for the multi-theater cinema platform, restructured from the repo-root roadmap.
> Sources: E:\cinema\ROADMAP.md
> Last reviewed: 2026-11-14

Status verified directly against the code on 2026-11-14.

**Legend** — done · partial (data layer exists, needs business/API/UI) · new
Size: S (~half day) · M (1-2 days) · L (3+ days / infra)

## Recently shipped

- Market-fit fixes (2026-11-14): age rating enforced online and at the gate (confirmation from 13, K-rated guardian banner), online sale checks (open showtime, per-theater `MaxSeatsPerBooking`), ID-check categories counter-only online.
- Discount simplification (2026-11-14): one `PromotionProgram` per invoice plus points redemption; no tier percentage, no gift cards.
- Per-theater promotion cap (2026-11-14): `MaxPromotionDiscountPercent` (0-50) and `MaxHolidayPromotionDiscountPercent` (0-100) per theater.
- Late-night price seed 0.85 with an admin warning (2026-11-14).
- Consent and age-attestation records in `AuditLog`, promotional email opt-in (2026-11-14).
- Multi-theater catalog: seat types, food & drinks, rooms are per-theater; theater detail page with tabs; ticket pricing by room type x seat type x time slot x holiday; screening room types; seat-map editor.
- Docs cleanup (2026-11-14): topical docs describe only the current system, removals are logged in `docs/05-process/history.md`, `market-research.md` filled.

## Open priority items

| Item | Status | Size | Notes |
|---|---|---|---|
| E-invoice and VAT | New | L | No tax invoice or VAT breakdown exists. Rates and rules are not researched yet (`01-business/market-research.md`, `UNVERIFIED`). |
| Data retention and anonymisation | New | M | No retention rule for phone, email, points or `AuditLog`; no self-service removal for a customer (an Admin can delete a user through `IdentityController.DeleteUser`). Consent evidence already exists. |
| VIP seat kinds | New | M | `SeatKind` has `Standard`, `Double` and `Aisle`; no VIP kind. Scope not defined. |
| Holiday pricing review | New | S | Holiday pricing is unchanged (a `Holiday` multiplier). The owner parked mapping holidays to the weekend row or per-holiday per-slot prices; revisit later. |
| Minimum-discount groups (Decree 131/2022 article 10) | New | M | Not enforced: nothing forces at least 20% off for the listed groups. See `01-business/market-research.md`. |
| Test hardening (deferred) | Partial | M | Database-backed tests for the new store paths and the `Pending -> Paid` transition claim, host-level startup tests for the SMTP/Twilio sections, Playwright specs for reviews/filters, a CI check that upgrades an old schema, a layer-reference architecture test, an atomic `BookingHub.LockSeat`, a `FallbackPolicy` requiring authentication. The full list is in `_bmad-output/implementation-artifacts/deferred-work.md`. |

## Auth & accounts

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Google login | Done | - | `AuthManager` + `GoogleTokenValidator`; OTP/2FA, email verify, lockout also done. |
| Facebook login | Done | - | Implemented end-to-end (`login.component.ts`'s `loginWithFacebook()`, backend `IdentityController.FacebookLogin`) but gated behind `facebookEnabled = !!environment.facebookAppId`; both env files ship an empty App ID, so the button stays hidden until a real Facebook App ID/secret is configured. |
| Role-based access (admin / theater staff / customer) | Done | - | `TheaterStaff` is the third role (`RoleNames.cs`, `UserType.Name`), always theater-scoped via a `theaterId` JWT claim, with a box-office/gate/operations feature set in `CinemaController` and the `CinemaStaff` app. See `docs/01-business/roles-permissions.md`. |

## Booking & tickets

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Seat hold/lock during checkout | Done | - | Static `ConcurrentDictionary` in `BookingManager` (process-local). |
| Real-time seat availability | Done | - | SignalR `BookingHub`. |
| Order food/drink combos with tickets | Done | - | `InvoiceFoodAndDrink` is wired into `booking-checkout.component.ts` end-to-end (out-of-stock handling included). |
| View booked tickets | Done | - | Booking history in `profile.component.ts`; customers cannot cancel (only the 15-minute reaper clears a Pending invoice). |
| E-tickets + QR codes | Done | - | Rendered inline in `profile.component.ts` (`qrMap`/`_buildQrCodes`), not via the `CinemaLib` ticket-card/qr-code components (those are dead code in CinemaUser, used only by CinemaStaff's POS receipt). |
| Payment gateway (VNPay) | Done | L | VNPay is the only real gateway and the only checkout option. |
| Order confirmation email/SMS | New | M | Overlaps with reminders — see messaging infra below. |

## Discovery & engagement

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Ratings & reviews | Done | - | Score is validated 1-10 (400), comments are 1-2000 chars, replies attach one level deep to an approved top-level comment of the same movie, and each user has one rating per movie. A star click keeps the saved review. CinemaUser shows "No ratings yet" (no ring), the viewer's own rating and private review (`myRating`/`myReview`), a reply form, "load more" comments (`GetMovieComments`) and edit/delete of own comments (`UpdateOwnComment`/`DeleteOwnComment`). Only users who watched the movie may rate/comment/reply (403 otherwise, read-only UI). Comments go through admin moderation. |
| Movie recommendations by preference | Done | - | `CinemaController.GetRecommendedMovies` → `MovieManager.GetRecommendedAsync`, personalized for signed-in users (top-rated for anonymous), wired into CinemaUser's home page. |
| Search & filter movies/showtimes (theater, date, time, genre, language) | Done | M | Done: `GetMovies` filters server-side by genre (`movieTypeId`), `language`, `theaterId`, `date` and `status` (now/coming) across all pages; `SearchShowTimes` searches showtimes across movies with `theaterId`/`movieTypeId`/`language`/`date`/`timeFrom`/`timeTo`; `GetMovieLanguages` feeds the language filter. UI: `cl-filter-bar` on CinemaUser `/movies` (incl. a "Showtimes" tab). |
| Nearest theater by location | Done | - | `Theater.Latitude`/`Longitude` (manually entered per theater in CinemaAdmin, no geocoding API) + browser geolocation + Haversine distance sort on CinemaUser's theaters list. |

## Membership & promotions

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Membership / points / promotion codes | Done | - | `MemberShip` tiers + `PromotionProgram` (code/auto-apply) exist; point accrual on purchase and tier upgrades are wired (`BookingManager.ApplyPaidSideEffectsAsync`). |
| Promotions scoped system-wide OR per-theater | Done | - | `PromotionProgramSchedule` rows scope a program to specific theaters/days/times (zero rows = system-wide); enforced at redemption in `BookingManager.MatchesProgramScope`. |

## Admin & reporting

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Dashboard | Done | - | Basic dashboard exists. |
| Revenue statistics & detailed reports | Partial | M | Sales, occupancy and KPI reports exist (`modules/overview/reports`); a fuller report set (time slot, ticketing trends) is not defined. |
| Advanced stats dashboard | New | M | Richer charts on top of the reports above. |
| Dark mode (admin) | Done | - | Toggle in the shared `cl-app-shell` backed by `ThemeService`. |

## Platform / infra

| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Messaging (email/SMS) | Partial | M | Providers are implemented but unvalidated against real accounts: `SmtpNotificationService` (email) and `TwilioSmsNotificationService` (SMS) take typed `SmtpOptions`/`TwilioOptions` validated at startup (a half-filled section fails the host start), normalise phone numbers to E.164 (`0900123456` -> `+84900123456`) and enforce send timeouts; unconfigured falls back to the dev-log senders. No outbox/retry queue (a failed send is logged, not retried). |
| Showtime reminders | Done | - | `ShowtimeReminderService` runs every 5 minutes and reminds about 60 minutes before start (`03-technical/architecture.md`, `01-business/requirements.md` NFR-16); delivery depends on the messaging providers above. |
| Multilingual (i18n) | Partial | L | `@ngx-translate/core` with Vietnamese and English files per app; coverage of every screen is not audited. |
| Support chatbot | Done | - | Rule-based, no LLM/API key: keyword-matched against a canned FAQ (booking, showtimes, cancel, membership, nearby theater, payment, combo) in `CinemaUser/shared/chatbot`. |

## Suggested next steps

1. Messaging infra (Partial, M): exercise the SMTP/Twilio providers against real accounts; add an outbox if delivery guarantees are needed.
2. Revenue statistics & detailed reports (Partial, M): define the full report set.
3. The open priority items above, in the owner's order.

Larger tracks (e-invoice and VAT, i18n coverage) are best scheduled on their own.
