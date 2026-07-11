# Cinema — Feature Roadmap

Backlog for the multi-theater cinema platform. Status reflects the codebase as of the
`feature/multi-theater-catalog-pricing` branch.

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
| **Facebook login** | 🆕 | S | Mirror the Google flow: add a Facebook token validator + `AuthManager` path + FE button. Need a Facebook App ID/secret. |
| **Role-based access (admin / theater staff / customer)** | 🟡 | M | Admin + Customer roles exist; **theater staff** role is new — decide staff permissions (which theater(s) they manage, which tabs they see). |

## Booking & tickets
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Seat hold/lock during checkout | ✅ | — | Static `ConcurrentDictionary` in `BookingManager` (process-local). |
| Real-time seat availability | ✅ | — | SignalR `BookingHub`. |
| **Order food/drink combos with tickets** | 🟡 | M | `InvoiceFoodAndDrink` join exists; wire into the booking flow + UI (add items to a booking, sum totals). |
| **Manage booked tickets (view / cancel)** | 🟡 | M | Invoices exist; add customer "my tickets" view + cancel flow. Depends on cancellation policy below. |
| **E-tickets + QR codes** | 🆕 | M | `InvoiceTicket.QrCode` column exists; generate a QR (booking ref) + render on ticket + a scan/validate endpoint. |
| **Cancellation / exchange policy** | 🆕 | M | Rules for refunds/showtime changes (time cutoffs, fees). Needs policy decisions before build. |
| **Payment gateway (VNPay/MoMo/Stripe)** | 🟡 | L | `PaymentController` + `SandboxPaymentGateway` abstraction exist; implement a real provider. Pick provider(s); handle callbacks/webhooks + PCI scope (prefer hosted redirect). |
| Order confirmation email/SMS | 🆕 | M | Overlaps with reminders — see messaging infra below. |

## Discovery & engagement
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| **Ratings & reviews** | 🟡 | S–M | `Evaluation` (Score + Review) and threaded `Comment` entities + stores exist — **not surfaced**. Add managers/endpoints/UI. Decide: only buyers can review? moderation? |
| **Movie recommendations by preference** | 🆕 | L | Needs a signal source (ratings, genres watched) + algorithm (start simple: by favorite genres / top-rated). |
| **Search & filter movies/showtimes** (theater, date, time, genre, language) | 🟡 | M | Some search exists (`GetTheatersByMovie`); build a unified filter API + UI. Confirm "language" = movie audio/subtitle field (add if missing). |
| **Nearest theater by location** | 🆕 | M | Theaters have address/city only — add lat/long + a distance sort (Haversine). Needs geocoding of theaters + browser geolocation. |

## Membership & promotions
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| **Membership / points / vouchers** | 🟡 | M | `MemberShip` tiers + `Discount`/`DiscountType` exist; wire point accrual on purchase, tier upgrades, voucher redemption at checkout. |
| **Promotions scoped system-wide OR per-theater** | 🆕 | M | Add an optional `TheaterId` to `Discount` (null = global). Enforce scope at redemption. |

## Admin & reporting
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| Dashboard | ✅ | — | Basic dashboard exists. |
| **Revenue statistics & detailed reports** | 🆕 | M | Revenue by movie / theater / time slot, ticketing trends. Define report set + date ranges + charts. |
| **Advanced stats dashboard** | 🆕 | M | Extends the above with richer charts. |
| **Dark mode (admin)** | 🆕 | S | Theme toggle; admin already uses CSS variables (`--ad-*`). |

## Platform / infra
| Feature | Status | Size | Notes / open questions |
|---|---|---|---|
| **Messaging (email/SMS)** | 🆕 | M | Underpins order confirmation + showtime reminders. `INotificationService` (`DevLogNotificationService`) abstraction exists — implement a real email provider + SMS provider. Pick providers. |
| **Showtime reminders** | 🆕 | M | Depends on messaging; needs a scheduler (e.g. background job) to send before showtime. |
| **Multilingual (i18n)** | 🆕 | L | Angular i18n or a runtime translation lib across CinemaUser + CinemaAdmin. Decide languages + approach. |
| **Support chatbot** | 🆕 | L | Decide: rule-based FAQ vs LLM-backed; scope of questions; data it can access. |

---

## Suggested next steps (small, high-value, low-dependency)
1. **Ratings & reviews** (🟡 S–M) — entities already exist; fast win.
2. **Facebook login** (🆕 S) — mirrors the done Google flow.
3. **Order food/drink combos with tickets** (🟡 M) — completes the booking value chain.
4. **Messaging infra** (🆕 M) — unblocks order confirmation + reminders.

Larger tracks (payments, recommendations, i18n, chatbot) are best scheduled on their own.
