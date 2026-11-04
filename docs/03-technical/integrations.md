# External Integrations

> Purpose: Every third-party/external service the backend talks to — purpose, call sites, and required credential names (never values).
> Sources: `cinemabe/Cinema/2-Business/Cinema.Business/{Payments,Notifications,ExternalRatings,Managers/Auth}/*.cs`, `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/{Program.cs,Hubs/*}`, `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/appsettings.json`, root `E:\cinema\CLAUDE.md`
> Last reviewed: 2026-11-04

## 1. Payment gateways

Abstracted behind `IPaymentGateway` (`Cinema.Business.Contracts.Payments`), one implementation per provider, all registered as singletons directly in `Program.cs` (not `AddBusiness()`, since the resolver needs `IConfiguration`). `IPaymentGatewayResolver` picks the default from config key `Payments:Provider`, falling back to the first configured gateway, then the first registered gateway at all (so an unconfigured deployment still gets a provider-specific error rather than a generic one).

**Callback-authoritative, not redirect-authoritative** — confirmed for VNPay, MoMo, and Stripe alike: each gateway's `VerifyPaymentAsync` (the synchronous, browser-redirect-driven confirm path) is hardcoded to return `Success = false` with a message pointing at the callback; only `SandboxPaymentGateway.VerifyPaymentAsync` ever succeeds synchronously. The only path to `Paid` for a real gateway is the signed server-to-server call to `PaymentController.PaymentCallback?provider=...` → `BookingManager.HandlePaymentCallbackAsync`. `PaymentCallback` is `[AllowAnonymous]` by design (called by the gateway's own servers, not the browser) and accepts both GET and POST; it builds a flattened `data` dict from query + form fields, falling back to the raw request body (`__rawBody`) for Stripe's JSON webhook, and copies the `Stripe-Signature` header into `__signature`.

**Idempotency**: `HandlePaymentCallbackAsync` checks `invoice.Status == Paid` first and returns `AlreadyConfirmed` without re-finalizing (no double loyalty accrual, no duplicate `InvoicePayment` row) — see the outcome matrix below. A non-`Pending`/non-`Paid` status (e.g. `Cancelled`) returns `NotPending` and leaves the invoice untouched.

**Amount validation**: each gateway's `ParseCallback` re-derives the paid amount from its own provider-signed fields; `HandlePaymentCallbackAsync` cross-checks the reported amount against `invoice.FinalAmount`, both rounded to whole VND, before finalizing — a mismatch yields `InvalidAmount` and the invoice is untouched. The whole system is implicitly VND-only (no currency field travels through the callback model).

| `ParseCallback` status / condition | Outcome | Invoice touched? |
|---|---|---|
| `InvalidSignature` | `InvalidSignature` | No |
| `InvalidOrderReference` | `OrderNotFound` | No |
| `InvalidAmount` (unparsable field) | `InvalidAmount` | No |
| `Rejected` (gateway not configured / unrecognized event) | `Rejected` | No |
| success, no invoice found for parsed `InvoiceId` | `OrderNotFound` | No |
| success, amount present and ≠ `invoice.FinalAmount` | `InvalidAmount` | No |
| success, invoice already `Paid` | `AlreadyConfirmed` | No (idempotent) |
| success, invoice status neither `Pending` nor `Paid` | `NotPending` | No |
| `PaymentFailed` on a `Pending` invoice | `PaymentFailedAcknowledged` | No — stays `Pending`, customer can retry |
| `Paid` on a `Pending` invoice | `Confirmed` | Yes — `FinalizePaidInvoiceAsync` |

| Gateway | File | Status | Credentials (names only) | Notes |
|---|---|---|---|---|
| Sandbox | `Cinema.Business.Payments.SandboxPaymentGateway` | Dev/test only, always "configured" | none | Synchronous confirm always succeeds; not for production. |
| VNPay | `Cinema.Business.Payments.VnPayGateway` | Production-shaped | `Payments:VnPay:TmnCode`, `Payments:VnPay:HashSecret`, `Payments:VnPay:PayUrl`, `Payments:VnPay:ReturnUrl` | HMAC-SHA512 signed redirect + IPN. **Callback-authoritative** — `VerifyPaymentAsync` always returns false; only the signed server-to-server IPN (`PaymentController.PaymentCallback?provider=VNPay`) can mark an invoice Paid. Requires a public tunnel (ngrok) for local dev — see §5. |
| MoMo | `Cinema.Business.Payments.MoMoGateway` | Production-shaped | `Payments:MoMo:PartnerCode`, `Payments:MoMo:AccessKey`, `Payments:MoMo:SecretKey`, `Payments:MoMo:Endpoint`, `Payments:MoMo:ReturnUrl`, `Payments:MoMo:IpnUrl` | `captureWallet` v2 flow, real HTTP POST via `IHttpClientFactory`, HMAC-SHA256 signed IPN. No dedicated unit test file (unlike VNPay). |
| Stripe | `Cinema.Business.Payments.StripeGateway` | Production-shaped | `Payments:Stripe:SecretKey`, `Payments:Stripe:PublishableKey`, `Payments:Stripe:WebhookSecret`, `Payments:Stripe:SuccessUrl`, `Payments:Stripe:CancelUrl` | Hosted Checkout Session via raw HTTP calls (no Stripe SDK dependency); webhook signature per Stripe's `t=...,v1=...` scheme; VND treated as zero-decimal currency (no ×100). |

