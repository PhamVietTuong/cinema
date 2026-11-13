# Configuration Reference

> Purpose: Every environment variable / config key used by the backend and frontend apps — purpose, required/optional, default.
> Sources: `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/appsettings.json` (key names only — `appsettings.Development.json`/`appsettings.Production.json` exist but are git-ignored and were not read for values), `Program.cs`, `cinemabe/Cinema/3-Data/Cinema.Data/Contexts/CinemaContextDesignTimeFactory.cs`, `cinemafe/projects/*/src/environments/environment*.ts`
> Last reviewed: 2026-11-12

No secret values are reproduced anywhere below — only key names, defaults as committed in `appsettings.json`, and whether the app fails/falls back when a key is blank.

## 1. Backend — `appsettings.json` keys

### Kestrel / hosting
| Key | Purpose | Required | Default (as committed) |
|---|---|---|---|
| `Kestrel:Endpoints:Http:Url` | HTTP listen address | optional | `http://localhost:5102` |
| `Kestrel:Endpoints:Https:Url` | HTTPS listen address | optional | `https://localhost:7068` |
| `AllowedOrigins` | CORS allow-list (array), policy `AllowFrontend` | required for cross-origin frontend calls | `localhost:4200`–`4203` (http+https) |
| `AllowedHosts` | ASP.NET host filtering | optional | `*` |

### Database
| Key | Purpose | Required | Default |
|---|---|---|---|
| `ConnectionStrings:CinemaDatabase` | SQL Server connection string | **required** — blank in committed `appsettings.json`, must be set in `appsettings.Development.json`/`appsettings.Production.json` or env var | `""` |
| `CINEMA_DESIGNTIME_CONNECTION` (environment variable, not an appsettings key) | Overrides the connection string used by `dotnet ef` design-time tooling (`CinemaContextDesignTimeFactory`) | optional | falls back to `Server=(localdb)\MSSQLLocalDB;Database=Cinema;Trusted_Connection=True;TrustServerCertificate=True` if unset |

### JWT
| Key | Purpose | Required | Default |
|---|---|---|---|
| `JWT:Secret` | HMAC-SHA256 signing key | **required** — app throws `InvalidOperationException` at startup if blank or equal to the old committed insecure default (`"CinemaSecretKey2024!SuperSecureAndLongEnoughForHS256Algorithm"`) | `""` (fails fast) |
| `JWT:Issuer` | Token issuer claim validation | required | `CinemaAPI` |
| `JWT:Audience` | Token audience claim validation | required | `CinemaClient` |
| `JWT:ExpiryHours` | Token lifetime | optional | `24` |

### OAuth
| Key | Purpose | Required | Default |
|---|---|---|---|
| `Google:ClientId` | Verifies Google ID tokens | optional — login returns 400 "Google login is not configured" if blank | `""` |
| `Facebook:AppId` | Facebook login (not directly validated server-side; used in client flow) | optional | `""` |
| `Facebook:AppSecret` | Enables `debug_token` app-id verification when present | optional — without it, token validation skips the app-id cross-check | `""` |

### Email / SMS
| Key | Purpose | Required | Default |
|---|---|---|---|
| `Smtp:Host` | Presence gates whether `SmtpNotificationService` replaces the dev-log sender | optional | `""` (dev-log fallback) |
| `Smtp:Port` | SMTP port | optional | `587` |
| `Smtp:User`, `Smtp:Password`, `Smtp:From` | SMTP credentials/sender | optional | `""` |
| `Smtp:EnableSsl` | SMTP TLS | optional | `true` |
| `Smtp:TimeoutSeconds` | Per-send timeout (1-120) | optional | `15` |
| `Sms:Provider` | SMS provider label | optional | `Twilio` |
| `Sms:Twilio:AccountSid` | Presence gates whether `TwilioSmsNotificationService` replaces the dev-log sender | optional | `""` (dev-log fallback) |
| `Sms:Twilio:AuthToken`, `Sms:Twilio:FromNumber` | Twilio credentials/sender number (required once `AccountSid` is set; `FromNumber` must be a valid number, normalised to E.164) | optional | `""` |
| `Sms:Twilio:TimeoutSeconds` | Per-request timeout (1-120) | optional | `10` |

Startup validation: when `Smtp:Host` or `Sms:Twilio:AccountSid` is set, `Program.cs` validates the whole section (`SmtpOptions`/`TwilioOptions`) and the host refuses to start with every problem listed (bad port, sender not an email, `User` without `Password`, missing `AuthToken`, invalid `FromNumber`...). A section with a credential/sender but no key field (`Smtp:User`/`Password`/`From` without `Smtp:Host`, or `Sms:Twilio:AuthToken`/`FromNumber` without `AccountSid`) fails too, so it cannot silently fall back to the dev-log sender. A fully empty section just keeps the dev-log senders. `Smtp:From` may include a display name (`Cinema <noreply@x.vn>`).

