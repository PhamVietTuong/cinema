# Payments, Invoicing & Refunds — Current Code Reference

Scope: `cinemabe` backend only (`PaymentController`, `Cinema.Business.Contracts.Payments`,
`Cinema.Business.Payments.*`, `InvoiceManager`/`InvoiceAdminManager`, `BookingManager` payment paths,
`GiftCard*`). Verified against code as of 2026-10-31, branch `feature/business-docs` (based on
`master` @ `446ebd0`).

**Headline fact, confirmed from code and git history: there is no refund feature in the current
codebase.** A refund system (customer/staff-initiated, gateway-side refund calls, `Refunded`/
`PartiallyRefunded` invoice states) was built across several commits and then explicitly removed:

```
84b6d9a feat(ui): refund a Paid booking from profile (customer) and invoices (admin)
e692a5a feat(refunds): refund a Paid booking (gateway refund + state reversal)
07c7877 feat(cinemabe): staff refund, exchange, reprint, drawer close and daily close
7edcc5f chore: regenerate NSwag clients for refunds, pickup queue and reports
...
9660958 feat(cinemabe): remove online refund feature
f0e4fe7 feat(cinemafe): remove refund UI, shared enum labels and error-catalog entries
```

`InvoiceStatus` (`Cinema.Data.Entities/Enums/InvoiceStatus.cs`) literally documents this:

```csharp
// 4 reserved (was Refunded) — do not reuse.
public enum InvoiceStatus { Pending = 0, Paid = 1, Cancelled = 2, Failed = 3 }
```

Treat any pre-existing documentation, memory, or assumption about a live refund flow as **stale**.
Section 6 below documents this as "not implemented" per the task's own request shape, with pointers
to where it used to live.

---

## 1. Invoice lifecycle

### 1.1 `InvoiceStatus` enum (`Cinema.Data.Enums.InvoiceStatus`)

| Value | Name | Meaning |
|---|---|---|
| 0 | `Pending` | Invoice created, awaiting payment (online) or mid-composition. Seats/food stock are reserved. |
| 1 | `Paid` | Payment confirmed (online gateway callback, synchronous Sandbox confirm, or counter sale is Paid at creation). |
| 2 | `Cancelled` | Customer cancelled while Pending, or the invoice expired Pending past its hold window. |
| 3 | `Failed` | Defined but **never set anywhere in current code** — no code path assigns it. Reserved for future use (e.g. a synchronous decline path). |
| 4 | *(reserved, unnamed)* | Formerly `Refunded`. Do not reuse this ordinal — old data/logs may reference it. |

`InvoiceAdminManager.UpdateStatusAsync` binds `request.Status` directly from the request DTO with no
allow-list of transitions — an admin (or a crafted request) can set *any* `InvoiceStatus` value,
including the reserved `4`, since C# enum model binding does not validate against named values. This
is a latent gotcha, not an exploited one (no code currently writes value 4).

### 1.2 Exact transition triggers

| From → To | Trigger | Code path |
|---|---|---|
| *(none)* → `Pending` | Customer creates an online booking | `BookingManager.CreateBookingAsync` (line ~144) |
| *(none)* → `Paid` | Staff rings up a counter (box-office) sale — always paid at creation, no Pending stage | `BookingManager.SellAtCounterAsync` (line ~246) |
| `Pending` → `Paid` (no gateway) | Invoice fully covered by gift card/points (`FinalAmount <= 0`) at `InitiatePayment` time | `BookingManager.InitiatePaymentAsync` → `FinalizePaidInvoiceAsync` |
| `Pending` → `Paid` | Owner's synchronous confirm succeeds (Sandbox gateway only) | `BookingManager.ConfirmPaymentAsync` → `FinalizePaidInvoiceAsync` |
| `Pending` → `Paid` | Gateway server-to-server callback reports success, signature/amount verified | `BookingManager.HandlePaymentCallbackAsync` → `FinalizePaidInvoiceAsync` |
| `Pending` → `Cancelled` | Owner cancels before paying | `BookingManager.CancelBookingAsync` |
| `Pending` → `Cancelled` | Booking left Pending past its age cutoff (seat-hold timeout), swept by a background job | `BookingManager.ExpireStalePendingBookingsAsync` |
| `Paid`/`Pending`/other → *any* | Admin override, no business-rule enforcement beyond stamping `PaidAt` if moving to `Paid` | `InvoiceAdminManager.UpdateStatusAsync` |
| *(delete)* | Admin deletes an invoice; if it was `Pending`, food stock is restored first | `InvoiceAdminManager.DeleteAsync` |

