# User Stories

> Purpose: Capabilities of `docs/01-business/requirements.md` restated as user stories with testable acceptance criteria, grouped by role.
> Sources: `docs/03-technical/api-spec.md`, `docs/01-business/roles-permissions.md`, `docs/01-business/requirements.md`, `docs/01-business/business-rules.md`, and the code and tests cited per criterion
> Last reviewed: 2026-11-14

Format: `As a <role>, I want to <capability>, so that <value>.` Each story maps to a capability that already exists in the current system (verified against code) — this is a description of current behavior, not a backlog of unbuilt features.

## How to read the acceptance criteria

- Stories are numbered `US-01` to `US-29` (`US-24`, gift cards, was removed 2026-11-14 and its number is not reused); each criterion is `AC-<story>.<n>` (for example `AC-07.3`), so a test, a defect or a review comment can name one.
- Each criterion is Given / When / Then and ends with its **Source**: a test (`Class.Method`, under `cinemabe/Cinema/2-Business/Cinema.Business.Tests` unless the path says otherwise) or a `file:line` in the code. Each criterion is tagged **success**, **error**, **boundary** or **gap**.
- **gap** marks a criterion where the code or the tests do not deliver what the story implies: a flag that is stored but never read, or an endpoint with no test. It is a statement about the code as of this review (2026-11-14), not a requirement to build.
- Where a criterion says "no test covers" a case, the behavior is read from the code, so it is not protected against regression.
- Thresholds quoted here (5 minutes, 15 minutes, 5 failures and so on) are also listed with their file:line in `requirements.md` section 5.

## Customer

### US-01 Register or sign in

- As a Customer, I want to register with email/phone/password or sign in with Google/Facebook, so that I can book tickets without re-entering my details each time.

- **AC-01.1** (success) Given an email and phone not used before and a password of 8 to 100 characters that matches its confirmation, when I register, then an account is created and I receive a token. The email is unconfirmed, and later password sign-ins are refused until I confirm it. Source: `AuthServiceTests.Register_NewEmail_CreatesUserAndReturnsToken`; `RegisterRequest.cs:21`; `AuthManager.cs:74`.
- **AC-01.2** (error) Given an email that already exists, when I register, then it fails with "Email already in use." Source: `AuthServiceTests.Register_DuplicateEmail_ThrowsInvalidOperation`; `AuthManager.cs:133`.
- **AC-01.3** (error) Given a phone number that already exists, when I register, then it fails with "Phone already in use." Source: `AuthManager.cs:138`.
- **AC-01.4** (boundary) Given a password shorter than 8 characters, or a confirmation that differs, when I register, then validation rejects the request. Source: `RegisterRequest.cs:21`, `RegisterRequest.cs:24-25`.
- **AC-01.5** (error) Given an unknown account or a wrong password on an account that is not locked, when I sign in, then the answer is the same 401 "Invalid credentials." and a wrong password increments the failure counter. A locked account is distinguishable: `LoginAsync` checks the lockout before the password, so an existing locked account answers "Account locked due to too many failed attempts. Try again in N minute(s)." even for a correct password. Source: `AuthServiceTests.Login_InvalidPassword_ThrowsUnauthorized`, `Login_UserNotFound_ThrowsUnauthorized`; `AuthManager.cs:51`, `AuthManager.cs:55-60`, `AuthManager.cs:65`.
- **AC-01.6** (boundary) Given 5 consecutive wrong passwords, when I try a 6th time even with the right password, then the account is locked for 15 minutes with "Account locked due to too many failed attempts. Try again in N minute(s)." Source: `AuthServiceTests.Login_FiveWrongPasswords_LocksTheAccount_ThenTheCorrectPasswordIsRefused`; `AuthManager.cs:42-43`, `AuthManager.cs:58`.
- **AC-01.7** (error) Given an Inactive or Banned account, when I sign in by password, Google or Facebook, then it fails with "This account is disabled." Source: `AuthServiceTests.Login_DisabledAccount_ThrowsUnauthorized_NoToken`, `GoogleLogin_BannedAccount_ThrowsUnauthorized`; `AuthManager.cs:125`.
- **AC-01.8** (success) Given a valid Google token with a verified email and no existing user, when I sign in with Google, then a confirmed Customer account is created and I receive a token. Source: `AuthServiceTests.GoogleLogin_NewUser_CreatesConfirmedUserAndReturnsToken`.
- **AC-01.9** (error) Given an invalid Google token, or one whose email is not verified, when I sign in with Google, then it fails with 401. Source: `AuthServiceTests.GoogleLogin_InvalidToken_ThrowsUnauthorized`; `AuthManager.cs:312`, `AuthManager.cs:317`.
- **AC-01.10** (success) Given a registered email, when I request a password reset and then submit a valid token with a new password, then the password changes. An unknown email returns silently, so it does not reveal which emails exist. Source: `AuthManager.cs:460-466`, `AuthManager.cs:492`.
- **AC-01.11** (error) Given more than 10 requests per minute from one client to the sign-in endpoints, or more than 3 per 15 minutes to the email-sending endpoints, when I send another, then it is answered 429 "Too many requests. Please wait and try again." Source: `RateLimitPolicies.cs:46-47`, `RateLimitPolicies.cs:55-56`; `RateLimitPolicies.cs:38-39`.

### US-02 Two-factor authentication

- As a Customer, I want to enable 2FA on my account, so that my login is protected even if my password leaks (note: this protection does not apply when I sign in via Google/Facebook).

- **AC-02.1** (success) Given 2FA is enabled and I enter the correct password, when I sign in, then no token is issued yet; a 6-digit code valid for 5 minutes is emailed (and sent by SMS if I have a phone number) and the response says a second factor is required. Source: `AuthServiceTests.Login_TwoFactorEnabled_ReturnsRequiresTwoFactorWithoutToken`; `AuthManager.cs:221`.
- **AC-02.2** (success) Given I submit the correct, unexpired code, when I verify, then I receive a token and the code is consumed (single use). Source: `AuthServiceTests.VerifyTwoFactor_CorrectCode_ResetsFailureCounter`; `AuthManager.cs:280-283`.
- **AC-02.3** (error) Given a wrong, reused, expired or never-issued code, when I verify, then every case gives the same 401 "Invalid or expired code." Source: `AuthServiceTests.VerifyTwoFactor_WrongCode_ThrowsUnauthorized`; `AuthManager.cs:243`, `AuthManager.cs:275`.
- **AC-02.4** (boundary) Given wrong passwords and wrong 2FA codes share one failure counter (5 combined failures), when the 5th failure is a wrong code, then the account is locked for 15 minutes and the pending code is burned; the 6th attempt gets the "Account locked ..." message and even the correct code is refused during the lockout. Source: `AuthServiceTests.VerifyTwoFactor_FiveWrongCodes_LocksAccountAndBurnsTheCode`; `AuthManager.cs:247-251`, `AuthManager.cs:263-271`.
- **AC-02.5** (boundary) Given I sign in with Google or Facebook, when 2FA is enabled on the account, then no second factor is asked. Source: `AuthManager.cs:307-402` builds the response without checking `TwoFactorEnabled` (see `business-rules.md` section 6, "Social login bypasses 2FA").

