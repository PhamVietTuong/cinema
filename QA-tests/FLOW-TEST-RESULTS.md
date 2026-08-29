# Flow Test Result — booking-seat-lock — 2026-06-20 12:17
**Flow**: Seat locking + booking → Invoice lifecycle (Business + Data + Service + FE)   **Layers**: business, data, service, frontend
**Changed scope detected**: cinemabe `BookingManager.cs`, `InvoiceManager.cs`, Booking DTOs (`CreateBookingRequest.cs`, `BookingResultDTO.cs`, `SeatDTO.cs`), `InvoiceDto.cs`, `Seat.cs`, `InvoiceTicket.cs`; cinemafe `seat-selection` + `booking-confirmation` components

> ✅ **No P0 alert raised this run.** All 9 static checks PASS and all 3 build/test checks PASS. SC-BOOK-05 was reconciled to the real guard expression (`InvoiceStatus.Pending` + `userId` in `CancelBookingAsync`) and now passes — RI-BOOK-05 is no longer triggered by static analysis. This supersedes the 19:06 run below (which failed on the obsolete literal `InvoiceStatus.Paid` pattern).

## §1 Static checks (9/9 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-BOOK-01 | P0 | ✅ PASS | `static`, `ConcurrentDictionary`, `_lockedSeats` all present (BookingManager.cs line 17) |
| SC-BOOK-02 | P0 | ✅ PASS | `TimeSpan.FromMinutes(5)` present in `IsSeatLocked` (line 192) — 5-min lock expiry intact |
| SC-BOOK-03 | P0 | ✅ PASS | `UnlockSeat` compares `info.ConnectionId == connectionId` (line 183) — owner-scoped release |
| SC-BOOK-04 | P0 | ✅ PASS | `CreateBookingAsync` sets `Status = InvoiceStatus.Pending` (line 127); wrapped in `BeginTransactionAsync`/`CommitTransactionAsync`/`RollbackTransactionAsync` (Transaction) |
| SC-BOOK-05 | P0 | ✅ PASS | `CancelBookingAsync` (lines 166–175) contains `InvoiceStatus.Pending` guard `if (invoice.Status != InvoiceStatus.Pending) return false;` (line 170, ✅) AND `userId` ownership check `invoice.UserId != userId` (line 169, ✅). Reconciled check now matches the real guard. |
| SC-BOOK-06 | P0 | ✅ PASS | No hardcoded magic-integer status assignment/comparison in BookingManager.cs or InvoiceManager.cs (forbidden regex: 0 matches) |
| SC-BOOK-07 | P1 | ✅ PASS | `InvoiceStatus` enum declares all 4: `Pending=0, Paid=1, Cancelled=2, Failed=3` |
| SC-BOOK-08 | P1 | ✅ PASS | `SeatStatus` enum declares all 3: `Available=0, Reserved=1, Occupied=2` |
| SC-BOOK-09 | P1 | ✅ PASS | `seat-selection.component.html` renders `available`, `occupied`, `locked` (+ `selected`, `vip`) seat states (lines 36–40) |

## §2 Build + test checks (3/3 PASS)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-BOOK-BUILD | ✅ PASS (exit 0) | Cinema.Business → built. `Build succeeded. 0 Warning(s) 0 Error(s)` (6.18s) |
| BC-BOOK-TEST | ✅ PASS (exit 0) | BookingServiceTests: `Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9` (only NU1603 restore warnings, non-blocking) |
| BC-BOOK-FE | ✅ PASS (exit 0) | CinemaUser build complete (6.283s). seat-selection + booking-confirmation chunks emitted. Not skipped (booking FE files changed). |

## §3 Playbook to run manually