Guard rules enforced by code (not just convention):
- `InitiatePaymentAsync`, `ConfirmPaymentAsync`, `CancelBookingAsync` all require `Status == Pending`
  and reject (return `null`/`false`) otherwise — including already-`Paid` invoices (no double
  confirm/cancel).
- `HandlePaymentCallbackAsync` explicitly branches on invoice status (see §2) for idempotency.
- Only the invoice's own `UserId` may call `InitiatePayment`/`ConfirmPayment`/`CancelBooking` for it
  (object-level authorization check against `userId` parameter, which comes from the JWT).

### 1.3 Side effects of `Pending` → `Paid` (`FinalizePaidInvoiceAsync`)

1. `Status = Paid`, `PaymentReference` set, `PaidAt = UtcNow`.
2. If `FinalAmount > 0`, append one `InvoicePayment { Method = Online, Amount = FinalAmount, Reference = paymentReference }`.
3. `ApplyPaidSideEffectsAsync`: accrue loyalty points (`FinalAmount / pointsPerUnit` via `LoyaltyRates`), re-evaluate membership tier, mark the used `Discount.UsedCount += 1` if a promo code was applied.
4. `PushFoodOrderQueuedAsync` (kitchen notification) if the invoice has food lines.
5. `SendConfirmationAsync` — email/SMS e-ticket confirmation (best-effort; template-rendered, falls back to a hardcoded string; a notification failure never fails the payment).

### 1.4 Side effects of `Pending` → `Cancelled` (`CancelBookingAsync` / `ExpireStalePendingBookingsAsync`)

Run inside one DB transaction:
1. `Status = Cancelled`.
2. `CancelFoodOrder` (releases food-order state).
3. `DeactivateTicketsAsync` — frees the seats.
4. `RestoreRedeemedPointsAsync` — gives back `PointsRedeemed` to the user.
5. `RestoreGiftCardAsync` — adds `GiftCardAmount` back to the referenced `GiftCard.Balance`.
6. `RestoreFoodStockAsync` — restocks reserved food/drink inventory (batched across all stale invoices for the sweep job).

---

## 2. Generic multi-gateway callback architecture

### 2.1 Abstraction (`Cinema.Business.Contracts.Payments`)

