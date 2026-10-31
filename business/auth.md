# Auth & Authorization — Cinema Platform

Dense reference for AI consumption. Source of truth: code under `cinemabe/Cinema/2-Business/{Cinema.Business.Contracts,Cinema.Business,Cinema.Business.DTO}/Auth`, `Cinema.Data/Services/JwtTokenService.cs`, `IdentityController.cs`, `CinemaController.cs`, `PaymentController.cs`, and `cinemafe/projects/CinemaLib/src/lib/{guards,interceptors,store/auth,models/roles.models.ts}`. Verified 2026-10-31 against current `master`-descended branch — the historical "staff roles" effort has been **removed**; only 3 roles exist today.

## 1. Login methods

All login endpoints: `POST api/Identity/*`, `[EnableRateLimiting(RateLimitPolicies.Auth)]` (10 req/min per client IP, fixed window, `QueueLimit=0` → 429 `Too many requests. Please wait and try again.` + `Retry-After` header on overflow).

### 1.1 Password login — `POST api/Identity/Login` (`LoginRequest{EmailOrPhone, Password}`)

Logic in `AuthManager.LoginAsync`, exact order:

| Step | Rule |
|---|---|
| 1 | Look up by `UserStore.GetByEmailAsync` then `GetByPhoneAsync` (single field accepts either). Not found → `UnauthorizedAccessException("Invalid credentials.")` → 401. |
| 2 | If `LockoutEndUtc` is in the future → 401 `"Account locked due to too many failed attempts. Try again in {n} minute(s)."` (lockout policy below). |
| 3 | `PasswordHasher.Verify` (PBKDF2-SHA256, 100k iterations, 32-byte key, 16-byte salt, `FixedTimeEquals`); also transparently verifies **legacy** single-round HMAC-SHA512 hashes (`IsLegacy` = salt length ≠ 16 bytes). Wrong password → increments failure counter (`RegisterFailedLoginAsync`), 401 `"Invalid credentials."`. |
| 4 | `EmailConfirmed == false` → 401 `"Please verify your email address before signing in."` (checked **after** password verification, so a wrong password on an unconfirmed account still reports "Invalid credentials", not confirmation status — no enumeration via error message). |
| 5 | Successful login resets `FailedLoginCount=0`, `LockoutEndUtc=null`. |
| 6 | If the stored hash was legacy, silently rehash to PBKDF2 and persist (transparent migration, no user-visible effect). |
| 7 | If `TwoFactorEnabled` → issue a 2FA code (§6.3) and return `AuthResponse{RequiresTwoFactor=true, Token=""}` — **no token yet**. |
| 8 | Else → `BuildAuthResponse` (full JWT). |

**Account lockout policy**: `_maxFailedLogins = 5`, `_lockoutDuration = 15 minutes`. On the 5th consecutive failure, `LockoutEndUtc = now + 15min` and the counter resets to 0 (the lockout window, not the counter, now governs). Any successful login clears both.

### 1.2 Google login — `POST api/Identity/GoogleLogin` (`GoogleLoginRequest{IdToken}`)

`GoogleTokenValidator.ValidateAsync` verifies the ID token via `GoogleJsonWebSignature.ValidateAsync` against `Google:ClientId` (throws `InvalidOperationException` / 400 if unconfigured). Invalid token → `null` → 401 `"Invalid Google token."`. `EmailVerified == false` on the Google payload → 401 `"Google account email is not verified."`.

**Account linking by email**: lookup `UserStore.GetByEmailAsync(info.Email)`.
- **No match** → auto-creates a `Customer` user: random unguessable password (32 random bytes hashed — login via password is impossible), placeholder phone `"g" + 15 hex chars` (phone is unique+required, Google gives none), `EmailConfirmed = true` immediately.
- **Match found, not yet confirmed** → signing in with Google proves email ownership, so the existing account is auto-confirmed (`EmailConfirmed = true`) — this is the account-linking path for a user who registered by password first but never verified.
- **Match found, already confirmed** → logs into the existing account as-is (whatever role it currently has — not forced to Customer).

**2FA bypass**: Google (and Facebook) logins **always** call `BuildAuthResponse` directly — `TwoFactorEnabled` is never checked. Comment in code: "Google logins bypass the app's own 2FA (Google enforces its own)."

### 1.3 Facebook login — `POST api/Identity/FacebookLogin` (`FacebookLoginRequest{AccessToken}`)