### US-03 Browse movies

- As a Customer, I want to browse now-showing and coming-soon movies with ratings (including OMDb critic scores), so that I can decide what to watch.

- **AC-03.1** (success) Given active movies, when I list now-showing or coming-soon, then I get a paged list mapped from the catalog. Source: `MovieServiceTests.GetMoviesAsync_ReturnsMappedResults`, `GetNowShowingAsync_ReturnsMappedList`.
- **AC-03.2** (success) Given a movie that has an IMDb id, when I open its detail, then external ratings are looked up and shown. Source: `MovieServiceTests.GetDetailAsync_ExternalRatingsFound_SetsExternalRatings`, `GetDetailAsync_PassesMovieImdbIdToProvider`.
- **AC-03.3** (error) Given OMDb has no match or does not answer within 3 seconds, when I open the detail, then the page still renders, without external ratings. Source: `MovieServiceTests.GetDetailAsync_ExternalRatingsNotFound_LeavesExternalRatingsNull`; `DependencyInjection.cs:25`.
- **AC-03.4** (boundary) Given a list request, when it is built, then the external ratings provider is never called, so a list never waits on OMDb; and a page size above 100 is capped to 100. Source: `MovieServiceTests.GetMoviesAsync_NeverCallsExternalRatingsProvider`, `SearchShowTimesAsync_PageSizeCappedAt100`; `MovieManager.cs:141`.
- **AC-03.5** (error) Given an unknown movie id or slug, when I open its detail, then it fails as not found. Source: `MovieServiceTests.GetDetailAsync_NotFound_Throws`, `GetDetailBySlugAsync_UnknownSlug_Throws`.

### US-04 Find theaters and showtimes

- As a Customer, I want to see which theaters are screening a given movie and when, so that I can pick a convenient showtime.

- **AC-04.1** (success) Given a movie with upcoming showtimes, when I open its detail, then I see schedule rows for a window of 4 days from now, with booked seats subtracted from the free count. Source: `MovieServiceTests.GetDetailAsync_RequestsFourDayWindowFromNow`, `GetDetailAsync_MapsScheduleRowsToSummaries_SubtractingBookedSeats`.
- **AC-04.2** (boundary) Given I search showtimes for today or a past date, when the search runs, then the start is clamped to now, so a past showtime is never offered. Source: `MovieServiceTests.SearchShowTimesAsync_Today_ClampsFromToNow`, `SearchShowTimesAsync_PastDate_ClampsFromToNow`.
- **AC-04.3** (error) Given a search whose end time is before its start time, when it runs, then it fails. Source: `MovieServiceTests.SearchShowTimesAsync_InvertedTimeRange_Throws`.
- **AC-04.4** (success) Given I filter movies by theater, when I search, then only movies with showtimes from now onwards at that theater are returned. Source: `MovieServiceTests.GetMoviesAsync_TheaterOnly_ShowFromNowAndNoUpperBound`.

### US-05 Live seat map

- As a Customer, I want to see which seats are available, locked by someone else, or already booked in real time while I'm choosing seats, so that I don't pick a seat someone else is about to take.

- **AC-05.1** (success) Given a free seat, when I lock it, then the lock is recorded and every viewer in that room is told the seat is locked. Source: `BookingHubTests.LockSeat_WhenFree_LocksAndBroadcastsToTheRoom`.
- **AC-05.2** (error) Given another connection holds the seat, when I lock it, then I am refused and nobody else is notified. Source: `BookingHubTests.LockSeat_WhenAnotherConnectionHoldsIt_IsRefusedToTheCallerOnly`.
- **AC-05.3** (boundary) Given I hold a lock, when 5 minutes pass, then another viewer can take the seat; re-locking my own seat does not extend the hold. Source: `BookingServiceTests.LockSeat_AfterExpiry_TakesANewLockForTheNextConnection`, `LockSeat_ReLockBySameConnection_DoesNotRenewTheHoldWindow`; `BookingManager.cs:1883`.
- **AC-05.4** (error) Given someone else holds a lock, when I try to unlock it, then it stays locked and no "unlocked" message is broadcast. Source: `BookingHubTests.UnlockSeat_ByANonOwner_BroadcastsNothing`.
- **AC-05.5** (success) Given I disconnect while holding seats, when the connection drops, then every seat I held is released and announced. Source: `BookingHubTests.Disconnect_BroadcastsEachHeldSeatToItsOwnRoom`.
- **AC-05.6** (success) Given a seat is on a Pending or Paid invoice, when the seat map loads, then it shows as occupied. Source: `BookingServiceTests.GetSeatsAsync_ReturnsOccupied_WhenSeatIsBooked`.
- **AC-05.7** (boundary) Given the lock is only advisory and lives in one server process, when two requests race for the same seat, then the unique index on `InvoiceTicket` makes the loser fail with "One or more selected seats were just booked by someone else." Source: `BookingServiceTests.CreateBookingAsync_SeatTakenByAnotherServerAtInsert_IsRolledBackAsJustBooked`; `cinemabe/sql/create_db.sql:717`.

### US-06 Discounts and loyalty points

- As a Customer, I want to apply a discount code or my loyalty points at checkout, so that I pay less for tickets I'd book anyway.

- **AC-06.1** (success) Given a usable promotion code, when I validate it at checkout, then I see what it takes off, capped at the program's maximum discount. Source: `BookingServiceTests.ValidateDiscountCodeAsync_UsableCode_ReportsWhatItTakesOff`, `ValidateDiscountCodeAsync_CapsTheAmountAtMaxDiscount`.
- **AC-06.2** (error) Given an unknown, expired or exhausted code, when I validate it, then the answer is "invalid" without an error; booking with an unusable code fails. Source: `BookingServiceTests.ValidateDiscountCodeAsync_UnknownCode_IsInvalidWithoutThrowing`, `ValidateDiscountCodeAsync_ExpiredOrExhaustedCode_IsInvalid`.
- **AC-06.3** (success) Given I am a tier member and no program applies, when I book, then I pay the list price: tiers give no percentage discount. Source: `BookingServiceTests.CreateBookingAsync_TierMemberWithoutProgram_PaysListPrice_NoTierPercentage`.
- **AC-06.4** (success) Given I am a tier member and a program applies, when I book, then I pay the program price alone, with nothing stacked on top. Source: `BookingServiceTests.CreateBookingAsync_TierMemberWithProgram_PaysProgramPriceAlone`.
- **AC-06.5** (boundary) Given my points were spent by another booking at the same moment, when I book, then it fails with "Not enough loyalty points." and rolls back. Source: `BookingServiceTests.CreateBookingAsync_PointsAlreadySpentByAConcurrentBooking_IsRejectedAndRolledBack`.
- **AC-06.6** (success) Given the theater has no custom loyalty rate, when I earn or redeem, then 1,000 VND earns 1 point and 1 point is worth 10 VND (100 points = 1,000 VND); a theater override replaces both rates. Source: `LoyaltyRates.cs:13-14`; `BookingServiceTests.CreateBookingAsync_RedeemsPoints_UsesTheaterLoyaltyOverrideRate`, `ConfirmPaymentAsync_CreditsPoints_FallsBackToGlobalDefaultWhenNoOverride`.
- **AC-06.7** (boundary) Given an auto-apply promotion that reached its usage cap or is scoped to another movie, when I book, then it is not applied. Source: `BookingServiceTests.CreateBookingAsync_AutoApplyProgramAtItsUsageCap_IsNotApplied`, `CreateBookingAsync_AutoApplyProgramScopedToAnotherMovie_IsNotApplied`.

