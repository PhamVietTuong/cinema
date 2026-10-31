# Cinema Domain Model (EF Core)

Describes the EF Core domain model behind `cinemabe` (Cinema.Data.Entities + Cinema.Data.Configurations), and the Cinema.Business.DTO shapes exposed over the API. Every entity inherits `BaseEntity`: `Id` (Guid, client-generated), `CreationTime` (DateTime, UTC), `LastUpdatedTime` (DateTime?). `Invoice` and `User` redeclare `Id` with `new` (same type/semantics, just hides the base property — no behavioral difference). Schema changes go through hand-edited `create_db.sql`/`upgrade_db.sql`, not EF migrations (see root CLAUDE.md), even though fluent `Configurations/` still describe the model EF expects.

## 1. Identity / Users

### User
| Field | Type | Notes |
|---|---|---|
| Phone, Email, Name | string | required, max 20/200/200; both Email and Phone unique |
| Avatar | string? | |
| PasswordHash, PasswordSalt | byte[] | required |
| Status | UserStatus | Active/Inactive/Banned |
| UserTypeId | Guid | FK -> UserType, Restrict |
| TheaterId | Guid? | staff accounts' single managed theater; null for admin/customer; FK -> Theater, SetNull, no inverse nav |
| MemberShipId | Guid? | FK -> MemberShip, SetNull |
| Points | int | loyalty point balance |
| NotifyBookingEmails/NotifyPromotionEmails/NotifyReminderEmails | bool | opt-out prefs, default true |
| PasswordResetTokenHash, PasswordResetExpiresAt | string?/DateTime? | SHA-256 hash of emailed token |
| FailedLoginCount, LockoutEndUtc | int/DateTime? | account lockout |
| EmailConfirmed, EmailVerificationTokenHash, EmailVerificationExpiresAt | | email verification |
| TwoFactorEnabled, TwoFactorCodeHash, TwoFactorCodeExpiresAt | | 2FA (emailed OTP) |
| OverridePinHash (maxlen 64), OverridePinSalt (maxlen 128), OverridePinFailedCount, OverridePinLockoutEndUtc | | staff-terminal manager-override PIN, PBKDF2 |

Relationships: `UserType` (Restrict), `MemberShip` (SetNull), `Theater` (SetNull, shadow FK only), nav collections `Invoices`, `Comments`, `Evaluations`.

### UserType
`Name` (string). Nav `Users` (1:many).

### MemberShip (loyalty tier)
`Name`, `MinPoints` (int), `MaxPoints` (int), `DiscountPercent` (double). Nav `Users`. Also referenced by `PromotionProgram.MinMemberShipId` as the minimum tier gate.

## 2. Catalog — Movies, Cast/Crew, Theaters, Rooms, Seats, Showtimes

### Movie
Fields: Title (300), Slug (320, **unique** `IX_Movie_Slug`), Description (2000), Duration (int, minutes), ReleaseDate/EndDate (DateOnly), PosterUrl, TrailerUrl, Director (200), Cast (1000, free-text legacy field — cast is now also modeled via `MovieCredit`), Language (100), Subtitle, **ImdbId** (20, nullable — used to match OMDb external ratings by IMDb ID when present, falling back to title/year match otherwise), IsActive, AgeRestrictionId (FK, Restrict).
Relationships: `MovieTypeDetails` (join to MovieType = genres), `ShowTimes` (Cascade from Movie), `Comments` (Cascade), `Evaluations` (Cascade), `MovieCredits` (Cascade).