`FacebookTokenValidator.ValidateAsync`: if `Facebook:AppSecret` is configured, first calls Graph `debug_token` to confirm the token's `app_id` matches and `is_valid`; invalid → `null` → 401 `"Invalid Facebook token."`. Then fetches `/me?fields=id,name,email`; if the token lacks email permission (`email` empty) → `null` → same 401 (no distinct error — the API cannot tell "bad token" from "no email permission" apart from the caller's perspective, both are the one error path).

Account-linking logic identical to Google: lookup by email, auto-create as `Customer` with placeholder phone `"f" + 15 hex chars` and `EmailConfirmed=true`, auto-confirm an existing unconfirmed match. **2FA is bypassed** the same way (no app secret check gate on this; it's identical by omission).

## 2. Registration — `POST api/Identity/Register` (`RegisterRequest{Name, Email, Phone, Password, ConfirmPassword}`)

| Validation | Rule |
|---|---|
| DTO-level | `Name` 2-200 chars; `Email` valid format, ≤256; `Phone` `[Phone]` format, 8-20 chars; `Password` 8-100 chars; `ConfirmPassword` must `[Compare]`-match `Password` (400 if not, before reaching the manager). |
| `AuthManager.RegisterAsync` | Email already in use → `InvalidOperationException("Email already in use.")` → 400. Phone already in use → 400 `"Phone already in use."`. Requires a `UserType` row named exactly `"Customer"` to exist (seeded) — missing → 400 `"Customer user type not found."`. |

New user: `EmailConfirmed = false`, a 32-byte random verification token (hex), hashed with SHA-256 and stored as `EmailVerificationTokenHash`, expiring in 24h (`EmailVerificationExpiresAt`). A verification email is sent with a link `/auth/verify-email?email=...&token={raw token}` (raw token only ever leaves the server in this email — only the hash is persisted). **Registration returns a full `AuthResponse` with a usable JWT immediately** — the user is logged in even though the email isn't confirmed yet; however subsequent **password** logins are blocked until confirmed (§1.1 step 4). The JWT issued at registration keeps working until it expires regardless of confirmation state (no server-side revocation).

## 3. JWT

`JwtTokenService.GenerateToken` (HMAC-SHA256, `JWT:Secret`/`JWT:Issuer`/`JWT:Audience`/`JWT:ExpiryHours` from config):

| Claim | Value |
|---|---|
| `sub` | `user.Id` (Guid) |
| `email` | `user.Email` |
| `ClaimTypes.Name` | `user.Name` |
| `ClaimTypes.Role` | `user.UserType?.Name ?? "Customer"` — **this is the one and only role claim**, one role per user |
| `jti` | new Guid per token (no replay/blacklist tracking server-side; purely informational) |
| `theaterId` (custom, conditional) | Present **only** if `user.TheaterId` is set (i.e. `TheaterStaff` accounts). Absent for `Admin`/`Customer`. |

- Expiry: `JWT:ExpiryHours` (default 24h if unset) from issue time; `AuthResponse.ExpiresAt` mirrors it. No refresh-token mechanism exists — the client must log in again after expiry.
- Server start refuses to boot if `JWT:Secret` is missing or the insecure built-in default (checked in `Program.cs`).
- `ClaimsPrincipalExtensions.GetUserId()` reads `ClaimTypes.NameIdentifier` or `sub`. `GetUserRole()` reads the role claim directly. `GetTheaterId()` / `GetTheaterIds()` parse the `theaterId` claim(s) (only ever one per current token, but the helper supports multiple).

## 4. Current role model (verified from code — superseded any older "Admin/TheaterStaff+variants/Customer" assumption)

**Exactly 3 roles exist** (`RoleNames.cs`, `UserType.Name` values, mirrored 1:1 in frontend `roles.models.ts`):

| Role | Meaning |
|---|---|
| `Admin` | Full access to everything; the only role with no theater scoping. |
| `TheaterStaff` | Single consolidated staff role (absorbed what used to be separate staff role types — box office, gate, concession, warehouse, manager, etc. are now **capability composites gated in code**, not separate roles/UserTypes). Always theater-scoped (`theaterId` claim required). |
| `Customer` | Default role for self-registered / social-login users. |

There is **no permission table** — `[Authorize(Roles=...)]` composites (comma = "any of") are hard-coded constants in `RoleNames.cs`, all currently resolving to either `Admin` alone or `Admin,TheaterStaff`:

| Composite | Resolves to | Gates |
|---|---|---|
| `RoleNames.Admin` | `Admin` | Admin-only actions (identical effect to `Admin` literal) |
| `RoleNames.Customer` | `Customer` | (not used in any `[Authorize]` seen — customer-only paths just use plain `[Authorize]` + ownership checks by user id) |
| `RoleNames.TheaterStaff` | `TheaterStaff` | — |
| `RoleNames.StaffApp` | `Admin,TheaterStaff` | Login to the CinemaStaff app; general staff-area endpoints (seat map admin, operations board, workforce roster, etc.) |
| `RoleNames.Sellers` | `Admin,TheaterStaff` | POS / cash drawer / reprint / customer lookup (box office) |
| `RoleNames.GateKeepers` | `Admin,TheaterStaff` | Ticket scanning/admission at the gate |
| `RoleNames.Concession` | `Admin,TheaterStaff` | Food/drink counter sale + pickup queue |
| `RoleNames.BackOffice` | `Admin,TheaterStaff` | Warehouse/inventory part of staff app (scope narrowed to own theater in code, see below) |
| `RoleNames.StockApprovers` | `Admin` only | Approve/reject storage plans, change inventory tracking settings |
| `RoleNames.Approvers` | `Admin` only | Manager-override PIN approvals, drawer reconciliation, compensation, rosters |
| `RoleNames.Reporting` | `Admin` only | Daily close, sales reporting, audit log |
| `RoleNames.TheaterScopedRoles` | `TheaterStaff` | Not an authz filter — used by `AuthManager.ResolveTheaterIdAsync` to decide which `UserType`s *require* a `TheaterId` on create/update |

**Since `TheaterStaff` is the only non-Admin staff value today, every "composite" other than the three Admin-only ones is behaviorally identical to `StaffApp` (`Admin,TheaterStaff`).** They remain distinct named constants so a future reintroduction of finer-grained staff roles only requires editing `RoleNames.cs`, not every controller action.

**Controller-level breakdown** (from grep of all 3 controllers):
- `IdentityController`: plain `[Authorize]` on profile/password/2FA self-service actions (any authenticated role); `[Authorize(Roles="Admin")]` (local const, not `RoleNames`) on `GetUsers`/`CreateUser`/`UpdateUser`/`DeleteUser`.
- `PaymentController`: class-level `[Authorize]`; `[Authorize(Roles="Admin")]` (local const) on admin payment-management actions (6 actions).
- `CinemaController`: 86 actions gated `[Authorize(Roles=_adminRole)]` ("Admin" local const — catalog/content management: movies, theaters, rooms, pricing, promotions, etc.), 2 plain `[Authorize]` (any logged-in user — e.g. booking/comment self-service), plus the `RoleNames.*` composites above concentrated in the `#region`s: `Concession`, `Gate`, `BoxOffice`, `StaffReport`, `Operations`, `Workforce` (lines ~1664–2464).

**Scope resolution helpers** (`ClaimsPrincipalExtensions`):
- `TryGetBackOfficeScope`: Admin → unrestricted (`theaterScope=null`); `BackOffice` role → pinned to `GetTheaterId()`, fails (→ caller must answer 403, not 401) if no theater claim or role isn't back-office at all.
- `TryGetStaffScope`: Admin → `StaffScope(isAdmin:true, [])`; theater-scoped role → `StaffScope(false, theaterIds)`, fails if no theater ids. Convention throughout: a role/scope mismatch on an authenticated token is **403 Forbidden**, never 401 — 401 is reserved for "not authenticated at all" because the frontend force-logs-out on 401 (see §7).
- User creation/update (`AuthManager.ResolveTheaterIdAsync`): any `UserType` in `RoleNames.TheaterScopedRoles` (today, only `TheaterStaff`) **requires** a valid, existing `TheaterId`, else 400 `"A theater is required for the {role} role."` / `"Theater not found."`. Non-scoped roles (`Admin`, `Customer`) force `TheaterId = null` regardless of what's requested.

## 5. SignalR `/hubs` access_token auth

Configured once in `Program.cs` `AddJwtBearer().Events.OnMessageReceived`: if the request path starts with `/hubs` **and** a non-empty `access_token` query-string value is present, that value is used as the bearer token instead of the `Authorization` header (browsers can't set custom headers on the WebSocket handshake). Applies uniformly to both hubs; normal HTTP endpoints still require the `Authorization: Bearer` header.

| Hub | Route | Authorize | Notes |
|---|---|---|---|
| `BookingHub` | `/hubs/booking` | `[Authorize]` (any authenticated role) | Used for live seat-lock updates during booking. |
| `StaffHub` | `/hubs/staff` | `[Authorize(Roles = RoleNames.StaffApp)]` (`Admin,TheaterStaff`) | Events: `FoodOrderQueued`, `FoodOrderUpdated`, `StockLow`, `IncidentRaised`, broadcast per `theater:{id}` SignalR group. On connect, auto-joins the groups for every `theaterId` claim the token carries (Admin has none, joins none automatically). `JoinTheater(theaterId)` lets a client subscribe explicitly — Admin may join any theater; others only a theater already in their claims, else throws `HubException("You do not have access to this theater.")`. |

## 6. Password reset / email verification / 2FA

### 6.1 Forgot/reset password
- `POST api/Identity/ForgotPassword` (`ForgotPasswordRequest{Email}`, rate limit `AuthEmail` = 3/15min/IP): looks up by email; **if not found, returns 204 silently** — no enumeration. If found: 32-byte random token, SHA-256-hashed and stored (`PasswordResetTokenHash`), 1-hour expiry (`PasswordResetExpiresAt`), emails a link `/auth/reset-password?email=...&token={raw}`.
- `POST api/Identity/ResetPassword` (`ResetPasswordRequest{Email, Token, NewPassword}`, rate limit `Auth`): validates token hash (`FixedTimeEquals` over SHA-256(token)) and expiry; any mismatch/expiry/missing-hash → `InvalidOperationException("Invalid or expired reset token.")` → 400. On success: rehashes password (fresh PBKDF2 salt), clears the reset token/expiry (single-use).

### 6.2 Email verification
- `POST api/Identity/ConfirmEmail` (`ConfirmEmailRequest{Email, Token}`, rate limit `Auth`): same hash/expiry pattern as reset; on success sets `EmailConfirmed=true` and clears the verification token (single-use, 24h expiry from registration).
- `POST api/Identity/ResendVerification` (`ResendVerificationRequest{Email}`, rate limit `AuthEmail`): silent no-op (204) if the email doesn't exist or is already confirmed (no enumeration); otherwise issues a fresh token/expiry and resends.

### 6.3 Two-factor (email + SMS code, opt-in per account)
- Toggle: `POST api/Identity/SetTwoFactor` (`[Authorize]`, `SetTwoFactorRequest{Enabled}`) — sets `TwoFactorEnabled`; disabling also clears any outstanding code.
- When enabled, a **password** login (§1.1 step 7) short-circuits: generates a 6-digit numeric code (`RandomNumberGenerator`), SHA-256-hashes it (`TwoFactorCodeHash`), 5-minute expiry, emails it ("Your Cinema login code") and **also SMS's it** if the user has a phone number (via `ISmsNotificationService` — dev-logs unless Twilio configured). Returns `AuthResponse{RequiresTwoFactor=true}` with **no token**.
- `POST api/Identity/VerifyTwoFactor` (`VerifyTwoFactorRequest{EmailOrPhone, Code}` — code must regex `^\d{6}$`, rate limit `Auth`): looks up by email-or-phone; `TwoFactorEnabled=false`, no pending code, expired, or hash mismatch → `UnauthorizedAccessException("Invalid or expired code.")` → 401. On success: consumes the code (clears hash/expiry, single-use) and returns the full `AuthResponse` with JWT.
- **2FA applies only to password login.** Google/Facebook logins never check `TwoFactorEnabled` (§1.2/1.3) — the social provider's own authentication substitutes for it, even if the account has 2FA turned on.

## 7. Frontend (CinemaLib) behavior

| Piece | File | Behavior |
|---|---|---|
| `authGuard` | `guards/auth.guard.ts` | `CanActivateFn`; reads `selectIsAuthenticated`; if false, redirects to `/auth/login?returnUrl={attempted url}`. |
| `adminGuard` | `guards/admin.guard.ts` | Allows only `user.userTypeName === 'Admin'`; else redirects to `/forbidden` (deliberately kept **outside** any guarded route tree to avoid redirect loops — documented gotcha). |
| `roleGuard(roles)` | `guards/role.guard.ts` | Factory: allows if `user.userTypeName` is in the given list, else `/forbidden`. Also exports `homeRedirectGuard` (Admin→`/dashboard`, else `/forbidden`) and `homeRedirectByRole(map, fallback)` for per-app landing routing. |
| `authInterceptor` | `interceptors/auth.interceptor.ts` | Reads `selectToken` from the NgRx store and attaches `Authorization: Bearer {token}` to every outgoing request when a token exists; otherwise passes the request through unmodified. |
| `errorInterceptor` | `interceptors/error.interceptor.ts` | **401** → dispatches `logout()` + navigates to `/auth/login` (hard client-side logout on any auth failure — this is why the backend deliberately avoids 401 for role/scope mismatches, see §4). **403** → toast with the specific server message if it matches the known `api-error-catalog`, else a generic "forbidden" toast (no logout/redirect). **0** (network) → generic network-error toast. **≥500** → generic server-error toast. 400/404 are left to component-level handling (inline validation). |
| NgRx auth store | `store/auth/*` | `auth.effects.ts`: `login$` calls `IdentityServiceAgent`; an empty-token response dispatches `twoFactorRequired()` instead of `loginSuccess` (mirrors `RequiresTwoFactor`). `loginSuccess$`/`registerSuccess$` persist via `TokenStorage.save` and navigate to the sanitized `returnUrl` (must start with `/` and not `//`, else defaults to `/` — open-redirect guard) — register and login-without-explicit-choice default `rememberMe=true`. `logout$` clears storage and navigates to `/auth/login`. `auth.reducer.ts` handles `loadUserFromStorage` to rehydrate state from `TokenStorage` on app bootstrap, and `profileUpdated` re-persists the cached user (so a profile edit survives reload). |
| `TokenStorage` | `store/auth/token-storage.ts` | "Remember me" → `localStorage` (survives browser close); otherwise `sessionStorage`. Saving to one explicitly clears the other so only one authoritative copy exists; reads fall back across both. |
| `roles.models.ts` | `models/roles.models.ts` | Mirrors `RoleNames.cs` 1:1 (`UserRoles.{Admin,Customer,TheaterStaff}` + the same composite arrays: `BACK_OFFICE_ROLES`, `STOCK_APPROVER_ROLES`, `STAFF_APP_ROLES`, `SELLER_ROLES`, `COMPLAINT_ROLES`, `GATE_KEEPER_ROLES`, `CONCESSION_ROLES`, `APPROVER_ROLES`, `REPORTING_ROLES`, `THEATER_SCOPED_ROLES`). Single source note: "what each role may do is defined here and on the API, not in the database." |

## 8. Edge cases & exact error responses

| Scenario | Status | Body shape |
|---|---|---|
| Unhandled exception reaching `ExceptionMiddleware` (bypasses controller try/catch, e.g. in middleware/filters) | per exception type: `KeyNotFoundException`→404, `UnauthorizedAccessException`→401, `InvalidOperationException`→400, else→500 `"An unexpected error occurred."` | `{ error, statusCode }` (camelCase JSON) |
| Exception caught in a controller action (`ApiControllerBase.HandleException`) | `KeyNotFoundException`→404, `AccessDeniedException`→**403** (never 401, deliberately — see §4/§7), `UnauthorizedAccessException`→401, `InvalidOperationException`→400, else→500 | `{ error, statusCode }` |
| `[Authorize]`/`[Authorize(Roles=...)]` failure at the ASP.NET pipeline level (no token, or wrong role) | Framework default: 401 (no token) / 403 (token present, role insufficient) — not routed through `HandleException` | ASP.NET default empty body |
| Rate limit exceeded (`Auth`/`AuthEmail` policies) | 429, `Retry-After` header set from the limiter's metadata | Plain text `"Too many requests. Please wait and try again."` |
| Login with correct password but unconfirmed email | 401 `"Please verify your email address before signing in."` | Checked after password is verified — does **not** leak confirmation state to a wrong-password guess |
| Login during active lockout | 401 `"Account locked due to too many failed attempts. Try again in {n} minute(s)."` | `n` = `Ceiling(minutes remaining)` |
| Forgot-password / resend-verification for unknown or already-confirmed email | 204 (silent success) | No body — no enumeration |
| Google/Facebook token valid but email missing/unverified | 401 `"Google account email is not verified."` / generic `"Invalid Facebook token."` (Facebook can't distinguish "no email permission" from "bad token") | |
| Social/password provider not configured (`Google:ClientId` / `Facebook:AppId` missing) | 400 (`InvalidOperationException`) | `"Google login is not configured (Google:ClientId is missing)."` / Facebook equivalent |
| Reset/confirm-email token wrong, reused, or expired | 400 `"Invalid or expired reset token."` / `"Invalid or expired verification token."` | Single-use — token hash is cleared after one successful use, so replay also hits this path |
| 2FA code wrong, reused, expired, or 2FA not enabled for that account | 401 `"Invalid or expired code."` | Same message for all four causes — no information leak about *why* |
| Theater-scoped role (`TheaterStaff`) created/updated without a valid `TheaterId` | 400 `"A theater is required for the {role} role."` or `"Theater not found."` | |
| Authenticated user hits an endpoint outside their role/theater scope | **403**, never 401 — explicit design choice because the frontend's `errorInterceptor` force-logs-out on 401 | `{ error, statusCode }` via `AccessDeniedException` path, or framework 403 for a plain `[Authorize(Roles=...)]` mismatch |
| Frontend: any 401 from any API call | Client dispatches `logout()` + redirects to `/auth/login`, regardless of which endpoint triggered it | — |