### US-07 Pay through the gateway

- As a Customer, I want to pay through a gateway (VNPay) and get my booking confirmed automatically once payment clears, so that I don't have to manually confirm payment myself.

- **AC-07.1** (success) Given my own Pending invoice, when I start payment, then the invoice records the gateway that owns it and I get a redirect URL. Source: `PaymentCallbackTests.InitiatePaymentAsync_RecordsTheResolvedGatewayAsTheInvoiceOwner`.
- **AC-07.2** (success) Given a validly signed VNPay IPN for a Pending invoice with the right amount, when it arrives, then the invoice becomes Paid once, one payment row is written, loyalty points accrue, and the IPN answers `{"RspCode":"00","Message":"Confirm Success"}`. Source: `PaymentCallbackTests.HandlePaymentCallbackAsync_ValidPayment_ConfirmsInvoiceAndFinalizes`; `business-rules.md` section 3.
- **AC-07.3** (error) Given a tampered signature, when the IPN arrives, then nothing changes and the answer is HTTP 200 with `RspCode` 97. Source: `PaymentCallbackTests.HandlePaymentCallbackAsync_TamperedSignature_ReturnsInvalidSignature_NoInvoiceLookup`; `docs/05-process/testing.md` rule BR-PAY-02.
- **AC-07.4** (error) Given an amount that differs from the invoice, when the IPN arrives, then the invoice stays Pending and the answer is `RspCode` 04. Source: `PaymentCallbackTests.HandlePaymentCallbackAsync_AmountMismatch_ReturnsInvalidAmount_InvoiceStaysPending`; `PaymentControllerCallbackTests.PaymentCallback_VnPay_InvalidAmount_ReturnsRspCode04`.
- **AC-07.5** (boundary) Given the same IPN is delivered twice, even at the same moment, when both are processed, then side effects run once and the second gets "already confirmed" (`RspCode` 02). Source: `PaymentCallbackTests.HandlePaymentCallbackAsync_AlreadyPaidInvoice_ReturnsAlreadyConfirmed_NoWrites`, `HandlePaymentCallbackAsync_TwoConcurrentDeliveries_ApplyTheSideEffectsExactlyOnce`.
- **AC-07.6** (error) Given an unhandled server error while processing a VNPay IPN, when it happens, then the answer is still HTTP 200 with `RspCode` 99, so VNPay does not retry in a storm. Source: `PaymentControllerCallbackTests.PaymentCallback_VnPay_UnhandledException_ReturnsRspCode99_Not500`.
- **AC-07.7** (error) Given an unsigned Sandbox callback for an invoice owned by VNPay, when it arrives, then it is rejected and nothing is written. Source: `PaymentCallbackTests.HandlePaymentCallbackAsync_SandboxCallbackOnVnPayInvoice_IsRejected_NoWrites`.
- **AC-07.8** (boundary) Given the amount due is zero or less (a 100% promotion program, or points covering the whole remainder, until the per-theater maximum discount follow-up caps it), when I start payment, then the invoice is finalized as Paid without calling any gateway. Source: `PaymentCallbackTests.InitiatePaymentAsync_ZeroAmountDue_FinalizesWithoutAnyGateway`.
- **AC-07.9** (error) Given an invoice that is not mine, or not Pending, when I start payment, then nothing happens. Source: `PaymentCallbackTests.InitiatePaymentAsync_ByANonOwner_ReturnsNull_AndTouchesNothing`, `InitiatePaymentAsync_OnANonPendingInvoice_ReturnsNull`.

### US-08 Honest pending state after the redirect

- As a Customer, if my payment gateway redirect returns before the payment is actually confirmed, I want the app to keep checking and tell me "payment pending" rather than falsely confirming my booking, so that I'm never shown a ticket I didn't actually pay for.

- **AC-08.1** (success) Given I return from the gateway before the IPN has arrived, when the return page loads, then it polls the invoice every 2 seconds, up to 15 times (about 30 seconds), and shows "payment pending" if it is still Pending. Source: `cinemafe/projects/CinemaUser/src/app/features/modules/booking/payment-return/payment-return.component.ts:18-19`.
- **AC-08.2** (error) Given a real gateway, when the browser asks the server to confirm payment, then the confirm is refused; only the signed IPN can mark the invoice Paid. Source: `cinemabe/Cinema/2-Business/Cinema.Business/Payments/VnPayGateway.cs:85-86` (`VerifyPaymentAsync` always returns not-confirmed); `business-rules.md` section 3 ("Callback-authoritative"); `PaymentCallbackTests.ConfirmPaymentAsync_InvoiceOwnedByUnavailableProvider_DoesNotFallBackToSandbox`.
- **AC-08.3** (boundary) Given the invoice was cancelled or the payment failed, when I am on the return page, then polling stops and the cancelled state is shown, instead of "pending" for 30 seconds. Source: `payment-return.component.ts:78-79`; fix `dfdcc2c`.

### US-09 Seat hold expiry

- As a Customer, I want my seat hold to expire automatically if I abandon checkout, so that other customers can book those seats again.

- **AC-09.1** (success) Given a Pending invoice older than 15 minutes with no payment started, when the reaper runs (every minute), then it is cancelled, its seats freed, redeemed points restored, and food stock restocked. Source: `PaymentCallbackTests.ExpireStalePendingBookingsAsync_RestoresRedeemedPoints_AndFreesTheSeats`; `PendingBookingReaper.cs:12-13`.
- **AC-09.2** (boundary) Given a payment was started, when the reaper runs, then the invoice gets an extra 15 minutes of grace (30 minutes in all), so a late IPN can still confirm it. Source: `PaymentCallbackTests.ExpireStalePendingBookingsAsync_GivesAnInFlightPaymentTheExtraGraceWindow`; `BookingManager.cs:1831`.
- **AC-09.3** (boundary) Given an invoice is paid at the moment the reaper looks at it, when both run, then the reaper skips it. Source: `PaymentCallbackTests.ExpireStalePendingBookingsAsync_SkipsInvoiceThatWasJustPaid`.
- **AC-09.4** (error) Given a customer, when they look for a cancel button on a Pending booking, then there is none: customer cancellation was removed, only the reaper clears a Pending invoice. Source: `requirements.md` section 1, "Explicitly not supported (Customer)".