### AgeRestriction
Code, Description, MinAge (int). Nav `Movies` (Restrict delete — can't delete a restriction in use).

### Person (cast/crew)
Name (200, **unique** `IX_Person_Name`), PhotoUrl (500).

### MovieCredit
Join of Movie x Person with a role. MovieId (Cascade), PersonId (Restrict), CreditType (enum), CharacterName (200, for Actor), DisplayOrder (int). Unique index on (MovieId, PersonId, CreditType) — a person can be credited once per role per movie (e.g. same person can't be Actor twice, but could be both Director and Actor).

### MovieType / MovieTypeDetail
`MovieType.Name` = genre label. `MovieTypeDetail` is the pure join table (composite PK MovieId+MovieTypeId), no cascade specified explicitly (EF Core conventions apply default Cascade via required FK).

### Comment
MovieId (Cascade), UserId (Restrict), Content (2000), ParentId (self-FK, Restrict — threaded replies via `Replies` nav), IsApproved (bool, default true — moderation flag).

### Evaluation (rating/review)
MovieId (Cascade), UserId (Restrict), Score (int), Review (1000). **Unique (MovieId, UserId)** — one review per user per movie.

### Theater
Name (200), Address (500), City (100), Phone, Email, ImageUrl, IsActive, Latitude/Longitude (double?, for nearest-theater search). Nav `Rooms` (Cascade).

### Room
Name (100), TheaterId (Cascade), RoomTypeId (Restrict), TotalRows/TotalColumns (int), Status (RoomStatus: Active/Maintenance/Inactive). Nav `Seats` (Cascade), `ShowTimeRooms`.

### RoomType (per-theater commercial class: Standard/IMAX/4DX/etc.)
TheaterId (Cascade), Name (100), Description, SupportsThreeD (bool), ThreeDSurcharge (double, flat per-ticket add-on for 3D), TurnoverBufferMinutes (int, default 0 — minutes reserved between screenings in this room class). Nav `Rooms`, `PatronCategoryPrices`.

### Seat
RoomId (Cascade), RowName (maxlen 5), ColIndex (int), IsActive, SeatGroupId (Guid?, nullable — links the 2 seats of a double/couple seat; **sole source of truth for seat kind**, no separate SeatTypeId on Seat). Unique (RoomId, RowName, ColIndex). Index on SeatGroupId.

### SeatType (per-theater: exactly Single + Double)
TheaterId (Cascade), Kind (SeatKind enum — matched by Kind, never by Name), Name (display only, renamable), Color (hex, default `#808080`). Unique (TheaterId, Kind). Nav `PatronCategories`.

### ShowTime
MovieId (Cascade), StartTime/EndTime (DateTime), ProjectionForm (TwoD/ThreeD — property of the *screening*, not the room), ShowTimeType (Normal/Premiere/Special), IsActive. Nav `ShowTimeRooms`.

### ShowTimeRoom (join: a showtime assigned to a room)
Composite PK (ShowTimeId, RoomId). BasePrice (int — flat per-showtime manual adjustment added on top of PatronCategory price; NOT the seat's base price). Nav `InvoiceTickets`.

## 3. Pricing

### PatronCategory (the pricing + eligibility engine)
One row per (Theater, SeatKind-via-SeatType, category name), e.g. Adult/Single, Adult/Double, Student/Single. **Omitting a row for a seat kind is the entire eligibility rule** — no separate allow-list.
| Field | Notes |
|---|---|
| TheaterId | Cascade |
| SeatTypeId | FK -> SeatType, Restrict (determines SeatKind) |
| Name (100), Description | |
| Price (double) | absolute VND, independent per row — a Double row is NOT 2x Single |
| IsActive | |
| RequiresIdCheck (bool, default false) | UI notice only, not enforced |
| IdCheckMessage (1000) | custom wording, falls back to default translation if null |
Unique (TheaterId, Name, SeatTypeId). Nav `RoomTypePrices`.

### RoomTypePatronCategoryPrice (per-RoomType override/allow-list)
RoomTypeId (Restrict), PatronCategoryId (Cascade — deleting the category removes the override), Price (double). Unique (RoomTypeId, PatronCategoryId). **A RoomType offers exactly the categories with a row here** — zero rows = nothing bookable, no fallback to PatronCategory.Price. Seeded 1 row per active theater-wide PatronCategory when a RoomType is created.

### TicketPrice (time-of-day/day-of-week multiplier)
(TheaterId Cascade, RoomTypeId Restrict, TimeSlotId Restrict, DayOfWeek) -> PriceMultiplier (double, default 1). Unique on the 4-tuple. Multiplies the resolved PatronCategory price. Holiday pricing is NOT here — see `Holiday`.

### TimeSlot
TheaterId (Cascade), Name, StartTime/EndTime (string "HH:mm", start inclusive/end exclusive). Nav `TicketPrices`.

### Holiday
Name, Date (DateOnly), PriceMultiplier (double, default 1.5). Matched by calendar date; overrides TicketPrice's day-of-week multiplier for that date. No TheaterId — global.

## 4. Promotions / Discounts / Loyalty / Gift Cards

### Discount (flat-percent code/auto-apply promo engine — the older of two promotion mechanisms)
| Field | Notes |
|---|---|
| Code (50)? | unique when present (`HasFilter("[Code] IS NOT NULL")` — multiple NULLs allowed for auto-apply promos with no code) |
| Description? | |
| Percent (double) | |
| MaxDiscountAmount (double)? | cap |
| DiscountTypeId | FK -> DiscountType, Restrict |
| StartDate/EndDate (DateTime) | |
| MaxUsage (int)?, UsedCount (int, default 0) | |
| IsActive, AutoApply (bool) | AutoApply = applied without a code |
| ApplyToAllTheaters (bool, default true) | else scoped via `DiscountTheaters` |
| MovieId (Guid?) | FK -> Movie, **SetNull** (deleting movie reverts to any-movie) |
| DaysOfWeekMask (int?) | bitmask, bit0=Sunday..bit6=Saturday |
| StartTimeOfDay/EndTimeOfDay (TimeOnly?) | inclusive daily window |
Nav `DiscountTheaters`.

### DiscountTheater (join)
DiscountId (Cascade), TheaterId (**Restrict** — avoids SQL Server multi-cascade-path error). Unique (DiscountId, TheaterId).

### DiscountType
Name. Nav `Discounts`.

### PromotionProgram (newer, richer "campaign card" promotion — Cinestar-style, deliberately separate from Discount)
Drives CinemaUser's promotions page as display content; only lowers checkout price when `AutoApply` is true.
| Field | Notes |
|---|---|
| Title (200), Summary (500)?, BannerUrl? | |
| Mechanic (PromotionMechanic) | Info / FixedPrice / PercentOff |
| PercentOff (double)? | required iff Mechanic=PercentOff, range (0,100] |
| Eligibility (PromotionEligibility) | Everyone/Members/StudentOrTeacher/AgeLimit |
| MinMemberShipId (Guid?) | FK -> MemberShip, **Restrict**; required iff Eligibility=Members |
| MaxAge (int)? | required iff Eligibility=AgeLimit |
| StartDate/EndDate (DateTime) | |
| ExcludeHolidays (bool) | display term, also a real AutoApply gate |
| AllowedChannelsMask (int?) | bitmask of SalesChannel; null = all |
| MaxTicketsPerTransaction (int)? | |
| StacksWithMembershipDiscount (bool) | |
| TermsText (2000)?, DisplayOrder (int), IsPublished (bool) | |
| AutoApply (bool) | requires Mechanic != Info, Eligibility in (Everyone, Members), LinkedDiscountId null |
| LinkedDiscountId (Guid?) | FK -> Discount, **SetNull**; cosmetic link only, doesn't itself change price |
Index: (IsPublished, EndDate), MinMemberShipId, LinkedDiscountId. Nav `Prices`, `Schedules`.

### PromotionProgramPrice (fixed-price rows, used when Mechanic=FixedPrice)
PromotionProgramId (Cascade), ProjectionForm (enum)?, SeatKind (enum)? — null = "any" for that dimension, most specific match wins. Price (double, all-in VND; a Double row prices the whole pair). Unique (PromotionProgramId, ProjectionForm, SeatKind) — at most one row per shape.

### PromotionProgramSchedule ("where and when" validity windows)
PromotionProgramId (Cascade), TheaterId (Guid?, **Restrict**, null = all theaters), DaysOfWeekMask (int?), StartTimeOfDay/EndTimeOfDay (TimeOnly?). Zero rows = applies everywhere/always; multiple rows needed to express different windows per theater.

### LoyaltySetting (points conversion rate)
TheaterId (Guid?, **unique when not null** — one global default row with TheaterId=null plus optional per-theater overrides), PointsPerUnit (int, VND spent per 1 point earned), PointValueVnd (int, VND value of 1 point redeemed).

### GiftCard
Code (50, **unique**), InitialBalance (double), Balance (double, drawn down/restored), IsActive, ExpiresAt (DateTime?), IssuedToEmail (string?, optional, not required to redeem). No FKs — invoices reference it loosely via `Invoice.GiftCardId` (no FK constraint, see Invoice).

## 5. Booking / Invoicing / Payments / Refunds

### Invoice (the order/booking header; "Id" redeclared via `new`)
| Field | Notes |
|---|---|
| Code (50) | **unique** |
| UserId (Guid?) | Restrict; null = walk-in counter sale (see SoldByUserId) |
| TotalAmount, DiscountAmount (default 0), FinalAmount | double |
| Status (InvoiceStatus) | Pending/Paid/Cancelled/Failed (value 4 "Refunded" reserved, never reused) |
| PaymentMethod?, PaymentReference? | |
| PaidAt (DateTime?) | |
| PointsRedeemed (int) | reserved at creation, restored on cancel/expire |
| GiftCardId (Guid?), GiftCardAmount (double) | **no FK** — amount restored to card on cancel/expire |
| DiscountId (Guid?) | FK -> Discount, SetNull |
| PromotionProgramId (Guid?) | FK -> PromotionProgram, SetNull; mutually exclusive with DiscountId as the pricing source |
| TheaterId (Guid?) | FK (shadow, Restrict); null only on rows predating the column |
| Channel (SalesChannel) | Online/Counter |
| SoldByUserId (Guid?) | staff who rang up a counter sale, no FK |
| CashDrawerSessionId (Guid?) | no FK |
| FoodStatus (FoodOrderStatus) | pickup state of food lines, default None |
| FoodHandedOverAt/FoodHandedOverByUserId | |
| ExchangedFromInvoiceId (Guid?) | the invoice this one replaced in a counter exchange, no FK |
Index: (TheaterId, PaidAt). Nav `InvoiceTickets`, `InvoiceFoodAndDrinks`, `Payments`.

**Refund flow note**: there is no dedicated Refund entity. `InvoiceStatus.Refunded` (was value 4) and `AuditAction.Refund` (was value 4) are both explicitly **reserved/retired** ("do not reuse") — refund handling appears to have been collapsed into the Cancelled status + audit trail (`AuditAction.Compensation`, `CashPayOut`) rather than a first-class Refund row. Treat any refund logic as working through `InvoiceStatus.Cancelled` + `InvoicePayment`/`AuditLog` rows, not a separate table.

### InvoiceTicket (one sold seat; no own Id — composite key)
PK (InvoiceId, ShowTimeId, RoomId, SeatId). Price (double), PatronCategoryId (Guid?, **no FK**, snapshot), PatronCategoryName (100, snapshot), PatronDiscountPercent (double), QrCode?, IsUsed (bool, default false) + UsedAt (DateTime?) + UsedByUserId (Guid?, no FK) set atomically at gate admission, IsActive (bool, default true — false once cancelled/expired), SeatLabel (maxlen 16, **frozen at sale time**, never recomputed even if seat grid changes later; ops/gate views intentionally use the seat's live RowName+ColIndex instead).
**Key DB invariant**: unique filtered index on (ShowTimeId, RoomId, SeatId) `WHERE IsActive = 1` — the sole DB-level guarantee against double-booking a seat across server instances (see seat-locking below).
Relationships: Invoice (Cascade), ShowTimeRoom (via composite FK ShowTimeId+RoomId, Restrict), Seat (Restrict).

### InvoiceFoodAndDrink (join, no own Id)
PK (InvoiceId, FoodAndDrinkId). Quantity (int), UnitPrice/TotalPrice (double). Invoice (Cascade), FoodAndDrink (Restrict).

### InvoicePayment (one tender line on an invoice)
InvoiceId (Cascade), Method (PaymentTender enum), Amount (double, what this tender contributed — for cash, the amount due not handed over), TenderedAmount/ChangeAmount (double?, cash only), Reference (100?, card/QR/gift-card code/gateway ref).

## 6. Concession / Inventory

### FoodAndDrink
TheaterId (Cascade — per-theater catalog), Name (200), Price (double), ImageUrl?, Description?, IsAvailable (bool, default true), TrackInventory (bool, opt-in stock tracking), QuantityOnHand (int, **check constraint >= 0**), LowStockThreshold (int), TargetStockLevel (int, 0=unset), IsCombo (bool — combos hold no stock themselves, deduct components instead). Nav `InvoiceFoodAndDrinks`, `ComboItems`.

### ComboItem (combo component line)
ComboId (Cascade, FK -> FoodAndDrink as "Combo"), ComponentId (NoAction, FK -> FoodAndDrink as "Component"), Quantity (int, default 1). Unique (ComboId, ComponentId). Check constraints: `Quantity > 0`, `ComboId <> ComponentId`.

### StockMovement (insert-only ledger)
FoodAndDrinkId (NoAction), TheaterId (denormalized, no own FK object), Type (StockMovementType), Quantity (int, **signed**: + adds, − removes), ReasonCode (StockReasonCode?, set for Adjust/Waste only), Reason (500)?, InvoiceId (Guid?, no FK — survives invoice deletion), StoragePlanId (Guid?), UserId (Guid?, null = system/expiry reaper). `FoodAndDrink.QuantityOnHand` is only ever changed together with a StockMovement row (app-level invariant, not DB-enforced).

## 7. Staff / Operations (NOT removed — still fully wired)

Despite a past "remove staff features/roles" effort mentioned in project history, **StaffShift, Incident, and workforce-related code are still present and wired** into `CinemaController` (`IIncidentManager`, `IWorkforceManager`, `IStaffReportManager`, `IStaffNotificationService` all exist and are injected; endpoints like `ReportIncident`, `GetIncidents`, `ResolveIncident`, shift save/delete are live). Treat these as active, not legacy/dead code.

### StaffShift
TheaterId, UserId (no explicit FK objects configured — plain Guid columns with indexes only), StartTime/EndTime (DateTime, theater's local wall-clock time, like showtimes), Note (500)?. Index (TheaterId, StartTime) and (UserId, StartTime).

### Incident (no FKs by design, like AuditLog — survives deletion of things it references)
TheaterId (Guid, plain), RoomId/SeatId/ShowTimeId (Guid?, plain), Category (IncidentCategory), Severity (IncidentSeverity, default Medium), Status (IncidentStatus, default Open), Title (200), Description (2000)?, ReportedByUserId (Guid), ResolvedByUserId (Guid?), ResolvedAt (DateTime?), ResolutionNote (1000)?, BlocksSeat (bool — true while this incident pulled the seat+its double partner out of sale), BlocksRoom (bool — true while it put the room into maintenance). Index (TheaterId, Status, CreationTime).

## 8. Audit / Notifications / Misc

### AuditLog (insert-only, no FKs on purpose)
TheaterId (Guid?), ActorUserId (Guid), ApproverUserId (Guid?, the manager who authorised — equals actor when actor is already an approver), Action (AuditAction), EntityType (100, required), EntityId (Guid?), Amount (double?), ReasonCode (StaffReasonCode?), Reason (500)?, DataJson (string?, free-form JSON before/after snapshot). Index (TheaterId, CreationTime), (ActorUserId, CreationTime).

### ReminderLog
UserId, ShowTimeId, SentAt (DateTime). **Unique (UserId, ShowTimeId)** — dedup so the reminder job doesn't resend after a process restart (this table replaces an in-memory dedup set).

### Template (email/SMS notification templates)
Code (100, **unique**, immutable lookup key used by sending code e.g. BookingManager), Name (200), Channel (TemplateChannel: Email/Sms), Subject (500?, email only), Body (required), IsActive. Resolved via `{{Token}}` placeholders by TemplateRenderer.

### News
Title, Content, ThumbnailUrl?, Author?, IsPublished (default false), PublishedAt (DateTime?). No explicit config (EF conventions only).

## 9. Enums

| Enum | Values | Notes |
|---|---|---|
| `AuditAction` | Other=0, OverrideFailed=1, OverridePinChanged=2, PriceOverride=3, (4 reserved, was Refund), Exchange=5, Reprint=6, VoidSale=7, CashPayOut=8, DrawerReconcile=9, Compensation=10, PointsAdjust=11, ResendTicket=12, BlockSeat=13, BlockRoom=14, TicketAdmitOverride=15, (16 reserved, was UserTheatersChanged), GateScan=17 | Append-only; **never renumber**; gaps are intentional |
| `CreditType` | Director=1, Actor=2 | role on a MovieCredit |
| `FoodOrderStatus` | None=0, Pending=1, Preparing=2, Ready=3, HandedOver=4, Cancelled=5 | allowed moves: Pending->Preparing->Ready->HandedOver, or Ready->HandedOver directly |
| `IncidentCategory` | Other=0, Seat=1, Room=2, Projection=3, Sound=4, Safety=5, Customer=6, Cleanliness=7 | |
| `IncidentSeverity` | Low=0, Medium=1, High=2, Critical=3 | |
| `IncidentStatus` | Open=0, Resolved=1 | |
| `InvoiceStatus` | Pending=0, Paid=1, Cancelled=2, Failed=3 | (4 reserved, was Refunded) |
| `PaymentTender` | Cash=0, Card=1, QrWallet=2, GiftCard=3, Points=4, Online=5 | |
| `ProjectionForm` | TwoD=1, ThreeD=2 | property of screening, not room |
| `PromotionEligibility` | Everyone=0, Members=1, StudentOrTeacher=2, AgeLimit=3 | only Everyone/Members are safe to AutoApply (system can't verify the other two online) |
| `PromotionMechanic` | Info=0, FixedPrice=1, PercentOff=2 | Info never affects price |
| `RoomStatus` | Active=0, Maintenance=1, Inactive=2 | |
| `SalesChannel` | Online=0, Counter=1 | |
| `SalesGroupBy` | Day=0, Movie=1, Theater=2, PaymentMethod=3, Staff=4, Channel=5 | staff report API contract; never renumber |
| `ScanOutcome` | Admitted=0, NotFound=1, NotPaid=2, AlreadyUsed=3, WrongTheater=4, WrongShowTime=5, TooEarly=6, Expired=7, AgeCheckRequired=8 | every value != Admitted means deny entry; AgeCheckRequired does NOT mark the ticket used |
| `SeatKind` | Standard=0, Double=1 | note: namespace is `Cinema.Data.Entities`, not `...Enums` (inconsistent with sibling enums) |
| `SeatStatus` | Available=0, Reserved=1, Occupied=2 | |
| `ShowTimeType` | Normal=0, Premiere=1, Special=2 | |
| `StaffReasonCode` | Other=0, CustomerRequest=1, WrongShowtime=2, DuplicateSale=3, ServiceFailure=4, TechnicalIssue=5, PriceMatch=6, Compensation=7 | |
| `StockMovementType` | Receive=0, Sale=1, SaleReversal=2, Adjust=3, Waste=4 | direction carried by signed Quantity |
| `StockReasonCode` | Other=0, Expired=1, Damaged=2, Spilled=3, TheftOrLoss=4, StockCountCorrection=5, OpeningBalance=6 | |
| `TemplateChannel` | Email=1, Sms=2 | |
| `UserStatus` | Active=0, Inactive=1, Banned=2 | |

## 10. Key invariants

- **datetime vs datetime2**: `CinemaContext.OnModelCreating` globally remaps every `DateTime`/`DateTime?` property to SQL `datetime` (EF's default would be `datetime2`). New entities pick this up automatically — do not override unless intentional.
- **Seat locking is in-process, not distributed.** `BookingManager` holds `static ConcurrentDictionary<string, (ConnectionId, LockedAt)> _lockedSeats` plus `static ConcurrentDictionary<string, SemaphoreSlim> _bookingGates` — this state does NOT survive a restart and is NOT shared across multiple API instances/pods. The only cross-instance guarantee against double-booking is the **DB-level filtered unique index** on `InvoiceTicket (ShowTimeId, RoomId, SeatId) WHERE IsActive = 1`. SignalR `BookingHub` (`/hubs/booking`) broadcasts lock/unlock events for UI responsiveness, but the real correctness guard is that unique index plus the final transactional insert.
- **Append-only enums, never renumber**: `AuditAction`, `FoodOrderStatus`, `IncidentCategory/Severity/Status`, `InvoiceStatus`, `PaymentTender`, `SalesChannel`, `SalesGroupBy`, `ScanOutcome`, `StockMovementType/ReasonCode` are all explicitly documented as "stored as int; never renumber" or "never renumber a member" — some (`AuditAction`, `InvoiceStatus`) have **explicit reserved/retired gaps** (do not reuse value 4 in either).
- **No Refund entity.** Refunds are represented through `InvoiceStatus.Cancelled` plus `AuditLog`/`InvoicePayment` rows; the `Refunded` status and `Refund` audit action were removed and their numeric slots retired rather than reused.
- **Soft-delete-like / audit-snapshot pattern without a real soft-delete flag**: `AuditLog` and `Incident` intentionally carry **no FKs** to the entities they reference (TheaterId, RoomId, SeatId, UserId are plain Guid columns) specifically so the audit/incident trail survives deletion of the referenced row. Similarly `InvoiceTicket.PatronCategoryId/PatronCategoryName/PatronDiscountPercent` and `SeatLabel` are frozen snapshots at sale time — never recomputed from the live `PatronCategory`/`Seat`, even if later renamed/resized/deleted.
- **`InvoiceTicket.IsActive`** is the real "soft delete" for a booked seat (false on cancel/expire); the row itself is never physically removed, since it's part of the audit/ticket trail. The uniqueness guarantee is scoped to `WHERE IsActive = 1` precisely so a seat can be rebooked after its prior ticket goes inactive.
- **Composite-key joins with no own Id**: `InvoiceTicket` (4-part PK), `InvoiceFoodAndDrink` (2-part PK), `MovieTypeDetail` (2-part PK), `ShowTimeRoom` (2-part PK) all break the `BaseEntity.Id` convention — they are pure relational rows, not independently addressable entities.
- **Multi-cascade-path avoidance**: SQL Server rejects multiple cascade paths to the same table, so wherever two FKs on a join/child row could both cascade from entities that already cascade from a common ancestor (e.g. `DiscountTheater`, `PromotionProgramSchedule`, `RoomTypePatronCategoryPrice`), exactly one leg is `Restrict`/`NoAction` by design — don't "fix" these to Cascade.
- **Eligibility-by-omission pattern**: both `PatronCategory` (vs `SeatType`/`SeatKind`) and `RoomTypePatronCategoryPrice` (vs `PatronCategory`) encode "who/what can book" purely by the *presence* of a pricing row — there is no separate allow-list/eligibility table anywhere in pricing.
- **VNPay is callback-authoritative** (see root CLAUDE.md) — `Invoice.Status` only flips to `Paid` from the signed IPN callback, never from the browser redirect; this is a business-flow invariant that rides on top of the `InvoiceStatus` enum rather than being visible in the schema itself.
- **Seat kind has exactly one source of truth**: `Seat.SeatGroupId` (non-null = part of a double-seat pair). There is deliberately no `SeatTypeId` on `Seat` itself, to avoid two places disagreeing about whether a seat is a double.
- **`SeatKind` enum lives in `Cinema.Data.Entities` namespace**, not `Cinema.Data.Enums` like its siblings — a (likely historical) inconsistency to be aware of when adding a `using`.
- **Staff/workforce/incident code is live**, not vestigial — see section 7. Do not assume it was removed just because project history mentions a "remove staff features" effort.

## 11. Entity -> DTO mapping

| Entity | DTO(s) | Notable differences |
|---|---|---|
| Movie | `MovieDTO`, `MovieDetailDTO`, `CreateMovieRequest`, `UpdateMovieRequest` | DTO adds computed `AverageRating`, `RatingCount`, `IsNowShowing`, `IsComingSoon`, `Genres` (flattened from MovieTypeDetails), `AgeRestrictionCode` (denormalized); entity's free-text `Cast` field still present alongside the newer `MovieCredit` relational model |
| Movie (external) | `ExternalRatingsDTO` | not entity-backed at all — live OMDb lookup result (RottenTomatoes/ImdbRating/Metascore as display strings), matched using `Movie.ImdbId` when present else title+year |
| MovieCredit | `MovieCreditDTO`, `MovieCreditRequest` | DTO adds `PersonName`/`PhotoUrl` (denormalized from Person) |
| Person | `PersonDTO`, `CreatePersonRequest`, `UpdatePersonRequest` | 1:1 |
| Theater | `TheaterDTO`, `CreateTheaterRequest`, `UpdateTheaterRequest` | 1:1 |
| Room | `RoomDTO`, `CreateRoomRequest`, `UpdateRoomRequest`; seats via `RoomSeatDTO`/`SaveSeatMapRequest` | seat grid is edited through dedicated request DTOs (`ResizeSeatGridRequest`, `SeatAssignmentItem`, `RemoveSeatRequest`), not a raw Seat CRUD |
| RoomType | `RoomTypeDTO`, `CreateRoomTypeRequest`, `UpdateRoomTypeRequest` | 1:1 (no PatronCategoryPrices embedded — managed via `RoomTypePatronCategoryPriceDTO`/`SaveRoomTypePatronCategoryPricesRequest`) |
| PatronCategory | `PatronCategoryDTO`, `CreatePatronCategoryRequest`, `UpdatePatronCategoryRequest` | DTO adds `SeatTypeName`, and exposes `Kind` (SeatKind) directly even though the entity derives it indirectly via SeatType |
| RoomTypePatronCategoryPrice | `RoomTypePatronCategoryPriceDTO`, `SaveRoomTypePatronCategoryPriceItem`/`SaveRoomTypePatronCategoryPricesRequest` | batch-save request shape, not per-row CRUD |
| ShowTime + ShowTimeRoom | `ShowTimeDTO`, `ShowTimeListDTO`, `ShowTimeSummaryDTO`, `CreateShowTimeRequest`, `UpdateShowTimeRequest` | `ShowTimeDTO` flattens the single assigned Room (RoomId/RoomName/RoomTypeName/BasePrice/TurnoverBufferMinutes) onto the showtime — the entity models room assignment as a separate `ShowTimeRoom` join |
| Discount | `DiscountDTO`, `CreateDiscountRequest`, `UpdateDiscountRequest`, `DiscountCodeValidationDTO` | validation DTO is a derived runtime result (code valid? amount off?), not a stored shape |
| DiscountType | `DiscountTypeDTO`, Create/Update requests | 1:1 |
| PromotionProgram | `PromotionProgramDTO`, `CreatePromotionProgramRequest`, `UpdatePromotionProgramRequest` | DTO adds `MinMemberShipName`, `LinkedDiscountCode` (denormalized, shown only if linked discount active+has code) |
| PromotionProgramPrice/Schedule | `PromotionProgramPriceDTO`, `PromotionProgramScheduleDTO` | embedded as lists inside PromotionProgramDTO, not separately paged |
| GiftCard | `GiftCardDTO` (admin view), `GiftCardValidationDTO` (runtime check), `IssueGiftCardRequest` | GiftCardDTO omits nothing sensitive (no secret beyond Code itself) |
| LoyaltySetting | `LoyaltySettingDTO`, `SaveLoyaltySettingRequest`, `EffectiveLoyaltyRateDTO` | `EffectiveLoyaltyRateDTO` is a computed "which rate applies to this theater" result, not a stored row |
| Invoice | `InvoiceDTO` (customer-facing, nested tickets/foods), `InvoiceAdminDTO` (admin list/report view) | `InvoiceDTO` denormalizes `UserName`/`UserEmail`; neither DTO currently surfaces GiftCardAmount/PointsRedeemed/Channel/SoldByUserId directly on the base InvoiceDTO — check `InvoiceAdminDTO` for the fuller admin shape before assuming parity |
| InvoiceTicket | `InvoiceTicketDTO`, `TicketItemDTO`, `TicketValidationDTO`, `GateLookupTicketDTO` | several ticket-shaped DTOs exist per call site (booking result vs gate scan vs admin) rather than one shared shape |
| InvoiceFoodAndDrink | `InvoiceFoodDTO`, `BookingFoodItem`, `CounterFoodItem` | booking-time request item vs stored/display shapes differ |
| InvoicePayment | surfaced inline inside `InvoiceAdminDTO`/`CounterSaleResultDTO`/`TenderLine` rather than its own top-level DTO | |
| FoodAndDrink | `FoodAndDrinkDTO`, `CreateFoodAndDrinkRequest`, `UpdateFoodAndDrinkRequest`, `LowStockItemDTO` | combo composition surfaced via `ComboComponentDTO`/`ComboComponentItem`/`SaveComboRequest`, not nested raw `ComboItem` rows |
| StockMovement | no direct list DTO found under Catalog/Concession beyond `LowStockItemDTO`; stock changes driven through request DTOs (`GetLowStockRequest`, `SetFoodStatusRequest`) rather than a ledger-read DTO | verify before building a stock-history UI — may need a new DTO |
| StaffShift | `StaffShiftDTO`, `SaveStaffShiftRequest`, `DeleteStaffShiftRequest`, `MyShiftsRequest`, `RosterRequest` | DTO adds denormalized `UserName` |
| Incident | `IncidentDTO`, `ReportIncidentRequest`, `ResolveIncidentRequest`, `GetIncidentRequest` | DTO adds denormalized `RoomName`, `SeatLabel`, `ReportedByName`, `ResolvedByName` |
| AuditLog | `AuditLogDTO`, `AuditEntry` | |
| User | `UserDTO`, `CreateUserRequest`, `UpdateUserRequest`, `UpdateProfileRequest`, auth-flow DTOs under `Auth/` | password/hash/salt/2FA/lockout/override-PIN internals never leave `Auth`-prefixed request DTOs; never exposed on `UserDTO` |
| MemberShip | `MemberShipDTO`, `CreateMemberShipRequest`, `UpdateMemberShipRequest` | 1:1 |
| Comment | `CommentDTO`, `CommentModerationDTO` | moderation DTO adds the approve/reject action shape |
| Theater staff scope | `TheaterStaffDTO`, `GetTheaterStaffRequest` | staff list is theater-scoped, not a generic UserDTO list |