Selector config: `Payments:Provider` (default provider name when a client doesn't specify one). All four gateways are selectable per-invoice via the `provider` parameter on `PaymentController.InitiatePayment`.

## 2. OAuth / social login

| Provider | Validator class | Call site | Credentials (names only) |
|---|---|---|---|
| Google | `Cinema.Business.GoogleTokenValidator` (`Managers/Auth/GoogleTokenValidator.cs`), interface `IGoogleTokenValidator` | `AuthManager.GoogleLoginAsync` → `IdentityController.GoogleLogin` | `Google:ClientId` |
| Facebook | `Cinema.Business.FacebookTokenValidator` (`Managers/Auth/FacebookTokenValidator.cs`), interface `IFacebookTokenValidator` | `AuthManager.FacebookLoginAsync` → `IdentityController.FacebookLogin` | `Facebook:AppId`, `Facebook:AppSecret` |

Both use `Google.Apis.Auth` / Facebook Graph API (`/me?fields=id,name,email`, `debug_token`) respectively. **Account linking by email**: on a successful token verification, `AuthManager` looks up the user by email — no match auto-creates a `Customer` with a random unguessable password (password login is then impossible) and a placeholder phone (`"g"`/`"f"` + 15 hex chars, since phone is unique+required but these providers give none), `EmailConfirmed=true` immediately; a match found but not yet confirmed is auto-confirmed (signing in via the provider proves email ownership); a match already confirmed logs into the existing account as-is. **2FA bypass**: Google and Facebook logins always build the full `AuthResponse` directly — `TwoFactorEnabled` is never checked, since the social provider's own authentication substitutes for it. Frontend-side client IDs: `cinemafe/projects/*/src/environments/environment.ts` → `googleClientId` / `facebookAppId` (per-app, see `configuration.md`).

## 3. Movie metadata — OMDb

| Piece | File | Purpose |
|---|---|---|
| `IExternalRatingsProvider` / `OmdbRatingsProvider` | `Cinema.Business/ExternalRatings/OmdbRatingsProvider.cs` | Live lookup of external ratings (RottenTomatoes/IMDb/Metascore) for the movie detail page. Matched by `Movie.ImdbId` when present, else title+year. Not entity-backed — result is a runtime DTO (`ExternalRatingsDTO`), never persisted. |

Registered in `AddBusiness()` as a named `HttpClient` (`OmdbRatingsProvider.HttpClientName`, base address `https://www.omdbapi.com`, 3s timeout) plus `IExternalRatingsProvider` singleton.

Credential: `OMDb:ApiKey`.

No evidence of a Stripe/MoMo-equivalent stub beyond what's listed in §1 — all four `IPaymentGateway` implementations found under `Cinema.Business/Payments/` are fully implemented (none are "planned but not built").

## 4. SignalR (real-time, in-process — not a third-party service)

| Hub | Route | Purpose |
|---|---|---|
| `BookingHub` | `/hubs/booking` | Seat-lock broadcast during checkout (`LockSeat`/`UnlockSeat`/`SeatLocked`/`SeatUnlocked` client events). |
| `StaffHub` | `/hubs/staff` | Staff-app events: `FoodOrderQueued`, `FoodOrderUpdated`, `StockLow`, `IncidentRaised`, grouped per `theater:{id}`. |

JWT auth over SignalR uses the `access_token` query-string parameter (browsers can't set custom WebSocket headers) — configured once in `Program.cs`'s `OnMessageReceived`: if the request path starts with `/hubs` and a non-empty `access_token` query value is present, it's used as the bearer token instead of the `Authorization` header; normal HTTP endpoints are unaffected. See `architecture.md` §4. No external dependency; listed here because it's a cross-cutting integration point frontend code must wire into (`CinemaLib`'s `HUB_BASE_URL` token).

## 5. ngrok (dev-only tunnel for VNPay IPN)

Not a runtime dependency of the app — a local developer tool. VNPay's sandbox servers must reach the IPN callback URL directly; `localhost` is never reachable from them. Workflow (from root `CLAUDE.md`):
1. `ngrok http 5102` (installed via `winget install ngrok.ngrok`, authtoken pre-configured on dev machines).
2. Copy the printed `https://....ngrok-free.dev` URL.
3. Set it as the IPN URL in the VNPay sandbox merchant portal (`sandbox.vnpayment.vn`, terminal code = `Payments:VnPay:TmnCode`).
4. Free-plan ngrok issues a new URL per restart — repeat step 3 each time.
5. Sandbox test card: Bank NCB, `9704198526191432198`, name `NGUYEN VAN A`, issue date `07/15`, OTP `123456`.

## 6. Email / SMS notification abstractions

| Interface | Default impl (always registered by `AddBusiness()`) | Real impl (swapped in by `Program.cs` only if configured) | Trigger condition | Credentials |
|---|---|---|---|---|
| `INotificationService` | `DevLogNotificationService` (`Notifications/DevLogNotificationService.cs`) — logs instead of sending | `SmtpNotificationService` (`Notifications/SmtpNotificationService.cs`) | `Smtp:Host` non-blank | `Smtp:Host`, `Smtp:Port`, `Smtp:User`, `Smtp:Password`, `Smtp:From`, `Smtp:EnableSsl` |
| `ISmsNotificationService` | `DevLogSmsNotificationService` (`Notifications/DevLogSmsNotificationService.cs`) | `TwilioSmsNotificationService` (`Notifications/TwilioSmsNotificationService.cs`) | `Sms:Twilio:AccountSid` non-blank | `Sms:Provider`, `Sms:Twilio:AccountSid`, `Sms:Twilio:AuthToken`, `Sms:Twilio:FromNumber` |
| `ISeatNotificationService` | `NoOpSeatNotificationService` | `SignalRSeatNotificationService` (`Hubs/SignalRSeatNotificationService.cs`) | always (no config gate — unconditionally overridden in `Program.cs`) | — |
| `IStaffNotificationService` | `NoOpStaffNotificationService` | `SignalRStaffNotificationService` (`Hubs/SignalRStaffNotificationService.cs`) | always (unconditionally overridden) | — |

Used for: booking confirmations, password reset / email verification / 2FA codes (email + SMS — 2FA sends a 6-digit numeric code, SHA-256-hashed, 5-minute expiry, emailed and also SMS'd if the user has a phone number; forgot-password/resend-verification return 204 silently for an unknown or already-confirmed email, by design, to avoid account enumeration), showtime reminders (`ShowtimeReminderService`), kitchen/stock/incident staff alerts. Templates are stored in the `Template` entity, rendered via `TemplateRenderer` with `{{Token}}` placeholders (see `database-schema.md` §8).

## 7. Summary — credential config keys by integration

| Integration | Config keys (names only, never values) |
|---|---|
| JWT | `JWT:Secret`, `JWT:Issuer`, `JWT:Audience`, `JWT:ExpiryHours` |
| Google OAuth | `Google:ClientId` |
| Facebook OAuth | `Facebook:AppId`, `Facebook:AppSecret` |
| SMTP | `Smtp:Host`, `Smtp:Port`, `Smtp:User`, `Smtp:Password`, `Smtp:From`, `Smtp:EnableSsl` |
| SMS (Twilio) | `Sms:Provider`, `Sms:Twilio:AccountSid`, `Sms:Twilio:AuthToken`, `Sms:Twilio:FromNumber` |
| VNPay | `Payments:VnPay:TmnCode`, `Payments:VnPay:HashSecret`, `Payments:VnPay:PayUrl`, `Payments:VnPay:ReturnUrl` |
| MoMo | `Payments:MoMo:PartnerCode`, `Payments:MoMo:AccessKey`, `Payments:MoMo:SecretKey`, `Payments:MoMo:Endpoint`, `Payments:MoMo:ReturnUrl`, `Payments:MoMo:IpnUrl` |
| Stripe | `Payments:Stripe:SecretKey`, `Payments:Stripe:PublishableKey`, `Payments:Stripe:WebhookSecret`, `Payments:Stripe:SuccessUrl`, `Payments:Stripe:CancelUrl` |
| Payment default | `Payments:Provider` |
| OMDb | `OMDb:ApiKey` |
| Database | `ConnectionStrings:CinemaDatabase` |

See `configuration.md` for the full config reference including non-integration keys.