### US-10 Rate and review a movie

- As a Customer, I want to rate and comment on a movie once I hold a paid ticket for a screening of it that has already finished (not before), so that reviews come from people who bought a seat. (The rule checks a `Paid`, active ticket whose showtime `EndTime` has passed; it does not require the ticket to have been scanned at the gate, so a no-show who paid can still review.)

- **AC-10.1** (success) Given a Paid ticket for a finished showtime of the movie, when I rate it with a score of 1 to 10, then my rating is saved or updated. Source: `MovieServiceTests.RateMovieAsync_Watched_Upserts`.
- **AC-10.2** (error) Given I did not watch the movie, when I rate or comment, then the answer is 403 and nothing is written. Source: `MovieServiceTests.RateMovieAsync_NotWatched_ThrowsAccessDenied_AndDoesNotWrite`, `AddCommentAsync_NotWatched_ThrowsAccessDenied_AndDoesNotCreate`.
- **AC-10.3** (boundary) Given a score of 0 or 11, when I rate, then it is rejected with 400; given a comment of 2,000 characters it is accepted, of 2,001 or blank it is rejected. Source: `MovieServiceTests.RateMovieAsync_ScoreOutsideOneToTen_Throws_AndDoesNotWrite`, `AddCommentAsync_Exactly2000Chars_Succeeds`, `AddCommentAsync_Over2000Chars_Throws`, `AddCommentAsync_Blank_Throws`.
- **AC-10.4** (boundary) Given two concurrent first ratings from me, when both arrive, then the second becomes an update of the first. Source: `MovieServiceTests.RateMovieAsync_ConcurrentFirstRating_BecomesUpdateOfWinningRow`.
- **AC-10.5** (error) Given a reply whose parent is missing, in another movie, not approved or itself a reply, when I post it, then it is rejected; replies are one level deep. Source: `MovieServiceTests.AddCommentAsync_InvalidReplyParent_Throws`.
- **AC-10.6** (error) Given a comment by someone else, when I edit or delete it, then the answer is 403; deleting my own comment also deletes its replies. Source: `MovieServiceTests.UpdateOwnCommentAsync_NonAuthor_ThrowsAccessDenied`, `DeleteOwnCommentAsync_Author_RemovesCommentAndReplies`.

### US-11 Splash banner

- As a Customer, I want to see a one-time promotional popup (splash banner) when a new campaign is live, so that I notice special offers without it reappearing every visit.

- **AC-11.1** (success) Given a published banner whose date range includes today (Vietnam local date), when I first land on a page outside booking or sign-in, then the popup opens once and is remembered for this browser session. Source: `SplashBannerServiceTests.GetActiveAsync_ReturnsSingleItemWhenOneIsActive`; `cinemafe/projects/CinemaUser/src/app/shared/splash-banner/splash-banner.service.ts:8-10`, `:28-34`.
- **AC-11.2** (boundary) Given I have already seen that banner in this browser session, when I reload the app, then it does not reopen (remembered per banner id in `sessionStorage`). The check runs once per app start (`init` takes the first allowed navigation only, `take(1)`), so a banner published while I keep the app open appears only after a full reload. A browser that blocks storage shows the banner on every app start. Source: `splash-banner.service.ts:43-57`.
- **AC-11.3** (boundary) Given my first navigation ends in `/booking` or `/auth`, when it ends, then no popup is shown yet: those navigations are filtered out before `take(1)`, so the popup is deferred to the first later navigation outside those prefixes, not skipped for the whole visit. Source: `splash-banner.service.ts:10`.
- **AC-11.4** (boundary) Given two banners are active, when the active banner is requested, then the one with the latest start date is returned. Source: `cinemabe/Cinema/3-Data/Cinema.Data/Stores/SplashBannerStore.cs:22` (`OrderByDescending(b => b.StartDate)`); `business-rules.md` section 8. No test covers the two-active case: `SplashBannerServiceTests.GetActiveAsync_ReturnsSingleItemWhenOneIsActive` and `GetActiveAsync_UsesLocalDate_ReturnsEmptyWhenNoneActive` cover one and none.

### US-12 Booking history and e-tickets

- As a Customer, I want to view my booking history and e-tickets, so that I can find my QR code at the gate.

- **AC-12.1** (success) Given I am signed in, when I list my invoices, then I get my own invoices, paged. Source: `PaymentController.cs:302` (`GetMyInvoicesAsync(User.GetUserId(), search)`).
- **AC-12.2** (error) Given an invoice id that is not mine, when I open it as a Customer, then the answer is 403. Source: `PaymentController.cs:318`, `:323` (`GetByIdAsync(id, userId, isAdmin)`, `Forbid()` on `UnauthorizedAccessException`).
- **AC-12.3** (success) Given my invoice is Paid, when it is finalized, then an e-ticket confirmation is sent by email, and by SMS if I have a phone number, on a best-effort basis: a failed notification never fails the payment. Source: `business-rules.md` section 2 "Paid side effects"; `BookingConfirmationTemplateTests`.

### US-13 Notification preferences

- As a Customer, I want to opt out of promotional/reminder emails while keeping booking confirmations, so that I control what I'm notified about.

- **AC-13.1** (success) Given I turn showtime-reminder emails off, when the reminder job runs, then I get no reminder email. Source: `ShowtimeReminderService.cs:55`.
- **AC-13.2** (success) Given any preference setting, when I save, then the three flags `NotifyBookingEmails`, `NotifyPromotionEmails` and `NotifyReminderEmails` are stored on my account. Source: `AuthManager.UpdateNotificationPreferencesAsync`.
- **AC-13.3** (gap) Given I turn promotional emails off, when the system sends mail, then there is nothing to suppress: no promotional email is sent anywhere, so the flag is stored only. Source: a repository-wide search finds `NotifyPromotionEmails` only in `AuthManager` (`UpdateNotificationPreferencesAsync` and the consent record it writes), the DTOs and the entity; no send path reads it.
- **AC-13.4** (gap) Given I turn booking emails off, when a booking is confirmed, then the confirmation is still sent: the flag `NotifyBookingEmails` is stored but not read by any send path. Source: the same search finds it only in `AuthManager.UpdateNotificationPreferencesAsync`, the DTOs and the entity.
- **AC-13.5** (success) Given I create a new account, when it is saved, then promotional email is off and an audit row (`ConsentChanged`, source `registration`) records it; when I later turn it on or off in my profile, then one more row (source `profile`, with the new value and a timestamp) is written, and none is written when the promotional value is unchanged. Source: `AuthManager.StageConsentAsync`.

## TheaterStaff

### US-14 Counter sale

