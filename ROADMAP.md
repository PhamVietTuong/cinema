# Cinema — Feature Roadmap

Backlog for the multi-theater cinema platform. Status verified directly against the code on
2026-11-06.

**Legend** — ✅ done · 🟡 partial (data layer exists, needs business/API/UI) · 🆕 new
· Size: S (≈½ day) · M (1–2 days) · L (3+ days / infra)

---

## Recently shipped (this branch)
- ✅ Multi-theater catalog: seat types, food & drinks, rooms are **per-theater**.
- ✅ Theater detail page with tabs (info, rooms, room types, seat types, food, time slots, ticket prices).
- ✅ Ticket pricing: explicit price by **room type × seat type × time slot × holiday**, per theater.
- ✅ Screening room types (2D/3D/IMAX/4DX) per theater.
- ✅ Seat-map editor with add/remove rows & columns.

> ⚠️ **Pending action:** these schema changes require a DB reset & reseed
> (`create_db.sql` + `insert_db.sql`) before running.

---

## Auth & accounts
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Google login | ✅ | — | `AuthManager` + `GoogleTokenValidator`; OTP/2FA, email verify, lockout also done. |
| **Facebook login** | ✅ | - | Implemented end-to-end (`login.component.ts`'s `loginWithFacebook()`, backend `IdentityController.FacebookLogin`) but gated behind an empty `facebookAppId` in both env files, so the button stays hidden until a real App ID/secret is configured. |
| **Role-based access (admin / theater staff / customer)** | ✅ | - | `TheaterStaff` is a fully live third role (`RoleNames.cs`), always theater-scoped via a `theaterId` JWT claim, with a full box-office/gate/workforce feature set wired into `CinemaController` and the `CinemaStaff` app. |

## Booking & tickets
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Seat hold/lock during checkout | ✅ | — | Static `ConcurrentDictionary` in `BookingManager` (process-local). |
| Real-time seat availability | ✅ | — | SignalR `BookingHub`. |
| **Order food/drink combos with tickets** | ✅ | - | `InvoiceFoodAndDrink` is wired into `booking-checkout.component.ts` end-to-end, including out-of-stock handling. |
| **Manage booked tickets (view / cancel)** | ✅ | - | `profile.component.ts`'s `cancelBooking`/`cancelById` via `PaymentServiceAgent.cancelBooking`. |
| **E-tickets + QR codes** | ✅ | - | Rendered inline in `profile.component.ts` (`qrMap`/`_buildQrCodes`). |
| **Payment gateway (VNPay/MoMo/Stripe)** | 🟡 | L | VNPay and MoMo are both fully wired (backend gateway + selectable checkout option); `StripeGateway.cs` exists and is DI-registered but has no frontend entry point yet. |
| Order confirmation email/SMS | 🆕 | M | Overlaps with reminders — see messaging infra below. |

## Discovery & engagement
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| **Ratings & reviews** | ✅ | - | Surfaced in `movie-detail.component.ts`: star rating widget, threaded comments, `canReview` gate; comments go through admin moderation. |
| **Movie recommendations by preference** | ✅ | - | `CinemaController.GetRecommendedMovies` → `MovieManager.GetRecommendedAsync`, personalized for signed-in users (top-rated for anonymous), wired into CinemaUser's home page. |
| **Search & filter movies/showtimes** (theater, date, time, genre, language) | 🟡 | M | Some search exists (`GetTheatersByMovie`); build a unified filter API + UI. Confirm "language" = movie audio/subtitle field (add if missing). |
| **Nearest theater by location** | ✅ | - | `Theater.Latitude`/`Longitude` (manually entered per theater in CinemaAdmin, no geocoding API) + browser geolocation + Haversine distance sort on CinemaUser's theaters list. |

## Membership & promotions
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| **Membership / points / vouchers** | 🟡 | M | `MemberShip` tiers + `Discount`/`DiscountType` exist; wire point accrual on purchase, tier upgrades, voucher redemption at checkout. |
| **Promotions scoped system-wide OR per-theater** | 🟡 | M | `Discount` already has this (`ApplyToAllTheaters`/`DiscountTheaters`); `PromotionProgram` does not yet — easy to conflate since both live in CinemaAdmin's Operations module. |

## Admin & reporting
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Dashboard | ✅ | — | Basic dashboard exists. |
| **Revenue statistics & detailed reports** | 🆕 | M | Revenue by movie / theater / time slot, ticketing trends. Define report set + date ranges + charts. |
| **Advanced stats dashboard** | 🆕 | M | Extends the above with richer charts. |
| **Dark mode (admin)** | ✅ | - | The shared `cl-app-shell` component (CinemaAdmin's root shell) has a built-in toggle wired to `ThemeService`; `--ad-*`/`--ml-*` CSS variables already have full dark variants. |

## Platform / infra
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| **Messaging (email/SMS)** | 🆕 | M | Underpins order confirmation + showtime reminders. `INotificationService` (`DevLogNotificationService`) abstraction exists — implement a real email provider + SMS provider. Pick providers. |
| **Showtime reminders** | 🆕 | M | Depends on messaging; needs a scheduler (e.g. background job) to send before showtime. |
| **Multilingual (i18n)** | 🆕 | L | Angular i18n or a runtime translation lib across CinemaUser + CinemaAdmin. Decide languages + approach. |
| **Support chatbot** | ✅ | L | Rule-based (no LLM/API key), keyword-matched against a canned FAQ in `CinemaUser/shared/chatbot`. |

---

## Suggested next steps (small, high-value, low-dependency)
1. **Messaging infra** (🆕 M) — unblocks order confirmation + reminders.
2. **Search & filter movies/showtimes** (🟡 M) — some search exists, build a unified filter UI.
3. **Revenue statistics & detailed reports** (🆕 M) — basic reports exist, add the full report set.

Ratings & reviews, Facebook login, food/drink combos, movie recommendations, nearest theater, and the support chatbot are all already done. Larger tracks (payments, i18n) are best scheduled on their own.