**Prerequisites:**
- Backend running: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
- Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (admin@cinema.vn / user@cinema.vn)
- DB seeded with ≥1 Movie, ≥1 Theater/Room with a seat map, ≥1 ShowTime
- FE: `ng serve CinemaUser` (http://localhost:4202)

### PB-BOOK-01 — Happy path: pick seats → create booking → confirm payment (P0)
1. Login as user@cinema.vn, open a movie's showtime → `/booking/seats?showTimeId=...&roomId=...`
   - Expected: Seat grid renders; available seats clickable, occupied seats not
2. Select 2 available seats → click 'XÁC NHẬN ĐẶT VÉ'
   - Expected: Navigates to `/booking/confirmation`
   - Expected: DB Invoice created with Status = Pending (0), Code matching `CIN{yyyyMMddHHmmss}{NNNN}`
   - Expected: DB 2 InvoiceTicket rows linked to the invoice
3. Choose a payment method → 'XÁC NHẬN & THANH TOÁN'
   - Expected: Success page shows a ticket-code
   - Expected: DB Invoice.Status = Paid (1)

### PB-BOOK-02 — Concurrent lock: two clients cannot book the same seat (P0)
1. Client A opens the seat grid and selects seat R5 (SignalR LockSeat fires)
   - Expected: Client B's grid shows R5 as 'locked' within ~1s
2. Client B tries to select R5
   - Expected: Selection rejected (seat is locked by A)
3. Client A abandons the page without booking; wait 5 minutes
   - Expected: R5 auto-expires (IsSeatLocked 5-min window) and becomes available to B again

### PB-BOOK-03 — Cancel guards (P0)
1. User cancels their own Pending booking
   - Expected: DB Invoice.Status = Cancelled (2); seats freed
2. User attempts to cancel a booking they do not own (different userId)
   - Expected: Rejected (returns false / 4xx), invoice unchanged
3. User attempts to cancel an already-Paid invoice
   - Expected: Rejected, status stays Paid

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-BOOK-01 | P0 | ✅ Not triggered | static SC-BOOK-01 PASS |
| RI-BOOK-02 | P0 | ✅ Not triggered (static) / verify manually | static SC-BOOK-02 PASS; PB-BOOK-02 step 3 manual |
| RI-BOOK-03 | P0 | ✅ Not triggered (static) / verify manually | static SC-BOOK-03 PASS; PB-BOOK-02 step 2 manual |
| RI-BOOK-04 | P0 | ✅ Not triggered (static) / verify manually | static SC-BOOK-04 PASS; PB-BOOK-01 step 2 manual |
| RI-BOOK-05 | P0 | ✅ Not triggered (static) / verify manually | static **SC-BOOK-05 PASS** (reconciled); PB-BOOK-03 steps 2–3 manual |
| RI-BOOK-06 | P0 | ✅ Not triggered | static SC-BOOK-06 PASS |
| RI-BOOK-07 | P1 | ✅ Not triggered (static) / verify manually | static SC-BOOK-09 PASS; PB-BOOK-01 step 1 manual |

## §5 Summary
- **0 of 7 regression indicators triggered.** No P0 alert raised this run.
- **Static: 9/9 PASS** — SC-BOOK-05 now passes after reconciliation to the real guard (`InvoiceStatus.Pending` + `userId` in `CancelBookingAsync`).
- **Build/tests: 3/3 OK** — Business build (0 errors), 9/9 xUnit BookingServiceTests, CinemaUser FE build all green. BC-BOOK-FE ran (not skipped) since booking FE files changed.
- **Action expected:** None blocking. The 3 P0 playbook scenarios (PB-BOOK-01/02/03) still require a manual E2E run — they cannot be auto-verified by static/build analysis.

---

# 🔴 P0 ALERT — booking-seat-lock — 2026-06-20 19:06

**Triggered P0 regression indicators:**
- **RI-BOOK-05** — Cancel guards broken → paid invoice cancellable or cross-user cancellation. Source: static **SC-BOOK-05 FAIL**.

**Detail:** SC-BOOK-05 isolates the `CancelBookingAsync` method body and requires the literal pattern `InvoiceStatus.Paid` (the paid-cancellation guard). The current `CancelBookingAsync` (BookingManager.cs lines 166–175) guards via `if (invoice.Status != InvoiceStatus.Pending) return false;` — which functionally *does* refuse cancelling a Paid invoice — but it never references `InvoiceStatus.Paid` by name, so the literal `must_contain` pattern is absent. The only `InvoiceStatus.Paid` in the file is in `ConfirmPaymentAsync` (line 158), outside the scoped method.

**Action expected before push:** Confirm with the dev whether this is (a) an acceptable refactor of the guard (status==Pending implies not-Paid) — in which case **update SC-BOOK-05** in `booking-seat-lock.yaml` to match the new guard expression (e.g. `InvoiceStatus.Pending`), or (b) an unintended weakening of the explicit paid-guard — in which case restore an explicit `InvoiceStatus.Paid` check. Do not push until SC-BOOK-05 reconciles. (Note: `userId` ownership guard IS present and passes.)

---

# Flow Test Result — booking-seat-lock — 2026-06-20 19:06
**Flow**: Seat locking + booking → Invoice lifecycle (Business + Data + Service + FE)   **Layers**: business, data, service, frontend
**Changed scope detected**: cinemabe `BookingManager.cs`, `InvoiceManager.cs`, Booking DTOs (`BookingResultDTO.cs`, `CreateBookingRequest.cs`, `SeatDTO.cs`), `InvoiceDTO.cs`, `Seat.cs`, `InvoiceTicket.cs`; cinemafe `seat-selection` + `booking-confirmation` components

## §1 Static checks (8/9 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-BOOK-01 | P0 | ✅ PASS | `static`, `ConcurrentDictionary`, `_lockedSeats` all present (BookingManager.cs line 17) |
| SC-BOOK-02 | P0 | ✅ PASS | `TimeSpan.FromMinutes(5)` present in `IsSeatLocked` (line 192) — 5-min lock expiry intact |
| SC-BOOK-03 | P0 | ✅ PASS | `UnlockSeat` compares `info.ConnectionId == connectionId` (line 183) — owner-scoped release |
| SC-BOOK-04 | P0 | ✅ PASS | `CreateBookingAsync` sets `Status = InvoiceStatus.Pending` (line 127); runs inside `BeginTransactionAsync`/`CommitTransactionAsync`/`RollbackTransactionAsync` (Transaction) |
| SC-BOOK-05 | P0 | ❌ FAIL | `CancelBookingAsync` (lines 166–175) contains `userId` ownership check (✅) but does NOT contain literal `InvoiceStatus.Paid` (❌). Guard is implemented as `status != Pending → return false` instead. Functionally rejects Paid cancellation, but the required pattern is absent in the scoped method. |
| SC-BOOK-06 | P0 | ✅ PASS | No hardcoded magic-integer status assignment/comparison in BookingManager.cs or InvoiceManager.cs (forbidden regex: 0 matches) |
| SC-BOOK-07 | P1 | ✅ PASS | `InvoiceStatus` enum declares all 4: `Pending=0, Paid=1, Cancelled=2, Failed=3` |
| SC-BOOK-08 | P1 | ✅ PASS | `SeatStatus` enum declares all 3: `Available=0, Reserved=1, Occupied=2` |
| SC-BOOK-09 | P1 | ✅ PASS | `seat-selection.component.html` renders `available`, `occupied`, `locked` (+ `selected`, `vip`) seat states (lines 36–40) |

## §2 Build + test checks (3/3 PASS)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-BOOK-BUILD | ✅ PASS (exit 0) | Cinema.Business → built. `Build succeeded. 0 Warning(s) 0 Error(s)` |
| BC-BOOK-TEST | ✅ PASS (exit 0) | BookingServiceTests: `Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9` (only NU1603 restore warnings, non-blocking) |
| BC-BOOK-FE | ✅ PASS (exit 0) | CinemaUser build complete (48.99s). seat-selection + booking-confirmation chunks emitted. Not skipped (booking FE files changed). |

## §3 Playbook to run manually

**Prerequisites:**
- Backend running: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
- Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (admin@cinema.vn / user@cinema.vn)
- DB seeded with ≥1 Movie, ≥1 Theater/Room with a seat map, ≥1 ShowTime
- FE: `ng serve CinemaUser` (http://localhost:4202)

### PB-BOOK-01 — Happy path: pick seats → create booking → confirm payment (P0)
1. Login as user@cinema.vn, open a movie's showtime → `/booking/seats?showTimeId=...&roomId=...`
   - Expected: Seat grid renders; available seats clickable, occupied seats not
2. Select 2 available seats → click 'XÁC NHẬN ĐẶT VÉ'
   - Expected: Navigates to `/booking/confirmation`
   - Expected: DB Invoice created with Status = Pending (0), Code matching `CIN{yyyyMMddHHmmss}{NNNN}`
   - Expected: DB 2 InvoiceTicket rows linked to the invoice
3. Choose a payment method → 'XÁC NHẬN & THANH TOÁN'
   - Expected: Success page shows a ticket-code
   - Expected: DB Invoice.Status = Paid (1)

### PB-BOOK-02 — Concurrent lock: two clients cannot book the same seat (P0)
1. Client A opens the seat grid and selects seat R5 (SignalR LockSeat fires)
   - Expected: Client B's grid shows R5 as 'locked' within ~1s
2. Client B tries to select R5
   - Expected: Selection rejected (seat is locked by A)
3. Client A abandons the page without booking; wait 5 minutes
   - Expected: R5 auto-expires (IsSeatLocked 5-min window) and becomes available to B again

### PB-BOOK-03 — Cancel guards (P0)
1. User cancels their own Pending booking
   - Expected: DB Invoice.Status = Cancelled (2); seats freed
2. User attempts to cancel a booking they do not own (different userId)
   - Expected: Rejected (returns false / 4xx), invoice unchanged
3. User attempts to cancel an already-Paid invoice
   - Expected: Rejected, status stays Paid

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-BOOK-01 | P0 | ✅ Not triggered | static SC-BOOK-01 PASS |
| RI-BOOK-02 | P0 | ✅ Not triggered (static) / verify manually | static SC-BOOK-02 PASS; PB-BOOK-02 step 3 manual |
| RI-BOOK-03 | P0 | ✅ Not triggered (static) / verify manually | static SC-BOOK-03 PASS; PB-BOOK-02 step 2 manual |
| RI-BOOK-04 | P0 | ✅ Not triggered (static) / verify manually | static SC-BOOK-04 PASS; PB-BOOK-01 step 2 manual |
| RI-BOOK-05 | P0 | 🔴 TRIGGERED | static **SC-BOOK-05 FAIL** (also verify PB-BOOK-03 steps 2–3 manually) |
| RI-BOOK-06 | P0 | ✅ Not triggered | static SC-BOOK-06 PASS |
| RI-BOOK-07 | P1 | ✅ Not triggered (static) / verify manually | static SC-BOOK-09 PASS; PB-BOOK-01 step 1 manual |

## §5 Summary
- **1 of 7 regression indicators triggered** (RI-BOOK-05, P0)
- **Build/tests: OK** (3/3 — Business build, 9/9 xUnit tests, CinemaUser FE build all green)
- **Static: 8/9 PASS** (SC-BOOK-05 FAIL on literal `InvoiceStatus.Paid` pattern in `CancelBookingAsync`)
- **Action expected:** P0 alert raised. The cancel-guard refactor (`status != Pending` instead of an explicit `== Paid` check) is functionally equivalent for the paid case but no longer matches the SC-BOOK-05 contract. Before push, the dev must either (a) reconcile the YAML check to the new guard expression, or (b) restore an explicit `InvoiceStatus.Paid` guard. The `userId` ownership half of SC-BOOK-05 passes. All other P0 indicators clear by static; PB-BOOK-01/02/03 still need a manual E2E run.

# Flow Test Result — auth-login — 2026-06-21 13:50
**Flow**: Authentication — login, register, JWT issuance, role-based gating   **Layers**: business, service, frontend
**Changed scope detected**: none (no tracked code changes; only untracked QA-tests/ and .claude/). Auth source files unchanged.

## §1 Static checks (5/5 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-AUTH-01 | P0 | PASS | `LoginAsync` throws `UnauthorizedAccessException("Invalid credentials.")` for unknown user and wrong password (AuthManager.cs L29, L32). |
| SC-AUTH-02 | P0 | PASS | `RegisterAsync` checks `GetByEmailAsync(request.Email)` and throws `InvalidOperationException("Email already in use.")` before creating user (AuthManager.cs L39-40). |
| SC-AUTH-03 | P0 | PASS | `JwtTokenService.GenerateToken` emits `new Claim(ClaimTypes.Role, user.UserType?.Name ?? "Customer")` (JwtTokenService.cs L27). NOTE: actual path is `Cinema.Data/Services/JwtTokenService.cs`, spec YAML lists `Cinema.Data/JwtTokenService.cs` (stale path — file located via search). |
| SC-AUTH-04 | P0 | PASS | All four admin endpoints (GetUsers/CreateUser/UpdateUser/DeleteUser) carry `[Authorize(Roles = _adminRole)]` (IdentityController.cs L119, L137, L155, L173). |
| SC-AUTH-05 | P1 | PASS | `auth.effects.ts` persists JWT via `localStorage.setItem('cinema_token', ...)` and removes it on logout (L43, L54). |

## §2 Build + test checks (2/2 PASS, 1 SKIP)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-AUTH-BUILD | PASS | `dotnet build Cinema.Business.csproj` → Build succeeded. 0 Warning(s), 0 Error(s). Exit 0. |
| BC-AUTH-TEST | PASS | `dotnet test --filter AuthServiceTests` → Passed! Failed: 0, Passed: 7, Skipped: 0. Exit 0. |
| BC-AUTH-FE | SKIP | `skip_if_unchanged: projects/CinemaLib/**` — no tracked changes under CinemaLib this session, so the `npx ng build CinemaLib` step was skipped. |

## §3 Playbook to run manually

**Prerequisites**
- Backend on http://localhost:5102, seed accounts created (admin@cinema.vn / Admin@123, user@cinema.vn / User@123).
- FE: ng serve CinemaUser (4202) and/or CinemaAdmin (4201). (Use nvm v22.12.0 — system Node is too old.)

**PB-AUTH-01 — Login success + role routing (P0)**
1. POST /api/Identity/Login `{email: admin@cinema.vn, password: Admin@123}` → expect 200 with a JWT; decoded token contains role = Admin.
2. Login as admin in CinemaAdmin UI → expect localStorage `cinema_token` set; reaches /dashboard.

**PB-AUTH-02 — Bad credentials + duplicate register (P0)**
1. POST /api/Identity/Login with a wrong password → expect 401 Unauthorized (NOT 200, NOT 500).
2. POST /api/Identity/Register with an email that already exists → expect 4xx with a duplicate-email message; no second user created.

**PB-AUTH-03 — Role gate enforced (P0)**
1. Call an Admin-only endpoint (e.g. GetUsers) with a standard user's token → expect 403 Forbidden.

> Note on PB-AUTH-02 step 1: `LoginAsync` throws `UnauthorizedAccessException`, but `IdentityController.Login`'s catch returns `StatusCode(500, e.Message)` for ALL exceptions. Unless `ExceptionMiddleware` maps `UnauthorizedAccessException` → 401, the HTTP response will be 500, not 401. Verify manually against the running API (see §4 RI-AUTH-01).

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-AUTH-01 | P0 | NOT TRIGGERED (static) / verify manually (playbook) | SC-AUTH-01 PASS. PB-AUTH-02 step 1 needs live check that the HTTP status is 401 (controller catch returns 500 unless middleware remaps). |
| RI-AUTH-02 | P0 | NOT TRIGGERED (static) / verify manually | SC-AUTH-02 PASS. PB-AUTH-02 step 2 confirms at runtime. |
| RI-AUTH-03 | P0 | NOT TRIGGERED (static) / verify manually | SC-AUTH-03 + SC-AUTH-04 both PASS. PB-AUTH-03 confirms 403 at runtime. |
| RI-AUTH-04 | P1 | NOT TRIGGERED | SC-AUTH-05 PASS. |

## §5 Summary
- 0 of 4 regression indicators triggered (static layer all clean).
- Build/tests: OK (Business build clean; 7 AuthServiceTests pass; FE build skipped — CinemaLib unchanged).
- Action expected: None — no defects. Two housekeeping notes:
  1. YAML reconciliation: `static_check SC-AUTH-03` and `trigger_paths` reference `Cinema/3-Data/Cinema.Data/JwtTokenService.cs`, but the file actually lives at `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs`. Update the spec path so the check resolves directly without a fallback search. (Stale check path, not a code defect — no bug file written.)
  2. Manual verification recommended for PB-AUTH-02 step 1: the controller's blanket catch returns HTTP 500 for `UnauthorizedAccessException`; confirm `ExceptionMiddleware` remaps it to 401 as the spec expects. Not a static failure, so no indicator triggered — flagged for the manual playbook.

---

# Flow Test Result — booking-seat-lock — 2026-06-21 13:48
**Flow**: Seat locking + booking → Invoice lifecycle (Business + Data + Service + FE)   **Layers**: business, data, service, frontend
**Changed scope detected**: No booking-flow source files changed vs HEAD (only QA-tests/ + .claude/ harness files). Static checks run on current content; BC-BOOK-FE skipped (booking/** + CinemaLib/** unchanged).

## §1 Static checks (9/9 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-BOOK-01 | P0 | PASS | `static`, `ConcurrentDictionary`, `_lockedSeats` all present (BookingManager.cs:17) |
| SC-BOOK-02 | P0 | PASS | `TimeSpan.FromMinutes(5)` present in `IsSeatLocked` (line 192) |
| SC-BOOK-03 | P0 | PASS | `UnlockSeat` compares `info.ConnectionId == connectionId` (owner-scoped, line 183) |
| SC-BOOK-04 | P0 | PASS | `CreateBookingAsync` sets `InvoiceStatus.Pending` (l.127,143) inside Begin/Commit Transaction (l.61,134) |
| SC-BOOK-05 | P0 | PASS | `CancelBookingAsync` gates on `Status != InvoiceStatus.Pending` (l.170) and `UserId != userId` (l.169) |
| SC-BOOK-06 | P0 | PASS | No hardcoded status integers in BookingManager/InvoiceManager (regex no-match) |
| SC-BOOK-07 | P1 | PASS | InvoiceStatus enum = {Pending, Paid, Cancelled, Failed} |
| SC-BOOK-08 | P1 | PASS | SeatStatus enum = {Available, Reserved, Occupied} |
| SC-BOOK-09 | P1 | PASS | seat-selection.component.html renders `available`/`occupied`/`locked` states |

## §2 Build + test checks (2/2 PASS, 1 SKIP)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-BOOK-BUILD | PASS | `dotnet build Cinema.Business` → Build succeeded, 0 Warning(s), 0 Error(s), exit 0 |
| BC-BOOK-TEST | PASS | BookingServiceTests → Passed! Failed: 0, Passed: 9, Total: 9, exit 0 |
| BC-BOOK-FE | SKIP | skip_if_unchanged matched — `projects/CinemaUser/.../booking/**` and `projects/CinemaLib/**` unchanged vs HEAD |

## §3 Playbook to run manually

**Prerequisites**
- Backend running: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
- Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (admin@cinema.vn / user@cinema.vn)
- DB seeded with ≥1 Movie, ≥1 Theater/Room with a seat map, ≥1 ShowTime
- FE: `ng serve CinemaUser` (http://localhost:4202)

**PB-BOOK-01 — Happy path: pick seats → create booking → confirm payment (P0)**
1. Login as user@cinema.vn, open a movie's showtime → `/booking/seats?showTimeId=...&roomId=...`
   - Expected: Seat grid renders; available seats clickable, occupied seats not.
2. Select 2 available seats → click 'XÁC NHẬN ĐẶT VÉ'
   - Expected: Navigates to `/booking/confirmation`; DB Invoice created Status=Pending (0), Code `CIN{yyyyMMddHHmmss}{NNNN}`; 2 InvoiceTicket rows linked.
3. Choose a payment method → 'XÁC NHẬN & THANH TOÁN'
   - Expected: Success page shows a ticket-code; DB Invoice.Status = Paid (1).

**PB-BOOK-02 — Concurrent lock: two clients cannot book the same seat (P0)**
1. Client A opens the seat grid and selects seat R5 (SignalR LockSeat fires)
   - Expected: Client B's grid shows R5 as 'locked' within ~1s.
2. Client B tries to select R5
   - Expected: Selection rejected (seat is locked by A).
3. Client A abandons the page without booking; wait 5 minutes
   - Expected: R5 auto-expires (IsSeatLocked 5-min window) and becomes available to B again.

**PB-BOOK-03 — Cancel guards (P0)**
1. User cancels their own Pending booking
   - Expected: DB Invoice.Status = Cancelled (2); seats freed.
2. User attempts to cancel a booking they do not own (different userId)
   - Expected: Rejected (returns false / 4xx), invoice unchanged.
3. User attempts to cancel an already-Paid invoice
   - Expected: Rejected, status stays Paid.

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-BOOK-01 | P0 | CLEAR | SC-BOOK-01 PASS |
| RI-BOOK-02 | P0 | CLEAR (verify PB-BOOK-02 step 3 manually) | SC-BOOK-02 PASS |
| RI-BOOK-03 | P0 | CLEAR (verify PB-BOOK-02 step 2 manually) | SC-BOOK-03 PASS |
| RI-BOOK-04 | P0 | CLEAR (verify PB-BOOK-01 step 2 manually) | SC-BOOK-04 PASS |
| RI-BOOK-05 | P0 | CLEAR (verify PB-BOOK-03 steps 2-3 manually) | SC-BOOK-05 PASS |
| RI-BOOK-06 | P0 | CLEAR | SC-BOOK-06 PASS |
| RI-BOOK-07 | P1 | CLEAR (verify PB-BOOK-01 step 1 manually) | SC-BOOK-09 PASS |

## §5 Summary
- **0 of 7 regression indicators triggered.**
- **Build/tests: OK** (Business build clean; 9/9 xUnit BookingServiceTests pass; FE build skipped as unchanged).
- **Static: 9/9 PASS.** Note: the prior run's SC-BOOK-05 FAIL is resolved — the YAML check was reconciled to assert the Pending gate (`InvoiceStatus.Pending` + `userId`) instead of a literal `Paid` check, which now matches the actual guard in `CancelBookingAsync`.
- **Action expected:** None blocking. All static + automated checks green; the three P0 playbook scenarios (PB-BOOK-01/02/03) still require a manual E2E run for full sign-off. No bug files written (no real defects).

# Flow Test Result — movie-admin — 2026-06-21 13:55
**Flow**: Admin movie & catalog management — CRUD, Admin gating, soft delete   **Layers**: business, service, frontend
**Changed scope detected**: none (clean working tree vs HEAD; only untracked QA-tests/ and .claude/ present) — all checks run anyway

## §1 Static checks (4/4 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-MADM-01 | P0 | PASS | `Authorize(Roles = _adminRole)` present on CreateMovie/UpdateMovie/DeleteMovie + all catalog/theater/room/showtime/invoice writes in CinemaController.cs (`_adminRole = "Admin"`, line 19) |
| SC-MADM-02 | P0 | PASS | MovieManager.DeleteAsync flips `movie.IsActive = false` then UpdateAsync+SaveChanges (lines 145-152); no `Store.Delete` present — soft delete confirmed |
| SC-MADM-03 | P1 | PASS | GetMoviesAsync signature `(PagingSearchDTO search)` → returns `DefaultSearchResults<MovieDTO>` (MovieManager.cs lines 17-36) |
| SC-MADM-04 | P1 | PASS | `adminGuard` exported from guards/index.ts; admin.guard.ts redirects non-Admin (`userTypeName === 'Admin'`) to `/` |

## §2 Build + test checks (3/3 PASS)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-MADM-BUILD | PASS | Build succeeded, 0 Warning(s), 0 Error(s) |
| BC-MADM-TEST | PASS | `Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6` (MovieServiceTests incl. soft-delete assertion) |
| BC-MADM-FE | PASS | CinemaAdmin dev bundle generated OK (130s); movies-management-component chunk emitted. Note: `skip_if_unchanged` qualified (no FE diff) but ran for full coverage. |

## §3 Playbook to run manually

**Prerequisites**
- Backend on http://localhost:5102, seed accounts created (admin@cinema.vn / Admin@123, user@cinema.vn / User@123)
- FE: `ng serve CinemaAdmin` (http://localhost:4201)

**PB-MADM-01 — Admin CRUD a movie (P0)**
1. Login as admin → /movies (admin) → create a movie → *expect:* Movie appears in the list
2. Edit then delete the movie → *expect:* List no longer shows it; DB row still present with IsActive = false (soft delete)

**PB-MADM-02 — Non-admin blocked (P0)**
1. Login as user@cinema.vn, navigate to an admin route → *expect:* adminGuard redirects away; no admin UI shown
2. Call CreateMovie API with a standard user's token → *expect:* 403 Forbidden

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-MADM-01 | P0 | NOT triggered (verify manually for runtime) | static SC-MADM-01 PASS; playbook PB-MADM-02 step 2 manual |
| RI-MADM-02 | P0 | NOT triggered (verify manually for runtime) | static SC-MADM-02 PASS; playbook PB-MADM-01 step 2 manual |
| RI-MADM-03 | P1 | NOT triggered | static SC-MADM-03 PASS |
| RI-MADM-04 | P0 | NOT triggered (verify manually for runtime) | static SC-MADM-04 PASS; playbook PB-MADM-02 step 1 manual |

## §5 Summary
- 0 of 4 regression indicators triggered
- Build/tests: OK (3/3 build checks pass, 6/6 unit tests pass)
- Action expected: None. All static invariants hold and all builds/tests green. Manual playbook (§3) remains for runtime verification of role gating and soft-delete DB state. No YAML check reconciliation needed — all spec patterns matched current code.

---

# Flow Test Result — auth-login (re-run after 401 fix) — 2026-06-21 14:08

Re-run triggered by fix: `IdentityController.cs` per-action try/catch blocks removed so `ExceptionMiddleware` maps exceptions to status codes. Login with bad credentials now returns **401** (was 500). **Verified live** before this run: bad password → 401, valid creds → 200 + JWT.

## §1 Static (5/5 PASS)

| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-AUTH-01 | P0 | PASS | `LoginAsync` throws `UnauthorizedAccessException("Invalid credentials.")` for unknown user and bad password (AuthManager.cs:29,32). |
| SC-AUTH-02 | P0 | PASS | `RegisterAsync` checks `GetByEmailAsync` and throws `InvalidOperationException("Email already in use.")` before creating user (AuthManager.cs:39-40). |
| SC-AUTH-03 | P0 | PASS | JwtTokenService embeds `new Claim(ClaimTypes.Role, user.UserType?.Name ?? "Customer")` (line 27). NOTE: file path stale — actual location `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs` (spec points at `Cinema.Data/JwtTokenService.cs`). |
| SC-AUTH-04 | P0 | PASS | All four admin endpoints gated `[Authorize(Roles = _adminRole)]` — GetUsers/CreateUser/UpdateUser/DeleteUser (IdentityController.cs:79,89,99,109). |
| SC-AUTH-05 | P1 | PASS | `auth.effects.ts` persists token via `localStorage.setItem('cinema_token', ...)` (line 43); logout removes same key (line 54). |

No static check asserts a try/catch or `StatusCode(500...)` in IdentityController, so the 401 fix did NOT invalidate any static invariant. The key auth invariants all still hold.

## §2 Build (2 PASS, 1 SKIP)

| Check | Status | Output |
|---|---|---|
| BC-AUTH-BUILD | PASS | `dotnet build Cinema.Business.csproj` → Build succeeded, 0 Warning(s), 0 Error(s), exit 0. |
| BC-AUTH-TEST | PASS | `dotnet test --filter ~AuthServiceTests` → Passed! Failed: 0, Passed: 7, Skipped: 0. exit 0. (Benign NU1603 restore warning re System.IdentityModel.Tokens.Jwt 8.3.3→8.4.0.) |
| BC-AUTH-FE | SKIP | `skip_if_unchanged: projects/CinemaLib/**` — `git diff` shows no CinemaLib changes this session; FE build not required. |

## §3 Playbook (manual — not auto-run)

**Prerequisites:** Backend on http://localhost:5102 with seed accounts (admin@cinema.vn / Admin@123, user@cinema.vn / User@123); FE: ng serve CinemaUser (4202) and/or CinemaAdmin (4201).

- **PB-AUTH-01 — Login success + role routing (P0)**
  1. POST /api/Identity/Login {admin@cinema.vn, Admin@123} → expect 200 + JWT, decoded role = Admin. *(login success path verified live: 200 + JWT.)*
  2. Login as admin in CinemaAdmin UI → expect localStorage `cinema_token` set; reaches /dashboard.
- **PB-AUTH-02 — Bad credentials + duplicate register (P0)**
  1. POST /api/Identity/Login with wrong password → expect **401 Unauthorized (NOT 200, NOT 500)**. *(VERIFIED LIVE — 401 after the try/catch-removal fix.)*
  2. POST /api/Identity/Register with existing email → expect 4xx duplicate-email message; no second user created. *(verify manually)*
- **PB-AUTH-03 — Role gate enforced (P0)**
  1. Call Admin-only GetUsers with a standard user's token → expect 403 Forbidden. *(verify manually)*

## §4 Indicators

| ID | Severity | Status | Source |
|---|---|---|---|
| RI-AUTH-01 | P0 | NOT TRIGGERED | SC-AUTH-01 PASS; PB-AUTH-02 step 1 verified live (401). |
| RI-AUTH-02 | P0 | NOT TRIGGERED | SC-AUTH-02 PASS; PB-AUTH-02 step 2 verify manually. |
| RI-AUTH-03 | P0 | NOT TRIGGERED | SC-AUTH-03 + SC-AUTH-04 both PASS; PB-AUTH-03 verify manually. |
| RI-AUTH-04 | P1 | NOT TRIGGERED | SC-AUTH-05 PASS. |

## §5 Summary
- **0 of 4** regression indicators triggered.
- **Build/tests: OK** — 2/2 executed build checks pass (1 skipped, FE unchanged); 7/7 AuthServiceTests pass.
- **The 401 fix is confirmed safe**: no static invariant relied on the removed try/catch; bad-credentials → 401 verified live; all auth invariants intact. This is NOT a defect.
- **Action expected: None.** No real bugs.
- **YAML reconciliation (stale checks, not regressions):**
  - SC-AUTH-03 `file` path is stale: should be `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs` (file moved into `Services/`). Check still passed after path correction.
  - Spec `last_updated: 2026-06-20` / `version: 1.0` predates the try/catch removal — consider bumping when reconciling the path above.

---

# Flow Test Result — auth-login (re-run: try/catch + HandleException) — 2026-06-21 14:12

Re-run triggered by fix: `IdentityController` now extends `ApiControllerBase` (new file `Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/ApiControllerBase.cs`). EACH action **keeps its own try/catch**, but the catch now delegates to `HandleException(e, nameof(Action))`, which maps exception type → status code: `KeyNotFoundException`→404, `UnauthorizedAccessException`→401, `InvalidOperationException`→400, else→500 (client errors log `Warning`, unexpected log `Fatal`). **This SUPERSEDES the 14:08 re-run above** (which described a "try/catch removed, middleware maps" approach — that is no longer the implementation; try/catch is retained and the base-class helper does the mapping). **Verified live before this run:** wrong password → `401 {"error":"Invalid credentials.","statusCode":401}`; valid creds → `200` + JWT; duplicate-email register → `400`.

## §1 Static checks (5/5 PASS)

| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-AUTH-01 | P0 | ✅ PASS | `LoginAsync` throws `UnauthorizedAccessException("Invalid credentials.")` for unknown user (AuthManager.cs:29) and wrong password (:32). Pattern `Unauthorized` present in the scoped method. |
| SC-AUTH-02 | P0 | ✅ PASS | `RegisterAsync` checks `GetByEmailAsync(request.Email)` and throws `InvalidOperationException("Email already in use.")` before creating the user (AuthManager.cs:39-40). Pattern `Email` present. |
| SC-AUTH-03 | P0 | ✅ PASS | `JwtTokenService.GenerateToken` emits `new Claim(ClaimTypes.Role, user.UserType?.Name ?? "Customer")` (JwtTokenService.cs:27). Pattern `Role` present. NOTE: actual path is `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs`; spec YAML lists `Cinema/3-Data/Cinema.Data/JwtTokenService.cs` (stale path — file located via glob). |
| SC-AUTH-04 | P0 | ✅ PASS | All four admin endpoints carry `[Authorize(Roles = _adminRole)]` — GetUsers (:114), CreateUser (:131), UpdateUser (:148), DeleteUser (:165). `_adminRole = "Admin"` (:18). |
| SC-AUTH-05 | P1 | ✅ PASS | `auth.effects.ts` persists JWT via `localStorage.setItem('cinema_token', ...)` (:43); logout removes the same key (:54). |

**On the HandleException refactor & stale-check nuance:** No static check in `auth-login.yaml` asserts a try/catch presence OR a literal `StatusCode(StatusCodes.Status500InternalServerError, e.Message)` inside `IdentityController`. The earlier blanket-500-in-catch was the *bug*; the new `HandleException` maps `UnauthorizedAccessException`→401, `InvalidOperationException`→400, `KeyNotFoundException`→404, else→500. Therefore the fix invalidated **no** static invariant and triggered **no** stale check. The new `ApiControllerBase.cs` still retains the 500 path only as the `default` branch (truly unexpected exceptions), which is correct. All core auth invariants hold: bad creds → Unauthorized, duplicate email rejected, JWT carries Role claim, admin endpoints gated, FE uses `cinema_token`.

## §2 Build + test checks (2 PASS, 1 SKIP)

| Check | Status | Output (excerpt) |
|---|---|---|
| BC-AUTH-BUILD | ✅ PASS (exit 0) | `dotnet build Cinema.Business.csproj` → **Build succeeded. 0 Warning(s), 0 Error(s).** (1.83s) |
| BC-AUTH-TEST | ✅ PASS (exit 0) | `dotnet test --filter ~AuthServiceTests` → **Passed! Failed: 0, Passed: 7, Skipped: 0, Total: 7** (270 ms). Only benign NU1603 restore warnings (System.IdentityModel.Tokens.Jwt 8.3.3→8.4.0). |
| BC-AUTH-FE | ⏭️ SKIP | `skip_if_unchanged: projects/CinemaLib/**` — `git status` shows no CinemaLib changes this session, so `npx ng build CinemaLib` was skipped (no nvm switch needed). |

## §3 Playbook (manual — not auto-run)

**Prerequisites:** Backend on http://localhost:5102 with seed accounts (admin@cinema.vn / Admin@123, user@cinema.vn / User@123); FE: `ng serve CinemaUser` (4202) and/or `ng serve CinemaAdmin` (4201). Use nvm v22.12.0 (system Node too old).

- **PB-AUTH-01 — Login success + role routing (P0)**
  1. POST `/api/Identity/Login` `{admin@cinema.vn, Admin@123}` → expect 200 + JWT, decoded role = Admin. *(success path VERIFIED LIVE: 200 + JWT.)*
  2. Login as admin in CinemaAdmin UI → expect localStorage `cinema_token` set; reaches `/dashboard`. *(verify manually)*
- **PB-AUTH-02 — Bad credentials + duplicate register (P0)**
  1. POST `/api/Identity/Login` with a wrong password → expect **401 Unauthorized (NOT 200, NOT 500)**. *(VERIFIED LIVE — 401 `{"error":"Invalid credentials.","statusCode":401}` via HandleException mapping.)*
  2. POST `/api/Identity/Register` with an existing email → expect **4xx** duplicate-email message; no second user created. *(VERIFIED LIVE — duplicate-email register → 400 via `InvalidOperationException`→400 mapping.)*
- **PB-AUTH-03 — Role gate enforced (P0)**
  1. Call Admin-only `GetUsers` with a standard user's token → expect 403 Forbidden. *(verify manually — enforced by `[Authorize(Roles = _adminRole)]`, framework returns 403 before the action runs.)*

## §4 Regression indicators

| ID | Severity | Status | Source |
|---|---|---|---|
| RI-AUTH-01 | P0 | ✅ NOT TRIGGERED | SC-AUTH-01 PASS; PB-AUTH-02 step 1 **verified live (401)**. |
| RI-AUTH-02 | P0 | ✅ NOT TRIGGERED | SC-AUTH-02 PASS; PB-AUTH-02 step 2 **verified live (400 duplicate email)**. |
| RI-AUTH-03 | P0 | ✅ NOT TRIGGERED | SC-AUTH-03 + SC-AUTH-04 both PASS; PB-AUTH-03 verify manually (403 via framework). |
| RI-AUTH-04 | P1 | ✅ NOT TRIGGERED | SC-AUTH-05 PASS. |

## §5 Summary
- **0 of 4 regression indicators triggered.** No P0 alert raised this run.
- **Static: 5/5 PASS.** **Build/tests: 2/2 executed PASS** (Business build clean; 7/7 AuthServiceTests), **1 SKIP** (BC-AUTH-FE — CinemaLib unchanged).
- **The try/catch + HandleException refactor is confirmed safe and is NOT a defect.** It fixes the prior blanket-500 bug: bad creds → 401, duplicate email → 400 (both verified live), valid login → 200. No static invariant relied on the old inline `StatusCode(500…)`, so nothing went stale because of it.
- **Stale checks to reconcile in `auth-login.yaml` (housekeeping, not regressions — no bug file written):**
  1. **SC-AUTH-03 `file` path** (and the matching `trigger_paths` entry) point at `Cinema/3-Data/Cinema.Data/JwtTokenService.cs`, but the file lives at `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs`. Update the path so the check resolves without a fallback search.
  2. Spec `last_updated: 2026-06-20` / `version: 1.0` predates both the file move and the HandleException refactor — bump when reconciling the path above. Optionally add `Controllers/ApiControllerBase.cs` to `trigger_paths` since it now owns the auth status-code mapping.
- **Action expected: None.** No real code defects.

---

# Flow Test Result — booking-seat-lock — 2026-07-05 18:36
**Flow**: Seat locking + booking → Invoice lifecycle (Business + Data + Service + FE)   **Layers**: business, data, service, frontend
**Changed scope detected**: BookingManager.cs, InvoiceManager.cs, PaymentController.cs, CinemaController.cs, Hubs/BookingHub.cs, AuthManager.cs, IBookingManager.cs, IInvoiceManager.cs, appsettings*.json, Program.cs, WebApiHost.csproj, Tests/Program.cs, Tests/appsettings.json (uncommitted security fixes)

## §1 Static checks (9/9 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-BOOK-01 | P0 | PASS | `_lockedSeats` is `static readonly ConcurrentDictionary` (line 17); all 3 patterns (static, ConcurrentDictionary, _lockedSeats) present. |
| SC-BOOK-02 | P0 | PASS | `IsSeatLocked` contains `TimeSpan.FromMinutes(5)` expiry window (line 214). |
| SC-BOOK-03 | P0 | PASS | `UnlockSeat` compares `info.ConnectionId == connectionId` before removal (line 205) — owner-scoped. |
| SC-BOOK-04 | P0 | PASS | `CreateBookingAsync` sets `Status = InvoiceStatus.Pending` (line 138) inside Begin/Commit/RollbackTransaction wrapper. |
| SC-BOOK-05 | P0 | PASS | `CancelBookingAsync` guards `invoice.UserId != userId` and `Status != InvoiceStatus.Pending` (lines 191-192). |
| SC-BOOK-06 | P0 | PASS | No hardcoded status magic numbers in BookingManager.cs / InvoiceManager.cs; forbidden regex has 0 matches. |
| SC-BOOK-07 | P1 | PASS | `InvoiceStatus` enum carries Pending, Paid, Cancelled, Failed. |
| SC-BOOK-08 | P1 | PASS | `SeatStatus` enum carries Available, Reserved, Occupied. |
| SC-BOOK-09 | P1 | PASS | seat-selection.component.html renders available / occupied / locked (+ selected) states. |

## §2 Build + test checks (2/2 PASS, 1 SKIP)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-BOOK-BUILD | PASS | `dotnet build Cinema.Business.csproj` → Build succeeded, 0 Warning(s), 0 Error(s), exit 0. |
| BC-BOOK-TEST | PASS | `dotnet test --filter ~BookingServiceTests` → Passed! Failed: 0, Passed: 9, Skipped: 0, exit 0. |
| BC-BOOK-FE | SKIP | skip_if_unchanged matched: no changes under `projects/CinemaUser/src/app/features/booking/**` or `projects/CinemaLib/**` (all changes are backend). |

## §3 Playbook to run manually

### Prerequisites
- Backend running: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
- Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (admin@cinema.vn / user@cinema.vn)
- DB seeded with ≥1 Movie, ≥1 Theater/Room with a seat map, ≥1 ShowTime
- FE: `ng serve CinemaUser` (http://localhost:4202)

### PB-BOOK-01 — Happy path: pick seats → create booking → confirm payment (P0)
1. Login as user@cinema.vn, open a movie's showtime → `/booking/seats?showTimeId=...&roomId=...` → **Expect**: Seat grid renders; available seats clickable, occupied not.
2. Select 2 available seats → click "XÁC NHẬN ĐẶT VÉ" → **Expect**: navigates to `/booking/confirmation`; DB Invoice created Status=Pending(0), Code `CIN{yyyyMMddHHmmss}{NNNN}`; 2 InvoiceTicket rows linked.
3. Choose a payment method → "XÁC NHẬN & THANH TOÁN" → **Expect**: success page shows ticket-code; DB Invoice.Status=Paid(1).
   - NOTE (security change): `ConfirmPaymentAsync(userId, invoiceId, paymentReference)` now enforces owner + Pending-only; verify a confirm request from a different user or on a non-Pending invoice returns false/4xx.

### PB-BOOK-02 — Concurrent lock: two clients cannot book the same seat (P0)
1. Client A selects seat R5 (SignalR LockSeat) → **Expect**: Client B's grid shows R5 as "locked" within ~1s.
2. Client B tries to select R5 → **Expect**: selection rejected (locked by A).
3. Client A abandons page; wait 5 minutes → **Expect**: R5 auto-expires (IsSeatLocked 5-min window) and becomes available to B.

### PB-BOOK-03 — Cancel guards (P0)
1. User cancels own Pending booking → **Expect**: DB Invoice.Status=Cancelled(2); seats freed.
2. User attempts to cancel a booking they do not own (different userId) → **Expect**: rejected (false/4xx), invoice unchanged.
3. User attempts to cancel an already-Paid invoice → **Expect**: rejected, status stays Paid.

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-BOOK-01 | P0 | NOT triggered | SC-BOOK-01 PASS |
| RI-BOOK-02 | P0 | NOT triggered (verify PB-BOOK-02 step 3 manually) | SC-BOOK-02 PASS |
| RI-BOOK-03 | P0 | NOT triggered (verify PB-BOOK-02 step 2 manually) | SC-BOOK-03 PASS |
| RI-BOOK-04 | P0 | NOT triggered (verify PB-BOOK-01 step 2 manually) | SC-BOOK-04 PASS |
| RI-BOOK-05 | P0 | NOT triggered (verify PB-BOOK-03 steps 2-3 manually) | SC-BOOK-05 PASS |
| RI-BOOK-06 | P0 | NOT triggered | SC-BOOK-06 PASS |
| RI-BOOK-07 | P1 | NOT triggered (verify PB-BOOK-01 step 1 manually) | SC-BOOK-09 PASS |

## §5 Summary
- 0 of 7 regression indicators triggered.
- Build/tests: OK (Cinema.Business builds clean; 9/9 BookingServiceTests pass). FE build skipped (no booking/lib FE changes).
- Security fixes validated against spec: `ConfirmPaymentAsync` new 3-arg owner+Pending-only signature and the per-`{showTimeId}:{roomId}` SemaphoreSlim booking gate are consistent with all static invariants; `_lockedSeats` dictionary + expiry + owner-scoped unlock unchanged.
- Spec hygiene note: `trigger_paths` lists `Cinema/1-Service/Cinema.Service.WebApiHost/BookingHub.cs` but the file actually lives at `Hubs/BookingHub.cs`. Stale path in the YAML, not a code defect — reconcile the spec.
- Action expected: none blocking. Safe to push after running the manual E2E playbook (§3).

---

# Flow Test Result — auth-login — 2026-07-05 18:37
**Flow**: Authentication — login, register, JWT issuance, role-based gating   **Layers**: business, service, frontend
**Changed scope detected**: AuthManager.cs (business), plus BookingManager.cs, InvoiceManager.cs, PaymentController.cs, CinemaController.cs, Hubs/BookingHub.cs, IBookingManager.cs, IInvoiceManager.cs, Program.cs, appsettings*.json, csproj, Tests/Program.cs, Tests/appsettings.json. No `projects/CinemaLib/**` (FE) changes.

## §1 Static checks (5/5 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-AUTH-01 | P0 | PASS | `LoginAsync` throws `UnauthorizedAccessException("Invalid credentials.")` for unknown user (l.29) and bad password (l.32). "Unauthorized" present. |
| SC-AUTH-02 | P0 | PASS | `RegisterAsync` checks `GetByEmailAsync` and throws `"Email already in use."` before creating (l.49-50). "Email" present. |
| SC-AUTH-03 | P0 | PASS | `JwtTokenService.GenerateToken` embeds `new Claim(ClaimTypes.Role, user.UserType?.Name ?? "Customer")` (l.27). Note: file lives at `Cinema.Data/Services/JwtTokenService.cs`, spec path `Cinema.Data/JwtTokenService.cs` is stale. |
| SC-AUTH-04 | P0 | PASS | `IdentityController` gates GetUsers/CreateUser/UpdateUser/DeleteUser with `[Authorize(Roles = _adminRole)]` (l.114,131,148,165). |
| SC-AUTH-05 | P1 | PASS | `auth.effects.ts` persists JWT via `localStorage.setItem('cinema_token', ...)` (l.43) and removes it on logout (l.54). |

## §2 Build + test checks (2/2 PASS, 1 SKIP)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-AUTH-BUILD | PASS | `dotnet build Cinema.Business.csproj` → Build succeeded, 0 Warning(s), 0 Error(s). Exit 0. |
| BC-AUTH-TEST | PASS | `dotnet test --filter FullyQualifiedName~AuthServiceTests` → Passed! Failed: 0, Passed: 7, Total: 7. Exit 0. (NU1603 restore warnings only, non-fatal.) |
| BC-AUTH-FE | SKIP | `skip_if_unchanged: projects/CinemaLib/**` — no CinemaLib changes in working tree, so CinemaLib build skipped. |

## §3 Playbook to run manually

**Prerequisites**
- Backend on http://localhost:5102, seed accounts created (`admin@cinema.vn / Admin@123`, `user@cinema.vn / User@123`).
- FE: `ng serve CinemaUser` (4202) and/or `ng serve CinemaAdmin` (4201).

**PB-AUTH-01 — Login success + role routing** (P0)
1. POST /api/Identity/Login `{email: admin@cinema.vn, password: Admin@123}` → expect 200 with a JWT; decoded token contains `role = Admin`.
2. Login as admin in CinemaAdmin UI → expect localStorage `cinema_token` set; reaches `/dashboard`.

**PB-AUTH-02 — Bad credentials + duplicate register** (P0)
1. POST /api/Identity/Login with a wrong password → expect 401 Unauthorized (NOT 200, NOT 500).
2. POST /api/Identity/Register with an already-existing email → expect 4xx with duplicate-email message; no second user created.

**PB-AUTH-03 — Role gate enforced** (P0)
1. Call an Admin-only endpoint (e.g. GetUsers) with a standard user's token → expect 403 Forbidden.

> Extra manual note for this run: the security change made login auto-rehash legacy HMAC hashes to PBKDF2 on success. When exercising PB-AUTH-01/02 against a DB seeded before the change, verify the first successful login of a legacy account still returns 200 and that a second login with the same password also succeeds (hash migrated in place).

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-AUTH-01 | P0 | NOT TRIGGERED | static SC-AUTH-01 PASS (login still throws Unauthorized on bad creds). PB-AUTH-02 step 1 = verify manually. |
| RI-AUTH-02 | P0 | NOT TRIGGERED | static SC-AUTH-02 PASS (duplicate email refused). PB-AUTH-02 step 2 = verify manually. |
| RI-AUTH-03 | P0 | NOT TRIGGERED | static SC-AUTH-03 + SC-AUTH-04 both PASS (Role claim present, admin gates intact). PB-AUTH-03 = verify manually. |
| RI-AUTH-04 | P1 | NOT TRIGGERED | static SC-AUTH-05 PASS (`cinema_token` key unchanged). |

## §5 Summary
- 0 of 4 regression indicators triggered.
- Build/tests: OK (Cinema.Business builds clean; 7/7 AuthServiceTests pass). FE build skipped (no CinemaLib changes).
- Security fixes validated against spec: PBKDF2-SHA256 hashing with legacy-HMAC verification fallback + auto-rehash on login does NOT alter any auth invariant — LoginAsync still throws `UnauthorizedAccessException` on bad creds, RegisterAsync still refuses duplicate email, Role claim and `[Authorize(Roles=_adminRole)]` gates unchanged, `cinema_token` FE key unchanged.
- Spec hygiene note: SC-AUTH-03 `file` path `Cinema/3-Data/Cinema.Data/JwtTokenService.cs` is stale — actual file is at `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs`. Check still resolved correctly; reconcile the YAML path. No code defect.
- Action expected: none blocking. Safe to push after running the manual E2E playbook (§3), paying attention to the legacy-hash migration note.

---

# Flow Test Result — booking-seat-lock — 2026-07-12 13:15
**Flow**: Seat locking + booking → Invoice lifecycle (Business + Data + Service + FE)   **Layers**: business, data, service, frontend
**Changed scope detected**: recent commits touched `BookingManager.cs` (CreateBookingAsync now emits per-ticket `QrCode`; `ComputePricingAsync` takes `roomId` and enforces per-theater discount scope), `InvoiceManager.cs`, and added `InvoiceStore.GetPaidTicketsForShowtimesAsync`. Branch `feature/multi-theater-catalog-pricing` also changed `CinemaLib` services + `booking-confirmation` component (vs master). Working tree at run time: only `show-times.component.html` uncommitted.

> ✅ **No P0 alert raised this run.** All 9 static checks PASS and all 3 build/test checks PASS. No regression indicators triggered by static/build analysis. Live-DB playbook items must be verified manually after the DB is reseeded (DB not reseeded and no app running this run).

## §1 Static checks (9/9 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-BOOK-01 | P0 | ✅ PASS | `static`, `ConcurrentDictionary`, `_lockedSeats` all present (BookingManager.cs line 18) — lock store stays process-local static |
| SC-BOOK-02 | P0 | ✅ PASS | `TimeSpan.FromMinutes(5)` present in `IsSeatLocked` (line 348) — 5-min lock expiry intact |
| SC-BOOK-03 | P0 | ✅ PASS | `UnlockSeat` compares `info.ConnectionId == connectionId` (line 339) — owner-scoped release |
| SC-BOOK-04 | P0 | ✅ PASS | `CreateBookingAsync` sets `Status = InvoiceStatus.Pending` (lines 155, 171); wrapped in `BeginTransactionAsync` (81) / `CommitTransactionAsync` (162) / `RollbackTransactionAsync` (177) — atomic |
| SC-BOOK-05 | P0 | ✅ PASS | `CancelBookingAsync` (lines 307–316): `InvoiceStatus.Pending` gate `if (invoice.Status != InvoiceStatus.Pending) return false;` (line 311) AND `userId` ownership check `invoice.UserId != userId` (line 310) |
| SC-BOOK-06 | P0 | ✅ PASS | No hardcoded magic-integer status assignment/comparison in BookingManager.cs or InvoiceManager.cs (forbidden regex: 0 matches) |
| SC-BOOK-07 | P1 | ✅ PASS | `InvoiceStatus` enum declares all 4: `Pending=0, Paid=1, Cancelled=2, Failed=3` |
| SC-BOOK-08 | P1 | ✅ PASS | `SeatStatus` enum declares all 3: `Available=0, Reserved=1, Occupied=2` |
| SC-BOOK-09 | P1 | ✅ PASS | `seat-selection.component.html` renders `available`, `occupied`, `locked` (+ `selected`, `vip`) seat states (lines 36–40) |

## §2 Build + test checks (3/3 PASS)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-BOOK-BUILD | ✅ PASS (exit 0) | Cinema.Business → built. `Build succeeded. 0 Warning(s) 0 Error(s)` (~3.9s) |
| BC-BOOK-TEST | ✅ PASS (exit 0) | BookingServiceTests: `Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9` (only NU1603 restore warnings, non-blocking) |
| BC-BOOK-FE | ✅ PASS (exit 0) | CinemaUser dev build complete (15.3s); `seat-selection-component` + `booking-confirmation-component` chunks emitted. Not skipped (booking FE + CinemaLib changed vs master). Node v22.12.0 via nvm. |

## §3 Playbook to run manually

**Prerequisites:**
- Backend running: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
- Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (admin@cinema.vn / user@cinema.vn)
- DB seeded with ≥1 Movie, ≥1 Theater/Room with a seat map, ≥1 ShowTime
- FE: `ng serve CinemaUser` (http://localhost:4202)

> ⚠️ **DB not reseeded and no app running this run** — all DB/live assertions below are "verify manually after reseed". The recent `CreateBookingAsync` change (per-ticket `QrCode`) means PB-BOOK-01 step 3 should also confirm each ticket carries a distinct QR token.

### PB-BOOK-01 — Happy path: pick seats → create booking → confirm payment (P0)
1. Login as user@cinema.vn, open a movie's showtime → `/booking/seats?showTimeId=...&roomId=...`
   - Expected: Seat grid renders; available seats clickable, occupied seats not
2. Select 2 available seats → click 'XÁC NHẬN ĐẶT VÉ'
   - Expected: Navigates to `/booking/confirmation`
   - Expected: DB Invoice created with Status = Pending (0), Code matching `CIN{yyyyMMddHHmmss}{NNNN}`
   - Expected: DB 2 InvoiceTicket rows linked to the invoice, each with a distinct `QrCode`
3. Choose a payment method → 'XÁC NHẬN & THANH TOÁN'
   - Expected: Success page shows a ticket-code / QR
   - Expected: DB Invoice.Status = Paid (1)

### PB-BOOK-02 — Concurrent lock: two clients cannot book the same seat (P0)
1. Client A opens the seat grid and selects seat R5 (SignalR LockSeat fires)
   - Expected: Client B's grid shows R5 as 'locked' within ~1s
2. Client B tries to select R5
   - Expected: Selection rejected (seat is locked by A)
3. Client A abandons the page without booking; wait 5 minutes
   - Expected: R5 auto-expires (IsSeatLocked 5-min window) and becomes available to B again

### PB-BOOK-03 — Cancel guards (P0)
1. User cancels their own Pending booking
   - Expected: DB Invoice.Status = Cancelled (2); seats freed
2. User attempts to cancel a booking they do not own (different userId)
   - Expected: Rejected (returns false / 4xx), invoice unchanged
3. User attempts to cancel an already-Paid invoice
   - Expected: Rejected, status stays Paid

## §4 Regression indicators
| Indicator | Severity | Status | Basis |
|---|---|---|---|
| RI-BOOK-01 | P0 | ✅ Not triggered | SC-BOOK-01 PASS |
| RI-BOOK-02 | P0 | ✅ Not triggered (static) | SC-BOOK-02 PASS; PB-BOOK-02 step 3 → verify manually after reseed |
| RI-BOOK-03 | P0 | ✅ Not triggered (static) | SC-BOOK-03 PASS; PB-BOOK-02 step 2 → verify manually after reseed |
| RI-BOOK-04 | P0 | ✅ Not triggered (static) | SC-BOOK-04 PASS; PB-BOOK-01 step 2 → verify manually after reseed |
| RI-BOOK-05 | P0 | ✅ Not triggered (static) | SC-BOOK-05 PASS; PB-BOOK-03 steps 2–3 → verify manually after reseed |
| RI-BOOK-06 | P0 | ✅ Not triggered | SC-BOOK-06 PASS |
| RI-BOOK-07 | P1 | ✅ Not triggered (static) | SC-BOOK-09 PASS; PB-BOOK-01 step 1 → verify manually after reseed |

## §5 Summary
- **Static**: 9 PASS / 0 FAIL / 0 SKIP.
- **Build/test**: 3 PASS / 0 FAIL / 0 SKIP (BC-BOOK-FE not skipped — booking FE + CinemaLib changed vs master).
- **Indicators triggered**: none. Live-DB indicators (RI-BOOK-02/03/04/05/07) have their playbook legs deferred to manual verification after the DB is reseeded and the app is running.
- **Bugs exported**: none — no real code defects and no stale-check/contract mismatches to reconcile. The recent `BookingManager` / `InvoiceStore` changes are consistent with the YAML contract; no YAML reconciliation needed.

---

# Flow Test Result — movie-admin — 2026-07-12
**Flow**: Admin movie & catalog management — CRUD, Admin gating, soft delete (Business + Service + FE)   **Layers**: business, service, frontend
**Changed scope detected**: feature branch `feature/multi-theater-catalog-pricing` — catalog admin heavily reworked (seat types, food & drinks now per-theater; rooms gained RoomTypeId; new RoomType/TimeSlot/TicketPrice entities; standalone /rooms, /seat-types, /food-and-drinks admin pages moved into theater-detail tabs). Working-tree edit this run: `CinemaAdmin/.../catalog/show-times/show-times.component.html`.

> ✅ **No P0 alert raised this run.** All 4 static checks PASS and all 3 build/test checks PASS. No regression indicators triggered. The spec's static checks reference only stable surfaces (controller `_adminRole` gate, `MovieManager.DeleteAsync`/`GetMoviesAsync`, guards folder) — none point at the removed standalone catalog pages, so no stale-check reconciliation is required for this flow.

## §1 Static checks (4/4 PASS)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-MADM-01 | P0 | ✅ PASS | `Authorize(Roles = _adminRole)` present on every write endpoint in `CinemaController.cs` — CreateMovie (L218), UpdateMovie (L236), DeleteMovie (L254) and all catalog Create/Update/Delete actions. Reads stay public. |
| SC-MADM-02 | P0 | ✅ PASS | `MovieManager.DeleteAsync` (L146–153) sets `movie.IsActive = false` then `MovieStore.UpdateAsync` — soft delete. `must_not_contain: Store.Delete` → 0 matches (no hard delete). |
| SC-MADM-03 | P1 | ✅ PASS | `GetMoviesAsync` (L17–36) takes `PagingSearchDTO` and returns `DefaultSearchResults<MovieDTO>` — standard paging contract intact. |
| SC-MADM-04 | P1 | ✅ PASS | `adminGuard` defined/exported in `CinemaLib/src/lib/guards/admin.guard.ts` (L7). |

## §2 Build + test checks (3/3 PASS)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-MADM-BUILD | ✅ PASS (exit 0) | `Cinema.Business` → built. `Build succeeded. 0 Warning(s) 0 Error(s)` (3.53s) |
| BC-MADM-TEST | ✅ PASS (exit 0) | MovieServiceTests: `Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6` (incl. soft-delete assertion). Only NU1603 restore warnings, non-blocking. |
| BC-MADM-FE | ✅ PASS (exit 0) | CinemaAdmin build complete (15.5s). movies-management + theater-detail chunks emitted. Not skipped (CinemaAdmin file changed). Node v22.12.0. |

## §3 Playbook to run manually

> DB has NOT been reseeded and no app is running — all live-DB items below are **"verify manually after reseed"**.

**Prerequisites:**
- Backend running: `dotnet run --project Cinema/1-Service/Cinema.Service.WebApiHost` (http://localhost:5102)
- Seed accounts: `dotnet run --project Cinema/2-Business/Cinema.Business.Tests` (admin@cinema.vn / user@cinema.vn)
- FE: `ng serve CinemaAdmin` (http://localhost:4201)

### PB-MADM-01 — Admin CRUD a movie (P0)
1. Login as admin → `/movies` (admin) → create a movie
   - Expected: Movie appears in the list
2. Edit then delete the movie
   - Expected: List no longer shows it
   - Expected: DB — row still present with `IsActive = false` (soft delete)  *(verify manually after reseed)*

### PB-MADM-02 — Non-admin blocked (P0)
1. Login as user@cinema.vn, navigate to an admin route
   - Expected: `adminGuard` redirects away; no admin UI shown
2. Call CreateMovie API with a standard user's token
   - Expected: 403 Forbidden  *(verify manually after reseed)*

## §4 Regression indicators
| Indicator | Severity | Detection | Status |
|---|---|---|---|
| RI-MADM-01 | P0 | static SC-MADM-01 OR playbook PB-MADM-02 step 2 | ✅ Not triggered (SC-MADM-01 PASS; step 2 → verify manually) |
| RI-MADM-02 | P0 | static SC-MADM-02 OR playbook PB-MADM-01 step 2 | ✅ Not triggered (SC-MADM-02 PASS; step 2 → verify manually) |
| RI-MADM-03 | P1 | static SC-MADM-03 | ✅ Not triggered (SC-MADM-03 PASS) |
| RI-MADM-04 | P0 | static SC-MADM-04 OR playbook PB-MADM-02 step 1 | ✅ Not triggered (SC-MADM-04 PASS; step 1 → verify manually) |

## §5 Summary
- **Verdict: PASS.** 4/4 static checks PASS, 3/3 build/test checks PASS, 0 of 4 regression indicators triggered. No P0 alert.
- No real code defects found → no BUG files written.
- Soft-delete invariant (RI-MADM-02) holds: `MovieManager.DeleteAsync` flips `IsActive` and never hard-deletes. All 6 MovieServiceTests pass.
- Admin gating (RI-MADM-01/04) holds: controller writes carry `[Authorize(Roles = _adminRole)]`; FE `adminGuard` present.
- **Stale-check note:** despite this session's heavy catalog rework (per-theater seat types & food/drinks, new RoomType/TimeSlot/TicketPrice, removed standalone /rooms · /seat-types · /food-and-drinks pages), none of this flow's static checks reference the removed paths, so no YAML reconciliation is needed for movie-admin. The controller correctly exposes the new catalog managers (RoomType, TimeSlot, TicketPrice) with the same Admin-gated write pattern.
- Live-DB playbook (§3) not executed (no reseed / no running app) — run manually after reseed.

---

# 🔴 P1 — auth-login — 2026-07-12 13:15
**Flow**: Authentication — login, register, JWT issuance, role-based gating   **Layers**: business, service, frontend
**Changed scope detected**: this branch (`feature/multi-theater-catalog-pricing`) touches CinemaLib services (`cinema-http.service.ts`, `payment-http.service.ts`), CinemaAdmin/CinemaUser components, reports feature, and several backend Cinema/Payment/Invoice/Movie files. No direct edits to `AuthManager.cs`, `IdentityController.cs`, `JwtTokenService.cs`, or the auth store in the working tree — but the FE auth token persistence was refactored out of `auth.effects.ts` into a `TokenStorage` helper (`store/auth/token-storage.ts`) at some point since the 2026-07-05 run.

> ⚠️ **P1 static miss (stale check, not a code defect).** SC-AUTH-05 asserts the literal `cinema_token` inside `auth.effects.ts`. That file no longer contains the key — token persistence was moved to `store/auth/token-storage.ts` (`const TOKEN_KEY = 'cinema_token'`) and `auth.effects.ts` now calls `TokenStorage.save(...)` / `TokenStorage.clear()`. The `cinema_token` key still exists and is still the one read by the interceptor and Playwright helper, so this is a YAML path/pattern that needs reconciling, **not** a regression. No BUG file written. All P0 auth invariants PASS.

## §1 Static checks (4/5 PASS, 1 FAIL)
| Check | Severity | Status | Detail |
|---|---|---|---|
| SC-AUTH-01 | P0 | PASS | `LoginAsync` throws `UnauthorizedAccessException("Invalid credentials.")` for unknown user (l.44) and bad password (l.57), plus lockout/2FA/email-unconfirmed paths. "Unauthorized" present. |
| SC-AUTH-02 | P0 | PASS | `RegisterAsync` checks `GetByEmailAsync(request.Email) != null` and throws `"Email already in use."` before creating the user (l.108-109). "Email" present. |
| SC-AUTH-03 | P0 | PASS | `JwtTokenService.GenerateToken` embeds `new Claim(ClaimTypes.Role, user.UserType?.Name ?? "Customer")` (l.27). Note: spec `file` path `Cinema.Data/JwtTokenService.cs` is stale — actual file is `Cinema.Data/Services/JwtTokenService.cs` (matches `trigger_paths` l.26). Check resolved via the real path. |
| SC-AUTH-04 | P0 | PASS | `IdentityController` gates `GetUsers`/`CreateUser`/`UpdateUser`/`DeleteUser` with `[Authorize(Roles = _adminRole)]` (l.243,260,277,294); `_adminRole = "Admin"` (l.18). |
| SC-AUTH-05 | P1 | **FAIL** | `auth.effects.ts` no longer contains the literal `cinema_token`; persistence delegates to `TokenStorage.save/clear` (l.50,60). Key now lives in `store/auth/token-storage.ts` (`const TOKEN_KEY = 'cinema_token'`, l.5). **Stale check** — key still present and honored by the interceptor; not a code defect. Reconcile the YAML `file`/`pattern`. |

## §2 Build + test checks (3/3 PASS)
| Check | Status | Output (excerpt if fail) |
|---|---|---|
| BC-AUTH-BUILD | PASS | `dotnet build Cinema.Business.csproj -v minimal` → Build succeeded, 0 Warning(s), 0 Error(s). Exit 0. |
| BC-AUTH-TEST | PASS | `dotnet test --filter FullyQualifiedName~AuthServiceTests` → Passed! Failed: 0, Passed: 13, Skipped: 0, Total: 13. Exit 0. (NU1603 restore warnings only, non-fatal.) |
| BC-AUTH-FE | PASS | `npx ng build CinemaLib` (node v22.12.0) → "Built CinemaLib", dist written. Exit 0. Not skipped — `projects/CinemaLib/**` changed on this branch. (PowerShell wraps ng's stderr progress lines as NativeCommandError noise; build genuinely succeeded.) |

## §3 Playbook to run manually

**Prerequisites**
- Backend on http://localhost:5102, seed accounts created (`admin@cinema.vn / Admin@123`, `user@cinema.vn / User@123`). DB has NOT been reseeded and no app is running this session → all live-DB legs are "verify manually after reseed".
- FE: `ng serve CinemaUser` (4202) and/or `ng serve CinemaAdmin` (4201).

**PB-AUTH-01 — Login success + role routing** (P0)
1. POST /api/Identity/Login `{email: admin@cinema.vn, password: Admin@123}` → expect 200 with a JWT; decoded token contains `role = Admin`. *(verify manually after reseed)*
2. Login as admin in CinemaAdmin UI → expect localStorage `cinema_token` set; reaches `/dashboard`. *(verify manually after reseed)*

**PB-AUTH-02 — Bad credentials + duplicate register** (P0)
1. POST /api/Identity/Login with a wrong password → expect 401 Unauthorized (NOT 200, NOT 500). *(verify manually after reseed)*
2. POST /api/Identity/Register with an already-existing email → expect 4xx with duplicate-email message; no second user created. *(verify manually after reseed)*

**PB-AUTH-03 — Role gate enforced** (P0)
1. Call an Admin-only endpoint (e.g. GetUsers) with a standard user's token → expect 403 Forbidden. *(verify manually after reseed)*

> Manual note: `TokenStorage` now splits persistence between `localStorage` (remember-me) and `sessionStorage`, falling back across both on read. When verifying PB-AUTH-01 step 2, confirm the token lands under `cinema_token` in the expected store for the remember-me choice, and that logout clears both stores.

## §4 Regression indicators
| ID | Severity | Status | Source |
|---|---|---|---|
| RI-AUTH-01 | P0 | NOT TRIGGERED | static SC-AUTH-01 PASS (login still throws Unauthorized on bad creds). PB-AUTH-02 step 1 = verify manually after reseed. |
| RI-AUTH-02 | P0 | NOT TRIGGERED | static SC-AUTH-02 PASS (duplicate email refused). PB-AUTH-02 step 2 = verify manually after reseed. |
| RI-AUTH-03 | P0 | NOT TRIGGERED | static SC-AUTH-03 + SC-AUTH-04 both PASS (Role claim present, admin gates intact). PB-AUTH-03 = verify manually after reseed. |
| RI-AUTH-04 | P1 | ⚠️ TRIGGERED (stale check) | static SC-AUTH-05 FAIL. But `cinema_token` was only relocated to `token-storage.ts`, not renamed or removed; the interceptor/Playwright helper still read the same key. No user-facing logout regression. Reconcile the YAML — no BUG file. |

## §5 Summary
- **Static**: 4 PASS / 1 FAIL / 0 SKIP.
- **Build/test**: 3 PASS / 0 FAIL / 0 SKIP (BC-AUTH-FE ran — CinemaLib changed on this branch; 13/13 AuthServiceTests pass; Cinema.Business builds clean).
- **Indicators triggered**: 1 (RI-AUTH-04, P1) — and it is a **stale check**, not a code defect. The FE token key `cinema_token` still exists (moved from `auth.effects.ts` into `store/auth/token-storage.ts`) and is still honored end-to-end. All four P0 indicators NOT triggered.
- **Bugs exported**: none — no real code defects.
- **YAML reconciliation needed (housekeeping, no code change):**
  1. **SC-AUTH-05**: update `file` to `projects/CinemaLib/src/lib/store/auth/token-storage.ts` (or broaden to the `store/auth/` folder) since the `cinema_token` literal was refactored out of `auth.effects.ts`.
  2. **SC-AUTH-03**: update `file` to `Cinema/3-Data/Cinema.Data/Services/JwtTokenService.cs` (add the missing `Services/` segment; `trigger_paths` already has the correct path).
- Live-DB playbook (§3) not executed (no reseed / no running app) — run manually after reseed.

---