- As a box-office staff member, I want to quote and ring up a walk-in ticket sale, so that a customer without the app can still buy tickets at the counter.

- **AC-14.1** (success) Given free seats and a cash plus card split that covers the amount, when I ring up the sale, then a Paid counter invoice is created with its tenders in one transaction. Source: `BoxOfficeTests.Sell_SplitCashAndCard_CreatesPaidCounterInvoiceWithTenders`.
- **AC-14.2** (success) Given I only want a price, when I quote, then it is priced like a sale but nothing is saved. Source: `BoxOfficeTests.Quote_PricesLikeASale_ButHasNoSideEffects`.
- **AC-14.3** (error) Given a cash shortfall, when I submit, then it is rejected and nothing is saved; an overpayment returns change. Source: `BoxOfficeTests.Sell_CashShortfall_IsRejectedAndNothingIsSaved`, `Sell_CashOverpayment_ReturnsChange`.
- **AC-14.4** (error) Given an empty cart, seats without a showtime, or the same seat listed twice, when I submit, then it is rejected. Source: `BoxOfficeTests.Sell_EmptyCart_IsRejected`, `Sell_SeatsWithoutAShowtime_AreRejected`; `BoxOfficeManager.cs:53-61`.
- **AC-14.5** (error) Given a seat already sold, blocked, or held by another terminal, when I sell it, then it is rejected, while a hold of my own passes. Source: `BoxOfficeTests.Sell_SeatAlreadySold_IsRejected`, `Sell_BlockedSeat_IsRejected_OnBothCounterAndOnlinePaths`, `Sell_SeatHeldByAnotherTerminal_IsRejected_ButTheOwnHoldPasses`.
- **AC-14.6** (error) Given a showtime that has ended, or one that belongs to another theater, when I sell for it, then it is rejected (400) or forbidden (403). Source: `BoxOfficeTests.Sell_ShowtimeThatHasEnded_IsRejected`, `Sell_ShowtimeOfAnotherTheater_IsForbidden`.
- **AC-14.7** (error) Given I am not an Approver, when I try a price override, then it is forbidden and nothing is saved; an Approver can only lower the price, never raise it, and the override is audited. Source: `BoxOfficeTests.Sell_PriceOverrideByNonApprover_IsForbidden_AndNothingIsSaved`, `Sell_PriceOverrideAboveTheListPrice_IsRejected`, `Sell_PriceOverrideByApprover_AppliesTheLowerPriceAndIsAudited`.
- **AC-14.8** (boundary) Given a walk-in with no account, when the sale completes, then no points accrue and no email is sent; with an attached member, points accrue and a confirmation is sent. Source: `BoxOfficeTests.Sell_WalkIn_HasNoUser_NoPointsAndNoEmail`, `Sell_WithAttachedMember_AccruesPointsAndSendsConfirmation`.

### US-15 Look up a customer by phone

- As a box-office staff member, I want to look up a returning customer by phone, so that I can attach their loyalty account to a counter sale.

- **AC-15.1** (success) Given a Customer account with that phone number, when I look it up, then I get the customer and membership details. Source: `BoxOfficeManager.cs:99-118`.
- **AC-15.2** (error) Given no account, or an account that is not a Customer, when I look it up, then it fails with "No customer with that phone number." Source: `BoxOfficeManager.cs:105`.
- **AC-15.3** (error) Given I try to redeem points without attaching a member, when I submit the sale, then it is rejected with "Attach a member to redeem points." Source: `BoxOfficeTests.Sell_RedeemingPointsWithoutAMember_IsRejected`; `BoxOfficeManager.cs:69`.

### US-16 Gate scan

- As a gate staff member, I want to scan a customer's QR ticket and get an immediate accept/deny result with a reason, so that I can admit valid ticket holders and catch invalid/duplicate/wrong-showtime tickets.

- **AC-16.1** (success) Given a Paid, active, unused ticket for this theater inside the admit window, when I scan it, then the result is Admitted with the movie, room and seat, and the ticket is marked used by me. Source: `GateManagerTests.Scan_ValidTicket_IsAdmitted_WithDetails`.
- **AC-16.2** (error) Given an unknown or blank code, when I scan, then the result is NotFound. Source: `GateManagerTests.Scan_UnknownCode_IsNotFound`, `Scan_BlankCode_IsNotFound`.
- **AC-16.3** (error) Given an unpaid invoice or a deactivated ticket, when I scan, then the result is NotPaid. Source: `GateManagerTests.Scan_InvoiceNotPaid_IsNotPaid`, `Scan_InactiveTicket_IsNotPaid`.
- **AC-16.4** (error) Given a ticket of another theater, when I scan, then the result is WrongTheater and reveals nothing about the movie, patron or who used it. Source: `GateManagerTests.Scan_TicketOfAnotherTheater_IsWrongTheater_AndRevealsNothing`, `Scan_UsedTicketOfAnotherTheater_IsWrongTheater_AndDoesNotLeakWhoUsedIt`.
- **AC-16.5** (error) Given a ticket already scanned, when I scan it again, then the result is AlreadyUsed with when and by whom; with a wrong showtime filter the result is WrongShowTime. Source: `GateManagerTests.Scan_UsedTicket_IsAlreadyUsed_WithUsedAtAndBy`, `Scan_ShowTimeFilterMismatch_IsWrongShowTime`.
- **AC-16.6** (boundary) Given the showtime starts in more than 30 minutes, when I scan, then the result is TooEarly; at exactly 30 minutes it is admitted; after the end time it is Expired; the 30 is configurable by `Gate:AdmitBeforeMinutes`. Source: `GateManagerTests.Scan_MoreThanThirtyMinutesBeforeStart_IsTooEarly`, `Scan_ExactlyThirtyMinutesBeforeStart_IsAdmitted`, `Scan_AfterEndTime_IsExpired`, `Scan_AdmitWindowIsConfigurable`; `GateManager.cs:17`.
- **AC-16.7** (boundary) Given a movie whose minimum age is at or above the age-prompt threshold (default 13, configurable as `Gate:AgePromptMinAge`), when I scan without confirming age, then the result is AgeCheckRequired and the ticket is not marked used (T13, T16 and T18 all prompt); a K-rated ticket never prompts and is admitted with `GuardianRequired=true` so the gate shows a "parent or guardian" banner. Source: `GateManagerTests.Scan_T18Ticket_NeedsAgeCheck_AndIsNotMarkedUsed`, `Scan_T16Ticket_AgeConfirmed_IsAdmitted`, `Scan_T13Ticket_NeedsAgeCheck_AndIsNotMarkedUsed`, `Scan_KTicket_IsAdmitted_WithGuardianBanner`; `GateManager.cs:16-19`.
- **AC-16.8** (boundary) Given two scanners scan the same ticket at the same instant, when both run, then exactly one is Admitted and the other gets AlreadyUsed; every attempt, of any outcome, is written to the audit log, best-effort. Source: `GateManagerTests.Scan_ConcurrentDoubleScan_AdmitsExactlyOnce`, `Log_ConcurrentDoubleScan_LogsExactlyOneAdmittedAndOneAlreadyUsed_AgainstTheOriginalTicket`, `Log_SaveChangesThrows_ScanStillReturnsCorrectResult_AndDoesNotThrow`.

