# User Stories

> Purpose: Capabilities of `docs/01-business/requirements.md` restated as user stories, grouped by role.
> Sources: `docs/03-technical/api-spec.md`, `docs/01-business/roles-permissions.md`, `docs/01-business/requirements.md`, `docs/01-business/business-rules.md`
> Last reviewed: 2026-11-04

Format: `As a <role>, I want to <capability>, so that <value>.` Each story maps to a capability that already exists in the current system (verified against code) — this is a description of current behavior, not a backlog of unbuilt features.

## Customer

- As a Customer, I want to register with email/phone/password or sign in with Google/Facebook, so that I can book tickets without re-entering my details each time.
- As a Customer, I want to enable 2FA on my account, so that my login is protected even if my password leaks (note: this protection does not apply when I sign in via Google/Facebook).
- As a Customer, I want to browse now-showing and coming-soon movies with ratings (including OMDb critic scores), so that I can decide what to watch.
- As a Customer, I want to see which theaters are screening a given movie and when, so that I can pick a convenient showtime.
- As a Customer, I want to see which seats are available, locked by someone else, or already booked in real time while I'm choosing seats, so that I don't pick a seat someone else is about to take.
- As a Customer, I want to apply a discount code, a gift card, or my loyalty points at checkout, so that I pay less for tickets I'd book anyway.
- As a Customer, I want to pay through a gateway (VNPay/MoMo/Stripe) and get my booking confirmed automatically once payment clears, so that I don't have to manually confirm payment myself.
- As a Customer, if my payment gateway redirect returns before the payment is actually confirmed, I want the app to keep checking and tell me "payment pending" rather than falsely confirming my booking, so that I'm never shown a ticket I didn't actually pay for.
- As a Customer, I want to cancel a booking I haven't paid for yet, so that I'm not stuck holding seats I no longer want.
- As a Customer, I want my seat hold to expire automatically if I abandon checkout, so that other customers can book those seats again.
- As a Customer, I want to rate and comment on a movie after I've actually watched it (not before), so that reviews reflect real viewing experience.
- As a Customer, I want to see a one-time promotional popup (splash banner) when a new campaign is live, so that I notice special offers without it reappearing every visit.
- As a Customer, I want to view my booking history and e-tickets, so that I can find my QR code at the gate.
- As a Customer, I want to opt out of promotional/reminder emails while keeping booking confirmations, so that I control what I'm notified about.

## TheaterStaff

- As a box-office staff member, I want to quote and ring up a walk-in ticket sale, so that a customer without the app can still buy tickets at the counter.
- As a box-office staff member, I want to look up a returning customer by phone, so that I can attach their loyalty account to a counter sale.
- As a gate staff member, I want to scan a customer's QR ticket and get an immediate accept/deny result with a reason, so that I can admit valid ticket holders and catch invalid/duplicate/wrong-showtime tickets.
- As a gate staff member, I want to look up a paid ticket by invoice code or phone when a customer's QR code won't scan, so that I can still admit them manually.
- As a staff member, I want to view the day's schedule board for my theater, so that I know which rooms/showtimes are running.
- As a staff member, I want to report an incident (seat/room/projection/sound/safety/customer/cleanliness issue), so that it's tracked and can be resolved before it affects more customers.

## Admin

- As an Admin, I want full CRUD over movies, theaters, rooms, room types, seat types, patron categories, pricing (time-slot multipliers, holidays, 3D surcharge, per-showtime base price), so that I can configure the entire catalog and pricing model without code changes.
- As an Admin, I want to create promotion programs (fixed-price or percent-off, scoped by theater/day/time/channel, with an auto-apply option), so that I can run campaigns that automatically discount eligible bookings.
- As an Admin, I want to manage loyalty settings globally and per-theater, so that different theaters can have different point-earning/redemption rates if needed.
- As an Admin, I want to moderate customer comments (approve/reject/delete), so that inappropriate content doesn't stay visible on the site.
- As an Admin, I want to issue and deactivate gift cards, so that I can run promotions or compensate customers without a refund feature.
- As an Admin, I want to view and override an invoice's status when something goes wrong operationally, so that I can correct bookings outside the normal customer/gateway flow (being aware this path isn't validated against a transition allow-list).
- As an Admin, I want to view revenue and sales/occupancy/KPI reports, so that I can track theater performance.
- As an Admin, I want to view the audit log, so that I can trace who performed sensitive actions (price overrides, voids, compensation, gate scans, etc.).
- As an Admin, I want to manage splash banners and notification templates, so that I can control customer-facing messaging without a code deploy.
- As an Admin, I want to create and manage staff/customer user accounts and assign roles, so that I control who has access to what.

## Explicitly out of scope (do not plan around these as if they exist)

- Nobody — Customer, TheaterStaff, or Admin — can issue a refund or reverse a `Paid` invoice's financial state today; this was built, then fully reverted. A `Paid` invoice can only be cancelled/overridden via the unconstrained admin status update, which does not reverse points/gift-card/stock or touch any payment gateway.
- No role can export a report as a server-generated file — CSV generation happens client-side in the Admin UI from the same JSON the reports endpoints return.
- Roster/shift scheduling and the manager-override-PIN mechanism were both removed outright (not just hidden) — blocking/unblocking a seat or room, and POS price overrides, now require the `Approvers` role (Admin) directly, with no self-service PIN or roster screen of any kind.