- **`IPaymentGateway`** — one implementation per provider (`Sandbox`, `VNPay`, `MoMo`, `Stripe`), all
  registered as singletons in `Program.cs` and resolved by name via `IPaymentGatewayResolver`.
  - `Name` — stable key stored on `Invoice.PaymentMethod`.
  - `IsConfigured` — true only if the provider's required secrets are non-blank; an unconfigured
    gateway is never handed out by the resolver, so checkout never throws mid-flow for a gateway
    nobody set up.
  - `CreatePaymentAsync(invoiceId, amount, returnUrl, clientIp)` → `PaymentInitiation(PaymentReference, RedirectUrl?)`.
  - `VerifyPaymentAsync(paymentReference, expectedAmount)` → `PaymentVerification` — **synchronous
    owner-confirm path**. Only `SandboxPaymentGateway` ever returns `Success = true` here; VNPay/MoMo/Stripe
    hardcode `Success = false` with a message pointing at the callback.
  - `ParseCallback(data)` → `PaymentCallbackResult` — verifies signature and parses the flattened
    callback payload (`data` holds merged query + form fields, `__rawBody` for raw-body signing
    (Stripe), `__signature` for header-based signing (Stripe's `Stripe-Signature`)).

- **`IPaymentGatewayResolver`** (`PaymentGatewayResolver`): holds all registered gateways keyed by
  `Name` (case-insensitive). `Default` is chosen from `Payments:Provider` config if that provider is
  configured, else falls back to the first configured gateway, else the first registered gateway at
  all (so an unconfigured deployment still gets a provider-specific error message instead of a
  generic one). `Resolve(providerName)` returns the named gateway only if configured, else `Default`.

### 2.2 Initiate → Callback → Paid flow

```
Client                     PaymentController                 BookingManager                  Gateway
  |  InitiatePayment  ----------> InitiatePayment -------------> InitiatePaymentAsync
  |                                                                  |-- invoice must be owner's + Pending
  |                                                                  |-- FinalAmount<=0 ? finalize now, no gateway
  |                                                                  |-- else resolve gateway, CreatePaymentAsync
  |                                                                  |-- stamp invoice.PaymentMethod/PaymentReference
  |  <--- PaymentInitiationDTO {Provider, PaymentReference, RedirectUrl}
  |
  |  (browser redirected to RedirectUrl, pays on provider's hosted page)
  |
  |                    [gateway's server] --- POST/GET PaymentCallback?provider=X ---> PaymentController
  |                                                                  |-- flattens query+form(+raw body/sig) into `data`
  |                                                                  |-- HandlePaymentCallbackAsync(provider, data)
  |                                                                       |-- gateway.ParseCallback(data) [sig + fields]
  |                                                                       |-- invoice lookup by parsed InvoiceId
  |                                                                       |-- amount cross-check vs invoice.FinalAmount
  |                                                                       |-- idempotency / status-state checks
  |                                                                       |-- FinalizePaidInvoiceAsync -> Status=Paid
  |                                            <--- provider-specific ack body (see §4 for VNPay)
  |
  |  (browser redirected back to ReturnUrl — display only, see §3)
  |  GetInvoice (poll) ------> GetInvoice ----------------------> InvoiceManager.GetByIdAsync
```

Key implementation detail in `PaymentController.PaymentCallback`
(`cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/PaymentController.cs`):
- `[AllowAnonymous]`, accepts both `POST` and `GET` (VNPay/MoMo call via redirect-style GET/POST, Stripe via POST webhook).
- Builds `data` from `Request.Query` + (if form-encoded) `Request.Form`; otherwise reads the raw body into `data["__rawBody"]` (Stripe JSON webhook).
- Copies the `Stripe-Signature` header into `data["__signature"]` when present.
- Branches the **response shape** on whether the resolved gateway is VNPay (`VnPayGateway.BuildIpnResponse`, exact JSON contract — §4) vs. a generic `{message}` / `{error}` body for everyone else.
- Any unhandled exception during a VNPay callback still returns HTTP 200 with `RspCode=99` (so VNPay doesn't retry-storm on a transient server error) — other providers fall through to `HandleException` (HTTP 500).

### 2.3 `HandlePaymentCallbackAsync` outcome matrix (`BookingManager`)

| Gateway `ParseCallback` status | → `PaymentCallbackOutcome` | Invoice touched? |
|---|---|---|
| `InvalidSignature` | `InvalidSignature` | No |
| `InvalidOrderReference` | `OrderNotFound` | No |
| `InvalidAmount` (from gateway parse, e.g. unparsable field) | `InvalidAmount` | No |
| `Rejected` (gateway not configured / unrecognized event) | `Rejected` | No |
| success, but no invoice found for parsed `InvoiceId` | `OrderNotFound` | No |
| success, `ReceivedAmount` present and ≠ `invoice.FinalAmount` (rounded) | `InvalidAmount` | No |
| success, invoice already `Paid` | `AlreadyConfirmed` | No (idempotent no-op) |
| success, invoice status is neither `Pending` nor `Paid` (e.g. `Cancelled`) | `NotPending` | No |
| `PaymentCallbackStatus.PaymentFailed` on a `Pending` invoice | `PaymentFailedAcknowledged` | No — invoice stays `Pending` so the customer can retry |
| `PaymentCallbackStatus.Paid` on a `Pending` invoice | `Confirmed` | Yes — `FinalizePaidInvoiceAsync` |

---

## 3. CRITICAL RULE — callback-authoritative, not redirect-authoritative

**Confirmed true in current code for VNPay, MoMo, and Stripe alike.** Every real gateway's
`VerifyPaymentAsync` (the synchronous, browser-redirect-driven confirm path) is hardcoded to fail:

```csharp
// VnPayGateway / MoMoGateway / StripeGateway — identical pattern
public Task<PaymentVerification> VerifyPaymentAsync(string paymentReference, double expectedAmount)
    => Task.FromResult(new PaymentVerification(false, "<Provider> payments are confirmed via the signed <IPN/webhook>."));
```

Only `SandboxPaymentGateway.VerifyPaymentAsync` ever returns `Success = true` (dev/local convenience).
This means `PaymentController.ConfirmPayment` / `BookingManager.ConfirmPaymentAsync` can **never**
flip a real-provider invoice to `Paid` — the only path to `Paid` for VNPay/MoMo/Stripe is the signed
server-to-server callback through `PaymentCallback` → `HandlePaymentCallbackAsync`. The browser's
return-URL redirect after checkout is display-only and must never be trusted as proof of payment,
because it travels through the customer's own browser and can be replayed/tampered with. The
frontend's `payment-return` page (`cinemafe/projects/CinemaUser/src/app/features/modules/booking/payment-return/payment-return.component.ts`) polls `GetInvoice` after the redirect and shows a
"pending" state until the IPN lands — this is expected behavior, not a bug, when the IPN hasn't
arrived yet (e.g. backend unreachable from the gateway's servers).

---

## 4. VNPay IPN response contract

`VnPayGateway.BuildIpnResponse(PaymentCallbackOutcome)` — the **only** acceptable response shape for
VNPay's IPN call is `{"RspCode": "..", "Message": ".."}`. Any other shape/HTTP status causes VNPay to
retry the IPN for up to 50 minutes (10 attempts, ~5 minutes apart — per VNPay's own retry policy, not
enforced by this codebase).

| `PaymentCallbackOutcome` | `RspCode` | `Message` |
|---|---|---|
| `Confirmed` | `00` | `Confirm Success` |
| `PaymentFailedAcknowledged` | `00` | `Confirm Success` (acknowledged even though payment failed — tells VNPay not to retry) |
| `OrderNotFound` | `01` | `Order not found` |
| `AlreadyConfirmed` | `02` | `Order already confirmed` |
| `NotPending` | `02` | `Order already confirmed` (reused for "invoice not in a confirmable state") |
| `InvalidAmount` | `04` | `Invalid amount` |
| `InvalidSignature` | `97` | `Invalid signature` |
| anything else (`Rejected`, `Error`, unmapped) | `99` | `Unknown error` |

VNPay signing: HMAC-SHA512 over the sorted, URL-encoded `vnp_*` query params (excluding
`vnp_SecureHash`/`vnp_SecureHashType`) using `Payments:VnPay:HashSecret`. Amount convention:
`vnp_Amount` is the VND amount × 100 (VNPay quirk); `ParseCallback` divides by 100 before comparing
to `invoice.FinalAmount`.

---

## 5. Gateway implementation status

| Gateway | File | Status | Notes |
|---|---|---|---|
| **Sandbox** | `Cinema.Business.Payments.SandboxPaymentGateway` | Fully implemented (dev/test only) | Always configured (`IsConfigured => true`); approves any non-empty reference + positive amount synchronously via `VerifyPaymentAsync`; `ParseCallback` expects plain `invoiceId`/`reference` fields (no signature). Not for production use. |
| **VNPay** | `Cinema.Business.Payments.VnPayGateway` | Fully implemented, production-shaped | Real HMAC-SHA512 signed redirect + signed IPN verification, `BuildIpnResponse` contract (§4). Config: `Payments:VnPay:TmnCode/HashSecret/PayUrl/ReturnUrl`. Has its own test suite (`VnPayGatewayTests.cs`, `VnPayTestHelper.cs`). |
| **MoMo** | `Cinema.Business.Payments.MoMoGateway` | Fully implemented, production-shaped | `captureWallet` v2 flow, real HTTP POST to MoMo's create endpoint via `IHttpClientFactory`, HMAC-SHA256 signed IPN verification. Config: `Payments:MoMo:PartnerCode/AccessKey/SecretKey/Endpoint/ReturnUrl/IpnUrl`. No dedicated unit test file found (unlike VNPay). |
| **Stripe** | `Cinema.Business.Payments.StripeGateway` | Fully implemented, production-shaped | Hosted Checkout Session via raw HTTP calls to Stripe's REST API (no Stripe SDK dependency), webhook signature verification per Stripe's `t=...,v1=...` scheme (HMAC-SHA256, constant-time compare via `CryptographicOperations.FixedTimeEquals`), only acts on `checkout.session.completed` events. Config: `Payments:Stripe:SecretKey/WebhookSecret/SuccessUrl/CancelUrl`. VND treated as zero-decimal currency (no cents conversion). |

All four are registered as singletons in `Program.cs` and selectable per-invoice via the `provider`
parameter on `InitiatePayment`; `PaymentGatewayResolver` silently falls back to a configured gateway
if the requested one lacks credentials.

---

## 6. Refund flow — NOT IMPLEMENTED in current code

Despite git history showing a complete refund feature having been built (customer self-service refund
from profile, admin/staff refund from the invoices screen, gateway-side refund calls, state reversal,
NSwag clients for a refund endpoint), it was **fully reverted**:

- `9660958 feat(cinemabe): remove online refund feature` — backend removal.
- `f0e4fe7 feat(cinemafe): remove refund UI, shared enum labels and error-catalog entries` — frontend removal.

Evidence the removal is complete, not partial:
- No `Refund` symbol anywhere under `cinemabe/Cinema` (controllers, contracts, DTOs, managers, entities).
- No refund UI component under `cinemafe` (only an unrelated `pos-calc.ts` hit for the string "refund" in a comment/calc context, in `CinemaStaff`).
- `InvoiceStatus` has no `Refunded`/`PartiallyRefunded` member; the ordinal `4` that `Refunded` used to occupy is explicitly commented as reserved/do-not-reuse.
- `IPaymentGateway` has no refund method (only `CreatePaymentAsync`/`VerifyPaymentAsync`/`ParseCallback`).
- The staff "refund, exchange, reprint, drawer close" feature (`07c7877`) still has **exchange** support (`Invoice.ExchangedFromInvoiceId` field persists on the entity), but the **refund** part of that commit was separately removed.

**Conclusion for anyone building on this doc**: there is currently no way, through the API, to move
money back to a customer, reverse a `Paid` invoice's financial state, or record a partial/full refund.
A `Paid` invoice can only become something else via the unconstrained admin `UpdateStatusAsync`
(§1.1), which does not reverse points/gift-card/stock or touch any gateway. If refund work is
requested again, there is no existing scaffolding to extend — it would need to be designed fresh,
and the removed commits above are a useful reference for what shape was tried before (gateway refund
calls, invoice state, loyalty/gift-card reversal) and then deliberately discarded.

---

## 7. Gift card redemption interaction with payment

- Applied during invoice composition (`BookingManager` — the private invoice-build helper around line 639–670), for both online booking and counter sale, before the gateway is ever invoked.
- Validation: code must resolve to an active (`IsActive`), non-expired (`ExpiresAt == null || > UtcNow`) `GiftCard`; an invalid/expired code throws `InvalidOperationException("Invalid or expired gift card.")` rather than silently skipping the discount.
- Amount drawn = `Math.Min(card.Balance, finalAmount)`, floored (never rounded up past the card's real balance) for counter sales (`wholeVnd`).
- Immediately decrements `GiftCard.Balance` (not deferred to payment success) and reduces `invoice.FinalAmount`/increases `invoice.DiscountAmount`; `invoice.GiftCardId`/`GiftCardAmount` record what was drawn, for later restoration.
- If gift card (+ points) fully covers the price (`FinalAmount <= 0`), `InitiatePaymentAsync` finalizes the invoice as `Paid` immediately with `PaymentReference = "GIFTCARD-FULL"` and **no gateway is ever called** (`Provider = "None"` in the response DTO).
- Counter sale: gift-card portion recorded as its own `InvoicePayment { Method = GiftCard, Amount, Reference = code }` line, settled instantly (counter sales are created directly in `Paid` status).
- Reversal: if the invoice is later `Cancelled` (customer cancel or stale-Pending expiry), `RestoreGiftCardAsync` adds the drawn amount back to `GiftCard.Balance`. There is **no reversal path from `Paid`** — because there is no refund feature (§6), a paid gift-card-funded booking can't have its gift-card balance restored.
- Gift cards have no interaction with the payment *gateway* layer at all — they're a pre-gateway discount mechanism, resolved entirely in `Cinema.Business`, never touching `IPaymentGateway`.

---

## 8. Idempotency & amount/currency validation

**Idempotency**
- Double-callback delivery (every real gateway retries on anything but a clean "success" ack): `HandlePaymentCallbackAsync` checks `invoice.Status == Paid` first and returns `AlreadyConfirmed` without re-running `FinalizePaidInvoiceAsync` — no double loyalty accrual, no duplicate `InvoicePayment` row.
- Double owner-confirm / double-cancel: both `ConfirmPaymentAsync` and `CancelBookingAsync` require `Status == Pending`, so a second call on an already-finalized invoice is a no-op (`false`).
- `InvoicePayment` rows added inside `FinalizePaidInvoiceAsync` use `Id = Guid.Empty` explicitly — a deliberate EF Core workaround (comment in code) so a tracked invoice's new child isn't mistaken for an update-of-existing-row (`DbUpdateConcurrencyException`), not an idempotency mechanism per se.

**Amount validation**
- Gateway `ParseCallback` implementations each independently re-derive the paid amount from provider-specific, provider-signed fields (`vnp_Amount` for VNPay — divided by 100; MoMo's signed `amount` field, implicitly verified since it's part of the HMAC input; Stripe trusts `payment_status == "paid"` on a signature-verified webhook body, no separate amount field is compared in `ParseCallback` itself beyond what's inside the verified JSON).
- `BookingManager.HandlePaymentCallbackAsync` cross-checks `result.ReceivedAmount` (when the gateway reports one) against `invoice.FinalAmount`, both rounded to whole VND via `(decimal)(long)Math.Round(...)` to avoid float/decimal rounding mismatches with `VnPayGateway.CreatePaymentAsync`'s own `(long)Math.Round(amount)`. Mismatch → `InvalidAmount` outcome, invoice untouched.
- No currency field is carried through the callback model at all — the whole system is implicitly VND-only (hardcoded `"VND"`/`"vnd"` literals in VNPay and Stripe gateways).

**Signature validation**
- VNPay: HMAC-SHA512 over sorted/encoded `vnp_*` params.
- MoMo: HMAC-SHA256 over a fixed-order pipe of named fields.
- Stripe: HMAC-SHA256 over `"{timestamp}.{rawBody}"`, constant-time compared against each `v1=` value in the `Stripe-Signature` header (supports key rotation's dual-signature window).
- Sandbox: no signature check at all (not a real provider).

---

## 9. Key invariants / gotchas

1. **No refund capability exists** — see §6. Do not assume `Refunded`/`PartiallyRefunded` invoice states, a refund endpoint, or gateway refund calls exist; they were built and then deliberately removed.
2. **Real gateways are callback-authoritative, never redirect/confirm-authoritative** — `VerifyPaymentAsync` always fails for VNPay/MoMo/Stripe by design (§3). Only Sandbox trusts the synchronous confirm.
3. **`InvoiceStatus` value `4` is poisoned** — reserved, do not reuse for a new status; `InvoiceAdminManager.UpdateStatusAsync` has no enum-value allow-list, so nothing stops an admin request from writing it (or any other status) directly.
4. **`InvoiceStatus.Failed` is defined but dead code** — no path sets it currently; a gateway/booking failure today just leaves the invoice `Pending` (real providers) or never creates one (thrown exceptions during `CreateBookingAsync`/`SellAtCounterAsync` roll back the whole transaction).
5. **Zero-amount checkout bypasses the gateway entirely** — a booking fully covered by gift card/points goes straight to `Paid` with `PaymentReference = "GIFTCARD-FULL"`; no `IPaymentGateway` is invoked, so gateway-specific tests/monitoring won't see these transactions.
6. **Counter sales skip `Pending` entirely** — `SellAtCounterAsync` creates the invoice directly as `Paid`; the online `Pending → Paid` machinery (gateway, callback, `ConfirmPayment`) is customer/online-booking-only.
7. **PaymentCallback is intentionally unauthenticated** (`[AllowAnonymous]`) since it's called by the gateway's servers, not the browser — all trust comes from the per-gateway signature check inside `ParseCallback`, not from ASP.NET auth.
8. **VNPay amount unit is ×100** (minor-unit-like encoding despite VND having no subunit in practice) — both `CreatePaymentAsync` and `ParseCallback` must agree on this, and `HandlePaymentCallbackAsync`'s rounding must match `VnPayGateway`'s own rounding or genuine payments get rejected as amount-mismatches (explicitly called out in a code comment).
9. **Gift card balance is drawn down at booking time, not at payment confirmation** — meaning a `Pending` invoice that never gets paid (and isn't cancelled/doesn't expire) would hold gift-card balance reserved without a path to restore it; the background sweep (`ExpireStalePendingBookingsAsync`) is what's relied upon to eventually free it.
10. **Stripe VND handling has no cents conversion** — `unit_amount` is the raw VND integer because VND is a Stripe zero-decimal currency; don't apply a ×100 adjustment here (unlike VNPay, which does the opposite — multiplies by 100).
11. **`PaymentTender` enum is a separate, serialized-as-int concept from `InvoiceStatus`/gateway names** — `Online` (5) is what `FinalizePaidInvoiceAsync` stamps for a gateway-settled line; `GiftCard` (3) and `Points` (4) are pre-gateway discount tenders recorded only for counter sales. Never renumber (explicit comment on the enum).