### US-17 Manual ticket lookup

- As a gate staff member, I want to look up a paid ticket by invoice code or phone when a customer's QR code won't scan, so that I can still admit them manually.

- **AC-17.1** (success) Given an invoice code or phone, when I look up today's tickets for my theater, then tickets are grouped per invoice and the phone is masked. Source: `GateManagerTests.Lookup_GroupsTicketsPerInvoice_AndMasksPhone`.
- **AC-17.2** (error) Given neither a code nor a phone, when I look up, then the request is rejected. Source: `GateManagerTests.Lookup_WithoutCodeOrPhone_IsRejected`.

### US-18 Schedule board

- As a staff member, I want to view the day's schedule board for my theater, so that I know which rooms/showtimes are running.

- **AC-18.1** (success) Given showtimes in my theater, when I open the board, then I see each screening with sold seats against capacity and the turnover buffer gap after it, computed with one grouped query for the sold counts. Source: `OperationsBoardAndIncidentTests.Board_ShowsBufferGap_SoldAndCapacity_WithOneGroupedSoldQuery`.
- **AC-18.2** (error) Given I am neither Admin nor TheaterStaff, when I call the board, then the endpoint answers 403. Source: `CinemaController.cs:2145` (`[Authorize(Roles = RoleNames.StaffApp)]`); `Cinema.Service.WebApiHost.Tests/StaffToolsetAuthorizationTests.cs`.
- **AC-18.3** (boundary) Given aisle cells in a room, when capacity is counted, then aisles are excluded. Source: `business-rules.md` section 1 "Aisle cells"; `ShowTimeStore.cs:44`.

### US-19 Report an incident

- As a staff member, I want to report an incident (seat/room/projection/sound/safety/customer/cleanliness issue), so that it's tracked and can be resolved before it affects more customers.

- **AC-19.1** (success) Given any staff member, when I report an incident, then an Open incident is created and nothing is blocked. Source: `OperationsBoardAndIncidentTests.Report_AnyStaff_CreatesOpenIncident_WithoutBlockingAnything`.
- **AC-19.2** (error) Given a seat that does not belong to the given room, when I report, then it is rejected. Source: `IncidentManager.cs:40`.
- **AC-19.3** (error) Given I am not an Approver, when I block a seat or room or resolve with unblock, then it is forbidden (403); blocking an aisle or an already blocked seat is rejected (400); a seat of another theater is 403. Source: `OperationsBoardAndIncidentTests.BlockSeat_AnAisle_Throws`, `BlockSeat_SeatOfAnotherTheater_Is403`, `Resolve_WithUnblock_ReactivatesSeat_ByApproverOnly`; `IncidentManager.cs:101`, `:215`, `:223`.
- **AC-19.4** (boundary) Given an incident already resolved, when I resolve it again, then it is rejected (400); an incident outside my theater is 403. Source: `OperationsBoardAndIncidentTests.Resolve_WithoutUnblock_LeavesBlockInPlace_AndTwiceIs400`, `Resolve_IncidentOutsideScope_Is403`.
- **AC-19.5** (success) Given an Approver blocks a double seat, when it is blocked, then both halves are blocked and the affected tickets are returned (nothing is cancelled automatically). Source: `OperationsBoardAndIncidentTests.BlockSeat_DoubleSeat_BlocksBothHalves`, `BlockSeat_ByApprover_DeactivatesSeat_ReturnsAffectedTickets_CancelsNothing_AndAudits`.

## Admin

### US-20 Catalog and pricing

- As an Admin, I want full CRUD over movies, theaters, rooms, room types, seat types, patron categories, pricing (time-slot multipliers, holidays, 3D surcharge, per-showtime base price), so that I can configure the entire catalog and pricing model without code changes.

- **AC-20.1** (success) Given valid data, when I create a movie, then it gets a unique slug (a numeric suffix is added if the base slug is taken). Source: `MovieServiceTests.CreateAsync_FreeBaseSlug_UsesBaseSlug`, `CreateAsync_TakenBaseSlug_AppendsSuffix`.
- **AC-20.2** (success) Given a movie, when I delete it, then it is deactivated, not removed. Source: `MovieServiceTests.DeleteAsync_ExistingMovie_SetsIsActiveFalse`.
- **AC-20.3** (error) Given a showtime window shorter than the movie runtime, an inactive movie, a date outside the movie's release range, or a room under maintenance, when I schedule it, then it is rejected. Source: `ShowTimeServiceTests.CreateAsync_RejectsWindowShorterThanMovieRuntime`, `CreateAsync_RejectsShowTime_WhenMovieIsInactive`, `CreateAsync_RejectsShowTime_BeforeMovieReleaseDate`, `CreateAsync_RejectsShowTime_WhenRoomIsUnderMaintenance`.
- **AC-20.4** (boundary) Given the release date or end date itself, when I schedule on that day, then it is allowed; a movie with no end date is open-ended. Source: `ShowTimeServiceTests.CreateAsync_AllowsShowTime_OnTheReleaseDateItself`, `CreateAsync_AllowsShowTime_OnTheEndDateItself`, `CreateAsync_AllowsShowTime_WhenMovieHasNoEndDate` (each asserts that the showtime is created). These cover `CreateAsync` only; for `UpdateAsync` the tests cover the skip rule for past showtimes (`UpdateAsync_SkipsMovieAvailability_ForAPastShowTime`), not the inclusive bounds.
- **AC-20.5** (error) Given a showtime that overlaps another in the room, or leaves a gap smaller than the room type's turnover buffer, when I schedule it, then it is rejected; a 3D showtime needs a room class that supports 3D. Source: `ShowTimeServiceTests.CreateAsync_RejectsShowTime_WhenGapToExistingIsSmallerThanBuffer`, `CreateAsync_RejectsThreeD_WhenRoomClassHasNoThreeDProjector`.
- **AC-20.6** (error) Given a patron category whose seat type belongs to another theater, or a duplicate name and seat type, when I save it, then it is rejected. Source: `PatronCategoryServiceTests.CreateAsync_SeatTypeFromAnotherTheater_Throws`, `CreateAsync_DuplicateNameAndSeatType_Throws`.
- **AC-20.7** (success) Given a holiday, when a showtime falls on it, then the holiday multiplier wins outright and the weekday matrix is not applied; the 3D surcharge and per-showtime base price are added flat afterwards. Source: `BookingServiceTests.GetShowTimePricesAsync_Holiday_OverridesWeekdayMatrixRow`, `GetShowTimePricesAsync_FullFormula_MatchesWorkedExample`.
- **AC-20.8** (error) Given a category with no price row for a room class, when a customer books it there, then it is rejected, with no fallback price. Source: `BookingServiceTests.CreateBookingAsync_RejectsCategoryNotIncludedInRoomType`.
- **AC-20.9** (error) Given a caller who is not an Admin, when they call a catalog write endpoint, then the answer is 403. Source: `Cinema.Service.WebApiHost.Tests/ControllerAuthorizationCoverageTests.cs` (every action guarded or allow-listed); `docs/01-business/roles-permissions.md`.