### Payments
| Key | Purpose | Required | Default |
|---|---|---|---|
| `Payments:Provider` | Default gateway name when a client doesn't specify one | optional | `Sandbox` |
| `Payments:EnableSandbox` | Registers the unsigned Sandbox gateway outside Development. Off by default; only CI e2e and the docker-compose demo set it to `true`. Never enable on a real deployment | optional | `false` |
| `RateLimiting:AuthPermitLimit` | Requests per minute per client IP on the `Auth` rate-limit policy (Login, Register, ResetPassword, ConfirmEmail, GoogleLogin, FacebookLogin, VerifyTwoFactor). Only the CI e2e job raises it (the Playwright suite signs in about 16 times from one IP within minutes); a missing, non-numeric or non-positive value keeps the default, so a typo never disables the limiter | optional | `10` |
| `Payments:VnPay:TmnCode` | VNPay terminal code | optional — gateway reports `IsConfigured=false` if blank | `""` |
| `Payments:VnPay:HashSecret` | VNPay HMAC-SHA512 signing secret | optional (see above) | `""` |
| `Payments:VnPay:PayUrl` | VNPay hosted payment page | optional | `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` |
| `Payments:VnPay:ReturnUrl` | Browser redirect target after payment (display-only — VNPay is callback-authoritative, so this URL is never trusted to confirm payment; see `integrations.md` §1) | optional | `http://localhost:4202/booking/payment-return` |

### Movie metadata
| Key | Purpose | Required | Default |
|---|---|---|---|
| `OMDb:ApiKey` | OMDb external-ratings lookup | optional — when blank, `OmdbRatingsProvider.GetRatingsAsync` returns `null` immediately (no HTTP call, no error; external ratings are simply absent) | `""` |

### Gate / booking rules (optional, in-code defaults, read with raw `IConfiguration`)
| Key | Purpose | Required | Default |
|---|---|---|---|
| `Gate:AdmitBeforeMinutes` | How long before the showtime start a ticket may be scanned in | optional | `30` |
| `Gate:AgePromptMinAge` | Minimum age (MinAge of the film) from which the gate prompts for an age check. Gate only: the online age confirmation uses a fixed threshold of 13 | optional | `13` (T13, T16, T18) |
| `Booking:OnlineSalesCloseMinutesAfterStart` | Online sale stays open this many minutes after the showtime start (0 = closes exactly at start) | optional | `0` |

### Logging
| Key | Purpose | Required | Default |
|---|---|---|---|
| `Serilog:MinimumLevel:Default` / `:Override:*` | Serilog log levels | optional | `Information`, with `Microsoft`/`Microsoft.EntityFrameworkCore` at `Warning` |
| `Serilog:WriteTo` | Sinks (Console + rolling File) | optional | Console + File at `logs/cinema-.log`, daily rolling. The path is relative to the working directory: the WebApiHost project folder under `dotnet run`, `/app/logs` in the container (compose mounts the named volume `cinema-logs`). Override the file path with env var `Serilog__WriteTo__1__Args__path`; the `1` is the sink's index in the `WriteTo` array (Console is 0, File is 1), so it breaks if the sinks are reordered. |
| `Logging:LogLevel:Default` / `:Microsoft.AspNetCore` | Standard ASP.NET logging config | optional | `Information` / `Warning` |

## 2. Frontend — Angular `environment.ts` keys (per app)

Each of the three apps (`CinemaUser`, `CinemaAdmin`, `CinemaStaff`) has its own `environment.ts` (dev) / `environment.prod.ts` (production build), swapped via `angular.json` file replacement.

### Common to all three apps
| Key | Purpose | Dev default | Prod default |
|---|---|---|---|
| `production` | Angular build flag | `false` | `true` |
| `apiUrl` | Backend base URL, bootstrapped into `CinemaServiceAgent.CINEMA_BASE_URL` | `http://localhost:5102` | `/api` |
| `hubUrl` | SignalR base URL (`API_BASE_URL`/`HUB_BASE_URL` tokens in `CinemaLib`) | `http://localhost:5102/hubs` | `/hubs` |

### CinemaUser-specific
| Key | Purpose | Dev default | Prod default |
|---|---|---|---|
| `googleClientId` | Google Sign-In button; blank disables the feature client-side | a real client ID is committed in dev (`616638523895-...apps.googleusercontent.com`) | `""` |
| `facebookAppId` | Facebook login button; blank disables the feature client-side | `""` | `""` |

### CinemaAdmin-specific
| Key | Purpose | Dev default | Prod default |
|---|---|---|---|
| `staffAppUrl` | Cross-app link to CinemaStaff (used from `/forbidden` and the staff-account login notice) | `http://localhost:4203` | `http://localhost:4203` (comment: "Set per deployment" — not yet parameterized for prod) |

### CinemaStaff-specific
| Key | Purpose | Dev default | Prod default |
|---|---|---|---|
| `adminAppUrl` | Cross-app link to CinemaAdmin | `http://localhost:4201` | `/admin` |

## 3. Dev tooling (not app config, but required to run certain flows locally)

| Tool | Purpose |
|---|---|
| ngrok | Public tunnel so VNPay's sandbox can reach the local IPN callback — see `integrations.md` §5. |
| `dotnet user-secrets` | Recommended way to set `JWT:Secret` (and other secrets) in Development without committing them. |