### US-21 Promotion programs

- As an Admin, I want to create promotion programs (fixed-price or percent-off, scoped by theater/day/time/channel, with an auto-apply option), so that I can run campaigns that automatically discount eligible bookings.

- **AC-21.1** (success) Given a valid Everyone or Members auto-apply program, when I save it, then it is stored. Source: `PromotionProgramServiceTests.CreateAsync_ValidEveryoneAutoApplyProgram_Saved`, `CreateAsync_ValidMembersProgram_Saved`.
- **AC-21.2** (error) Given auto-apply combined with a code, with the Info mechanic, or with Student/Teacher eligibility, when I save, then it is rejected. Source: `PromotionProgramServiceTests.CreateAsync_AutoApplyWithCode_Rejected`, `CreateAsync_AutoApplyInfo_Rejected`, `CreateAsync_AutoApplyStudentOrTeacher_Rejected`.
- **AC-21.3** (error) Given a duplicate code, a fixed-price program with no price rows, a percent outside range, or an end date before the start, when I save, then it is rejected. Source: `PromotionProgramServiceTests.CreateAsync_DuplicateCode_Rejected`, `CreateAsync_FixedPriceWithNoPriceRows_Rejected`, `CreateAsync_PercentOffOutOfRange_Rejected`, `CreateAsync_EndBeforeStart_Rejected`.
- **AC-21.4** (boundary) Given two auto-apply programs both qualify, when a booking is priced, then the one with the bigger saving wins; a fixed price above the list price gives no saving. Source: `BookingServiceTests.CreateBookingAsync_TwoAutoApplyPrograms_BiggerSavingWins`, `CreateBookingAsync_FixedPriceAboveListPrice_GivesNoSaving`.
- **AC-21.5** (boundary) Given a program excludes holidays or is limited to another day or to the counter channel, when a booking is priced, then it is not applied. Source: `BookingServiceTests.CreateBookingAsync_ProgramExcludesHolidays_NotAppliedOnHolidayShowtime`, `CreateBookingAsync_ProgramScheduleRestrictedToOtherDay_NotApplied`, `CreateBookingAsync_ProgramChannelRestrictedToCounterOnly_NotAppliedOnline`.

### US-22 Loyalty rates

- As an Admin, I want to configure the loyalty point rates for each theater (default 1,000 VND = 1 point; 100 points = 1,000 VND), so that different theaters can have different point-earning/redemption rates if needed.

- **AC-22.1** (success) Given a theater with no override, when rates are read, then the built-in defaults apply (1,000 VND per point earned, 10 VND per point redeemed). Source: `LoyaltySettingServiceTests.GetEffectiveAsync_NoRow_ReturnsBuiltInDefaults`; `LoyaltyRates.cs:13-14`.
- **AC-22.2** (success) Given a valid override, when I save, then it is created or updated, and resetting deletes the row so the default returns. Source: `LoyaltySettingServiceTests.SaveAsync_NoExistingRow_CreatesOne`, `SaveAsync_ExistingTheaterOverride_UpdatesIt`, `ResetToDefaultAsync_ExistingRow_DeletesAndReturnsTrue`.
- **AC-22.3** (error) Given an unknown theater, or a rate that is zero or negative, when I save, then it fails (not found, or invalid). Source: `LoyaltySettingServiceTests.SaveAsync_UnknownTheater_ThrowsNotFound`, `SaveAsync_NonPositiveValue_Throws`.

### US-23 Moderate comments

- As an Admin, I want to moderate customer comments (approve/reject/delete), so that inappropriate content doesn't stay visible on the site.

- **AC-23.1** (success) Given a comment, when I approve or reject it, then its `IsApproved` flag changes and the call returns true. Source: `MovieManager.cs:469-480`.
- **AC-23.2** (error) Given a comment id that does not exist, when I moderate or delete it, then the call returns false. Source: `MovieManager.cs:471-475`, `MovieManager.cs:484-488`.
- **AC-23.3** (success) Given a comment with replies, when I delete it, then its replies are deleted with it. Source: `MovieManager.cs:482-491` (`DeleteWithRepliesAsync`).
- **AC-23.4** (boundary) Given a reply is posted under an unapproved parent, when it is submitted, then it is rejected, so rejecting a comment also stops new replies to it. Source: `MovieServiceTests.AddCommentAsync_InvalidReplyParent_Throws`; `MovieManager.cs:372`.
- **AC-23.5** (gap) Given the moderation actions above, when the test suite runs, then nothing checks them: no test class calls `ModerateCommentAsync` or `DeleteCommentAsync`.

### US-24 (removed)

- Gift cards were removed on 2026-11-14 together with the membership-tier percentage discount; this number is kept so other story ids stay stable.

### US-25 Invoice override

- As an Admin, I want to view and override an invoice's status when something goes wrong operationally, so that I can correct bookings outside the normal customer/gateway flow (being aware that since `0862cb2` the override is limited to constrained, atomic transitions and does not refund a Paid invoice).

- **AC-25.1** (success) Given a Pending invoice, when I move it to Cancelled, then seats are freed and redeemed points and food stock are restored. Source: `InvoiceAdminManagerTests.UpdateStatus_PendingToCancelled_FreesSeatsAndRestoresReservations`.
- **AC-25.2** (success) Given a Pending invoice, when I move it to Paid, then a payment row is added and loyalty points accrue exactly once. Source: `InvoiceAdminManagerTests.UpdateStatus_PendingToPaid_AddsPaymentRowAndAccruesLoyaltyOnce`.
- **AC-25.3** (boundary) Given a Paid invoice, when I move it to Cancelled, then its tickets are deactivated but points spend is not refunded. Source: `InvoiceAdminManagerTests.UpdateStatus_PaidToCancelled_DeactivatesTickets_ButDoesNotRefundPoints`.
- **AC-25.4** (error) Given a closed invoice, when I try to reopen it, then it is refused and nothing is written. Source: `InvoiceAdminManagerTests.UpdateStatus_ReopeningAClosedInvoice_Throws_NoWrites`.
- **AC-25.5** (boundary) Given the invoice changed at the same moment, when I submit, then the losing change throws and rolls back. Source: `InvoiceAdminManagerTests.UpdateStatus_LosesTheClaim_ThrowsAndRollsBack`.
- **AC-25.6** (success) Given a Pending invoice, when I delete it, then points are restored; deleting a Paid invoice restores nothing. Source: `InvoiceAdminManagerTests.Delete_PendingInvoice_RestoresPoints`, `Delete_PaidInvoice_DoesNotRestoreAnything`.

### US-26 Reports

- As an Admin, I want to view revenue and sales/occupancy/KPI reports, so that I can track theater performance.

- **AC-26.1** (success) Given a date range, when I run sales by day, movie or payment method, then ticket and food revenue are separate and empty days are filled. Source: `StaffReportTests.Sales_ByDay_SplitsTicketAndFood_AndFillsEmptyDays`, `Sales_ByPaymentMethod_UsesTenderNames`.
- **AC-26.2** (boundary) Given a range of 92 days it is accepted, and of 93 days or more it is rejected with "The report range is limited to 92 days."; an end date before the start date is also rejected with "The end date must not be before the start date." Source: `StaffReportManager.Sales.cs:11`, `:164-166`, `:160-163`. The only test is the rejection side, `StaffReportTests.Sales_RangeOver92Days_IsRejected` (From plus 92 days, which is a 93-day inclusive range). No test covers the at-limit 92-day success or the end-before-start rejection.
- **AC-26.3** (error) Given a theater outside my scope, when I request it, then the answer is 403; an empty scope returns an empty report without querying. Source: `StaffReportTests.Sales_ScopedCallerRequestingAnotherTheater_Throws403Exception`, `Sales_EmptyScope_GetsEmptyReport_WithoutQuerying`.
- **AC-26.4** (boundary) Given no data, when I open the KPIs, then every figure is zero. Source: `StaffReportTests.Kpis_NoData_ReturnsZeros`.
- **AC-26.5** (success) Given a report, when I export, then the CSV is built in the browser from the JSON; there is no server-side export. Source: `business-rules.md` section 8 "CSV export is client-side only".

### US-27 Audit log

- As an Admin, I want to view the audit log, so that I can trace who performed sensitive actions (price overrides, voids, compensation, gate scans, etc.).

- **AC-27.1** (success) Given audit entries, when I open the log, then they are listed newest first, filterable by action, date range, actor and theater. Source: `CinemaController.cs:2039-2043` (summary and `[Authorize]`) and `:2046-2064`; `StaffReportManager.cs:26`.
- **AC-27.2** (error) Given a theater outside my scope, when I filter on it, then the answer is 403; the endpoint itself is gated to `RoleNames.Reporting` (Admin). Source: `CinemaController.cs:2043`, `:2051-2062`; `RoleNames.cs:33`.
- **AC-27.3** (success) Given a gate scan of any outcome, a price override or a seat block, when it happens, then an `AuditLog` row exists for it. Source: `GateManagerTests.Log_Admitted_StagesOutcome_WithSnapshotAndRequestedShowTimeId`, `BoxOfficeTests.Sell_PriceOverrideByApprover_AppliesTheLowerPriceAndIsAudited`, `OperationsBoardAndIncidentTests.BlockSeat_ByApprover_DeactivatesSeat_ReturnsAffectedTickets_CancelsNothing_AndAudits`.
- **AC-27.4** (boundary) Given an audited user, theater or entity is later deleted, when I open the log, then the row remains, because the table has no foreign keys. Source: `docs/03-technical/database-schema.md` ("AuditLog (insert-only, no FKs by design)").

### US-28 Splash banners and templates

- As an Admin, I want to manage splash banners and notification templates, so that I can control customer-facing messaging without a code deploy.

- **AC-28.1** (success) Given a title, an image URL and a valid date range, when I create a banner, then it is saved with the title trimmed and the time of day stripped from the dates. Source: `SplashBannerServiceTests.CreateAsync_TrimsTitleAndStripsTimeOfDay`.
- **AC-28.2** (error) Given an end date before the start date, a blank image URL, or a link that is not an absolute http(s) URL or a site-relative path, when I save, then it is rejected (this blocks `javascript:` links). Source: `SplashBannerServiceTests.CreateAsync_EndBeforeStart_Rejected`, `CreateAsync_BlankImageUrl_Rejected`, `CreateAsync_DisallowedLink_Rejected`.
- **AC-28.3** (error) Given an id that does not exist, when I update a banner, then it fails as not found. Source: `SplashBannerServiceTests.UpdateAsync_MissingId_ThrowsKeyNotFound`.
- **AC-28.4** (success) Given a template, when a booking confirmation is sent, then it is rendered from the stored template with `{{Token}}` placeholders. Source: `TemplateRendererTests`, `TemplateManagerTests`, `BookingConfirmationTemplateTests`.

### US-29 User accounts and roles

- As an Admin, I want to create and manage staff/customer user accounts and assign roles, so that I control who has access to what.

- **AC-29.1** (success) Given a theater-scoped role and a valid theater, when I create or update a user, then the theater is kept; for any other role the theater is forced to none. Source: `AuthUserTheaterTests.CreateUser_TheaterRoleWithValidTheater_KeepsTheater`, `CreateUser_NonTheaterRole_ForcesTheaterNull`.
- **AC-29.2** (error) Given a theater-scoped role with no theater, or an unknown theater, when I save, then it fails with "A theater is required for the {role} role." or "Theater not found." Source: `AuthUserTheaterTests.CreateUser_TheaterRoleWithoutTheater_Throws`, `CreateUser_TheaterRoleWithUnknownTheater_Throws`; `AuthManager.cs:624`, `:630`.
- **AC-29.3** (error) Given an email or phone already in use, when I create a user, then it fails. Source: `AuthManager.cs:537`, `:541`.
- **AC-29.4** (boundary) Given the only active Admin, when I demote, deactivate or delete that account, then it fails with "Cannot remove, demote or deactivate the last active administrator."; when another active Admin exists, deletion soft-deletes (sets Inactive). Source: `AuthServiceTests.DeleteUser_LastActiveAdmin_Throws_AndStaysActive`, `UpdateUser_DemotingLastActiveAdmin_Throws`, `DeleteUser_AdminWhenAnotherAdminExists_SoftDeletes`; `AuthManager.cs:661`.
- **AC-29.5** (boundary) Given an account was deactivated, when its old token is presented, then it still works until it expires (24 hours): there is no revocation list. Source: `business-rules.md` section 6 "Only Active accounts sign in"; `JwtTokenService.cs:50`.

## Explicitly out of scope (do not plan around these as if they exist)

- Nobody — Customer, TheaterStaff, or Admin — can issue a refund or reverse a `Paid` invoice's financial state today; this was built, then fully reverted. A `Paid` invoice can only be cancelled/overridden via the constrained admin status update, which does not reverse points spend or touch any payment gateway.
- No role can export a report as a server-generated file — CSV generation happens client-side in the Admin UI from the same JSON the reports endpoints return.
- Roster/shift scheduling and the manager-override-PIN mechanism were both removed outright (not just hidden) — blocking/unblocking a seat or room, and POS price overrides, now require the `Approvers` role (Admin) directly, with no self-service PIN or roster screen of any kind.
