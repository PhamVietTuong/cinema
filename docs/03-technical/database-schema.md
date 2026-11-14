# Database Schema

> Purpose: Every EF Core entity/table — fields, types, constraints, relationships, indexes, and business meaning of non-obvious fields.
> Sources: `cinemabe/Cinema/3-Data/Cinema.Data.Entities/Entities/*.cs`, `.../Enums/*.cs`, `cinemabe/Cinema/3-Data/Cinema.Data/Configurations/*.cs`, `cinemabe/Cinema/3-Data/Cinema.Data/Contexts/CinemaContext.cs`, `cinemabe/Cinema/3-Data/Cinema.Data.Entities/Entities/AuditLog.cs`
> Last reviewed: 2026-11-14

## EF Core quirk — `datetime`, not `datetime2`

`CinemaContext.OnModelCreating` (after `ApplyConfigurationsFromAssembly`) iterates every entity property and forces every `DateTime`/`DateTime?` column to SQL `datetime` instead of EF Core's default `datetime2`:

```csharp
foreach (var entity in mb.Model.GetEntityTypes())
    foreach (var prop in entity.GetProperties())
        if (prop.ClrType == typeof(DateTime) || prop.ClrType == typeof(DateTime?))
            prop.SetColumnType("datetime");
```

This applies automatically to new entities — do not override unless intentional. All `DateTime` columns listed below are `datetime`, not `datetime2`.

Every entity inherits `BaseEntity`: `Id` (`Guid`, client-generated, not DB-identity), `CreationTime` (`DateTime`), `LastUpdatedTime` (`DateTime?`). `Invoice` and `User` redeclare `Id` with `new Guid Id` — same type/semantics, just hides the base property.

Schema changes are hand-written into `cinemabe/sql/create_db.sql` / `upgrade_db.sql` (see root `CLAUDE.md`), not generated via `dotnet ef migrations add` — the Fluent `Configurations/` below still describe the model EF expects, kept in sync by hand.

**Destructive one-shot upgrade (2026-11-14, child ticket removal).** The "Remove the child ticket category" block of `upgrade_db.sql` permanently deletes every invoice that has a ticket sold under the child patron category (exact `PatronCategory.Name` / ticket `PatronCategoryName`, matched by name or `PatronCategoryId`) together with its tickets, payments, food lines and `StockMovement` rows, then that category's `RoomTypePatronCategoryPrice` rows and the category itself, in one transaction. Redeemed points of purged Pending invoices go back to their users, and surviving invoices whose `ExchangedFromInvoiceId` pointed at a purged one get it set to NULL. It is one-shot, not merely idempotent: it records the database extended property `purge_child_ticket_done` in the same transaction and skips itself when that property exists, so a later category with the same name is never purged. Not reversed: points earned on purged Paid invoices, `PromotionProgram.UsedCount`, food stock. Run it with the API stopped (no payment in flight), and never on a non-dev database without the owner's confirmation and a verified backup.

**Destructive upgrade (2026-11-14, discount simplification).** The "Discount simplification" block of `upgrade_db.sql` permanently deletes every invoice that used a gift card (`Invoice.GiftCardAmount > 0`, or an `InvoicePayment` with `Method = 3`) together with its tickets, payments, food lines and `StockMovement` rows, in one transaction, and gives the redeemed points of purged Pending invoices back to their users. It then drops `Invoice.GiftCardId/GiftCardAmount`, the `GiftCard` table, `PromotionProgram.StacksWithMembershipDiscount` and `MemberShip.DiscountPercent`. Not reversed: loyalty points earned on purged Paid invoices, `PromotionProgram.UsedCount`, food stock, and historical revenue, which drops by the purged invoices. Apply it to the local dev database only; any other environment needs the owner's fresh confirmation and a verified backup first.

## 1. Identity / Users

### User
| Field | Type | Constraints / notes |
|---|---|---|
| Id | Guid | PK |
| Phone | string | required, max 20, **unique** |
| Email | string | required, max 200, **unique** |
| Name | string | required, max 200 |
| Avatar | string? | |
| PasswordHash, PasswordSalt | byte[] | required |
| Status | UserStatus | Active=0/Inactive=1/Banned=2 |
| UserTypeId | Guid | FK → UserType, `Restrict` |
| TheaterId | Guid? | FK → Theater (shadow, no inverse nav), `SetNull`; set only for `TheaterStaff` |
| MemberShipId | Guid? | FK → MemberShip, `SetNull` |
| Points | int | loyalty balance, default 0 |
| NotifyBookingEmails / NotifyReminderEmails | bool | default true (opt-out) |
| NotifyPromotionEmails | bool | default **false** (opt-in); column default constraint `DF_User_NotifyPromotionEmails` = 0; users created before 2026-11-14 keep their stored value |
| PasswordResetTokenHash | string? | SHA-256 of emailed token |
| PasswordResetExpiresAt | DateTime? | |
| FailedLoginCount | int | default 0 |
| LockoutEndUtc | DateTime? | account lockout until |
| EmailConfirmed | bool | default false |
| EmailVerificationTokenHash / EmailVerificationExpiresAt | string? / DateTime? | |
| TwoFactorEnabled | bool | default false |
| TwoFactorCodeHash / TwoFactorCodeExpiresAt | string? / DateTime? | emailed OTP |

Relationships: `UserType` (Restrict), `MemberShip` (SetNull), `Theater` (SetNull). Nav collections: `Invoices`, `Comments`, `Evaluations`.

### UserType
| Field | Type |
|---|---|
| Name | string |

Nav: `Users` (1:many).

### MemberShip (loyalty tier)
| Field | Type | Notes |
|---|---|---|
| Name | string | |
| MinPoints, MaxPoints | int | points range of the tier; tiers carry no percentage discount (`DiscountPercent` removed 2026-11-14) |

Nav: `Users`. Also referenced by `PromotionProgram.MinMemberShipId` as a minimum-tier gate.

## 2. Catalog — Movies, Cast/Crew, Theaters, Rooms, Seats, Showtimes

### Movie
| Field | Type | Notes |
|---|---|---|
| Title | string | max 300 |
| Slug | string | max 320, **unique** (`IX_Movie_Slug`) |
| Description | string | max 2000 |
| Duration | int | minutes |
| ReleaseDate, EndDate | DateOnly | |
| PosterUrl, TrailerUrl | string? | |
| Director | string | max 200 |
| Cast | string | max 1000, legacy free-text; cast is also modeled relationally via `MovieCredit` |
| Language, Subtitle | string | |
| ImdbId | string? | max 20, used to match OMDb ratings; falls back to title+year |
| IsActive | bool | |
| AgeRestrictionId | Guid | FK → AgeRestriction, `Restrict` |

Relationships: `MovieTypeDetails` (genres join), `ShowTimes` (Cascade), `Comments` (Cascade), `Evaluations` (Cascade), `MovieCredits` (Cascade).

### AgeRestriction
`Code`, `Description`, `MinAge` (int). Nav `Movies` — `Restrict` (can't delete a restriction in use).

### Person (cast/crew)
`Name` (max 200, **unique**, `IX_Person_Name`), `PhotoUrl` (max 500).

### MovieCredit
| Field | Notes |
|---|---|
| MovieId | FK → Movie, Cascade |
| PersonId | FK → Person, Restrict |
| CreditType | enum (Director=1, Actor=2) |
| CharacterName | max 200, actor only |
| DisplayOrder | int |

**Unique** (MovieId, PersonId, CreditType) — one credit per role per movie.

### MovieType / MovieTypeDetail
`MovieType.Name` = genre label. `MovieTypeDetail` is a pure join (composite PK MovieId+MovieTypeId), default EF cascade via required FKs.

### Comment
| Field | Notes |
|---|---|
| MovieId | Cascade |
| UserId | Restrict |
| Content | max 2000 |
| ParentId | self-FK, Restrict — threaded via `Replies` nav |
| IsApproved | bool, default true |

### Evaluation (rating/review)
MovieId (Cascade), UserId (Restrict), Score (int), Review (max 1000). **Unique** (MovieId, UserId).

### Theater
Name (200), Address (500), City (100), Phone, Email, ImageUrl, IsActive, Latitude/Longitude (double?), MaxSeatsPerBooking (int, NOT NULL, default 10, valid 1-50 — most seats one **online** booking may hold at this theater; counter sales are not capped; added by the idempotent `COL_LENGTH` block in `upgrade_db.sql`), MaxPromotionDiscountPercent (int, NOT NULL, default 50, valid 0-50) and MaxHolidayPromotionDiscountPercent (int, NOT NULL, default 50, valid 0-100) — the most the promotion saving may take off, as a percent of the summed ticket list prices, on a normal day and on a showtime date with a `Holiday` row (own idempotent `COL_LENGTH` block in `upgrade_db.sql`). Nav `Rooms` (Cascade).

### Room
Name (100), TheaterId (Cascade), RoomTypeId (Restrict), TotalRows/TotalColumns (int), Status (RoomStatus: Active=0/Maintenance=1/Inactive=2). Nav `Seats` (Cascade), `ShowTimeRooms`.

### RoomType (per-theater commercial class: Standard/IMAX/4DX/etc.)
TheaterId (Cascade), Name (100), Description, SupportsThreeD (bool), ThreeDSurcharge (double), TurnoverBufferMinutes (int, default 0). Nav `Rooms`, `PatronCategoryPrices`.

### Seat
RoomId (Cascade), RowName (max 5), ColIndex (int), IsActive, Kind (`SeatKind`, int, default 0 — stores only `Standard` (0) or `Aisle` (2); an aisle is a non-bookable gap cell with `SeatGroupId` null and `IsActive` true; `Seat.cs:14`, `create_db.sql` `[Seat].[Kind]`), SeatGroupId (Guid?, **sole source of truth for Standard vs Double** — non-null links the two seats of a double/couple pair, so `Double` is never stored in `Kind`; read the effective kind via `SeatKinds.EffectiveKind` (`cinemabe/Cinema/2-Business/Cinema.Business/Helpers/SeatKinds.cs:17`); no SeatTypeId on Seat). **Unique** (RoomId, RowName, ColIndex). Index on SeatGroupId.

### SeatType (per-theater: exactly Single + Double)
TheaterId (Cascade), Kind (SeatKind enum — matched by Kind, never Name), Name (display only), Color (hex, default `#808080`). **Unique** (TheaterId, Kind). Nav `PatronCategories`.

### ShowTime
MovieId (Cascade), StartTime/EndTime (DateTime, **theater-local GMT+7 wall-clock time**, not UTC — compared against `DateTime.Now`, so the server process must run in GMT+7; the Dockerfile sets `TZ=Asia/Ho_Chi_Minh`), ProjectionForm (TwoD=1/ThreeD=2 — property of the screening, not the room), ShowTimeType (Normal=0/Premiere=1/Special=2), IsActive. Nav `ShowTimeRooms`.

### ShowTimeRoom (join: a showtime assigned to a room)
Composite PK (ShowTimeId, RoomId). BasePrice (int — flat per-showtime manual add-on, not the seat's base price). Nav `InvoiceTickets`.

## 3. Pricing

### PatronCategory (pricing + eligibility engine)
One row per (Theater, SeatKind-via-SeatType, category name). **Omitting a row for a seat kind IS the eligibility rule.**

| Field | Notes |
|---|---|
| TheaterId | Cascade |
| SeatTypeId | FK → SeatType, Restrict (determines SeatKind) |
| Name (100), Description | |
| Price | double, absolute VND — independent per row |
| IsActive | |
| RequiresIdCheck | bool, default false. Counter-only category: `BookingManager.ComposeInvoiceAsync` rejects it for online bookings ("requires an ID check and can only be bought at the counter"), CinemaUser hides it, counter sales allow it |
| IdCheckMessage | max 1000 |

**Unique** (TheaterId, Name, SeatTypeId). Nav `RoomTypePrices`.

### RoomTypePatronCategoryPrice (per-RoomType override/allow-list)
RoomTypeId (Restrict), PatronCategoryId (Cascade), Price (double). **Unique** (RoomTypeId, PatronCategoryId). Zero rows = nothing bookable for that RoomType.

### TicketPrice (time-of-day/day-of-week multiplier)
(TheaterId Cascade, RoomTypeId Restrict, TimeSlotId Restrict, DayOfWeek) → PriceMultiplier (double, default 1). **Unique** on the 4-tuple.

### TimeSlot
TheaterId (Cascade), Name, StartTime/EndTime (string "HH:mm"). Nav `TicketPrices`.

### Holiday
Name, Date (DateOnly), PriceMultiplier (double, default 1.5). No TheaterId — global.

## 4. Promotions / Loyalty

### PromotionProgram (single promotion/pricing entity — campaign-card display + code/auto-apply pricing)
Formerly two separate entities (this one and the flat-percent `Discount`/`DiscountType`/`DiscountTheater`,
now removed); merged so there is one pricing path and one admin screen. A program is triggered by
exactly one of `Code` (customer-entered) **xor** `AutoApply=true`, enforced in
`PromotionProgramManager.ValidateAsync`.

| Field | Notes |
|---|---|
| Title (200), Summary (500)?, BannerUrl? | |
| CampaignId? | FK → PromotionCampaign, SetNull — optional customer-facing grouping/filter; replaces the old fixed 3-value `Category` enum (Promotion/Seasonal/Membership), which in practice was nearly always "Promotion" and served no real grouping purpose |
| Mechanic | enum: Info=0/FixedPrice=1/PercentOff=2 |
| PercentOff? | double, required iff Mechanic=PercentOff, (0,100] |
| Eligibility | enum: Everyone=0/Members=1/StudentOrTeacher=2/AgeLimit=3 |
| MinMemberShipId? | FK → MemberShip, **Restrict**, required iff Eligibility=Members |
| MaxAge? | int, required iff Eligibility=AgeLimit |
| StartDate/EndDate | DateTime |
| ExcludeHolidays | bool |
| AllowedChannelsMask? | int bitmask of SalesChannel; null = all |
| MaxTicketsPerTransaction? | int |
| TermsText (2000)?, DisplayOrder, IsPublished | |
| AutoApply | bool; requires Mechanic != Info, Eligibility in (Everyone, Members), Code null |
| Code? | string, max 50, unique when present (filtered unique index `WHERE [Code] IS NOT NULL`) — mutually exclusive with AutoApply |
| MaxUsage? / UsedCount | int, UsedCount default 0 — incremented on every paid invoice whose `PromotionProgramId` points here, regardless of trigger (code or auto-apply) |
| MaxDiscountAmount? | double — caps the *summed* per-unit saving (post-`ComputeProgramSaving`, after the program is resolved), migrated from `Discount.MaxDiscountAmount` |
| MovieId? | FK → Movie, **SetNull** — optional movie scope; migrated from `Discount.MovieId`. Theater/day/time scope is expressed via `Schedules` instead (no separate fields here) |

Index: (IsPublished, EndDate), MinMemberShipId, Code (filtered unique), MovieId. Nav `Prices`, `Schedules`.

`Invoice.DiscountId` is vestigial: the column stays (historical data only, no FK, never written by new
code) so old invoices keep their value; `Invoice.PromotionProgramId` is the only pricing-source column
new code writes.

### PromotionCampaign (admin-managed named grouping, e.g. "C'Student", "Happy Hour")
Name (100, unique), referenced by `PromotionProgram.CampaignId`. Managed via its own CRUD screen (Catalog > Promotion Campaigns) rather than being a fixed enum, so new campaign families can be added without a code change. Drives the dynamically-built filter tabs on the customer Promotions page.

### PromotionProgramPrice
PromotionProgramId (Cascade), ProjectionForm?/SeatKind? (null = "any"). Price (double, all-in). **Unique** (PromotionProgramId, ProjectionForm, SeatKind).

### PromotionProgramSchedule
PromotionProgramId (Cascade), TheaterId (Guid?, **Restrict**, null = all theaters), DaysOfWeekMask?, StartTimeOfDay/EndTimeOfDay?. Zero rows = applies everywhere/always.

### SplashBanner
Title, ImageUrl (required), LinkUrl (optional), StartDate/EndDate (DateTime, date-only, inclusive), IsPublished (bool). No FKs, no nav. "Active" is computed against Vietnam local date, not stored.

### LoyaltySetting
TheaterId (Guid, required, **unique** — one row per theater; no global row), PointsPerUnit (int, VND spent per 1 point earned), PointValueVnd (int, VND value of 1 point redeemed). A theater with no row uses the built-in default 1000 / 10 (1,000 VND = 1 point; 100 points = 1,000 VND).

## 5. Booking / Invoicing / Payments

### Invoice (order/booking header)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | redeclared `new` |
| Code | string | max 50, **unique** |
| UserId | Guid? | FK → User, Restrict; null = walk-in counter sale |
| TotalAmount, DiscountAmount, FinalAmount | double | column type `float`; DiscountAmount default 0 |
| DiscountId | Guid? | **Vestigial** — the `Discount` entity/table is gone; no FK, historical data only, never written by new code |
| PromotionProgramId | Guid? | FK → PromotionProgram, SetNull; the pricing source for any invoice that used a program (code or auto-apply) |
| Status | InvoiceStatus | Pending=0/Paid=1/Cancelled=2/Failed=3 (4 reserved, "Refunded", never reused) |
| PaymentMethod?, PaymentReference? | string | |
| PaidAt | DateTime? | |
| PointsRedeemed | int | reserved at creation, restored on cancel/expire |
| TheaterId | Guid? | FK (shadow), Restrict; null only on rows predating the column |
| Channel | SalesChannel | Online=0/Counter=1 |
| SoldByUserId | Guid? | no FK |
| CashDrawerSessionId | Guid? | no FK. Vestigial — cash drawer close/daily close UI was removed from CinemaStaff (see `features.md`), same kind of leftover as `FoodStatus` below. |
| FoodStatus | FoodOrderStatus | None=0/Pending=1/Preparing=2/Ready=3/HandedOver=4/Cancelled=5. Vestigial beyond Pending/HandedOver since the kitchen pickup queue feature was removed — a counter sale now moves it straight to HandedOver; Preparing/Ready/Cancelled are never set by any remaining code path. |
| FoodHandedOverAt / FoodHandedOverByUserId | | Stamped on the counter-sale HandedOver transition only. |
| ExchangedFromInvoiceId | Guid? | no FK |

Index: (TheaterId, PaidAt). Nav `InvoiceTickets`, `InvoiceFoodAndDrinks`, `Payments`.

**No Refund entity exists.** `InvoiceStatus` value 4 and `AuditAction.Refund` (value 4) are reserved/retired — a refund system (customer/staff-initiated, gateway-side refund calls, `Refunded`/`PartiallyRefunded` invoice states) was built across several commits and then fully reverted (`9660958 feat(cinemabe): remove online refund feature`, `f0e4fe7 feat(cinemafe): remove refund UI, shared enum labels and error-catalog entries`). No `Refund` symbol exists anywhere in `cinemabe` today; a cancelled/expired invoice only reverses points/food-stock via `InvoiceStatus.Cancelled`, never a dedicated refund row. If refund work is requested again it needs to be designed fresh — see `01-business/business-rules.md` and `02-product/roadmap.md`.

### InvoiceTicket (one sold seat; no own Id)
PK: composite (InvoiceId, ShowTimeId, RoomId, SeatId). Price (double), PatronCategoryId (Guid?, **no FK**, snapshot), PatronCategoryName (max 100, snapshot), PatronDiscountPercent (double), QrCode?, IsUsed (bool, default false) + UsedAt (DateTime?) + UsedByUserId (Guid?, no FK), IsActive (bool, default true), SeatLabel (max 16, frozen at sale time, never recomputed).

**Key invariant**: filtered unique index on (ShowTimeId, RoomId, SeatId) `WHERE IsActive = 1` — the sole DB-level guard against double-booking across instances. Relationships: Invoice (Cascade), ShowTimeRoom (composite FK, Restrict), Seat (Restrict).

### InvoiceFoodAndDrink (join, no own Id)
PK (InvoiceId, FoodAndDrinkId). Quantity (int), UnitPrice/TotalPrice (double). Invoice (Cascade), FoodAndDrink (Restrict).

### InvoicePayment (one tender line)
InvoiceId (Cascade), Method (PaymentTender: Cash=0/Card=1/QrWallet=2/Points=4/Online=5; 3 was GiftCard, retired, never reuse), Amount (double), TenderedAmount/ChangeAmount (double?, cash only), Reference (string?, max 100).

## 6. Catalog / Inventory

### FoodAndDrink
TheaterId (Cascade), Name (200), Price (double), ImageUrl?, Description?, IsAvailable (bool, default true), TrackInventory (bool), QuantityOnHand (int, **check constraint `>= 0`**), LowStockThreshold (int), TargetStockLevel (int, 0=unset), IsCombo (bool). `LowStockThreshold`/`TargetStockLevel` are vestigial since the kitchen pickup queue / low-stock alert feature was removed — no endpoint reads them today. Nav `InvoiceFoodAndDrinks`, `ComboItems`.

### ComboItem
ComboId (Cascade, FK → FoodAndDrink "Combo"), ComponentId (NoAction, FK → FoodAndDrink "Component"), Quantity (int, default 1). **Unique** (ComboId, ComponentId). Check constraints: `Quantity > 0`, `ComboId <> ComponentId`.

### StockMovement (insert-only ledger)
FoodAndDrinkId (NoAction), TheaterId (denormalized, no FK object), Type (StockMovementType: Receive=0/Sale=1/SaleReversal=2/Adjust=3/Waste=4), Quantity (int, **signed**), ReasonCode (StockReasonCode?, Adjust/Waste only), Reason (500)?, InvoiceId (Guid?, no FK), StoragePlanId (Guid?), UserId (Guid?, null = system/expiry reaper).

## 7. Staff / Operations

### Incident
TheaterId (Guid, plain), RoomId/SeatId/ShowTimeId (Guid?, plain), Category (IncidentCategory), Severity (IncidentSeverity, default Medium), Status (IncidentStatus, default Open), Title (200), Description (2000)?, ReportedByUserId (Guid), ResolvedByUserId (Guid?), ResolvedAt (DateTime?), ResolutionNote (1000)?, BlocksSeat (bool), BlocksRoom (bool). Index (TheaterId, Status, CreationTime). No FKs by design (survives deletion of referenced rows).

## 8. Audit / Notifications / Misc

### AuditLog (insert-only, no FKs by design)
TheaterId (Guid?), ActorUserId (Guid), ApproverUserId (Guid?), Action (AuditAction), EntityType (100, required), EntityId (Guid?), Amount (double?), ReasonCode (StaffReasonCode?), Reason (500)?, DataJson (string?). Index (TheaterId, CreationTime), (ActorUserId, CreationTime).

### ReminderLog
UserId, ShowTimeId, SentAt (DateTime). **Unique** (UserId, ShowTimeId) — dedup across process restarts.

### Template (email/SMS)
Code (100, **unique**, immutable), Name (200), Channel (TemplateChannel: Email=1/Sms=2), Subject (500?, email only), Body (required), IsActive. Resolved via `{{Token}}` placeholders.

### News
Title, Content, ThumbnailUrl?, Author?, IsPublished (default false), PublishedAt (DateTime?). EF conventions only, no explicit `Configurations/` file.

## 9. Enums reference

All enums below are stored as `int`. Several are explicitly append-only with reserved/retired gaps that must never be renumbered or reused.

| Enum | Values | Notes |
|---|---|---|
| `AuditAction` | Other=0, OverrideFailed=1, OverridePinChanged=2, PriceOverride=3, (4 reserved, was Refund), Exchange=5, Reprint=6, VoidSale=7, CashPayOut=8, DrawerReconcile=9, Compensation=10, PointsAdjust=11, ResendTicket=12, BlockSeat=13, BlockRoom=14, TicketAdmitOverride=15, (16 reserved, was UserTheatersChanged), GateScan=17, ConsentChanged=18 (DataJson: Channel, Value, Source), AgeConfirmed=19 (DataJson: RatingCode, MinAge, InvoiceId) | Append-only; never renumber; gaps are intentional |
| `CreditType` | Director=1, Actor=2 | role on a MovieCredit |
| `FoodOrderStatus` | None=0, Pending=1, Preparing=2, Ready=3, HandedOver=4, Cancelled=5 | allowed moves: Pending→Preparing→Ready→HandedOver, or Ready→HandedOver directly |
| `IncidentCategory` | Other=0, Seat=1, Room=2, Projection=3, Sound=4, Safety=5, Customer=6, Cleanliness=7 | |
| `IncidentSeverity` | Low=0, Medium=1, High=2, Critical=3 | |
| `IncidentStatus` | Open=0, Resolved=1 | |
| `InvoiceStatus` | Pending=0, Paid=1, Cancelled=2, Failed=3 | (4 reserved, was Refunded) |
| `PaymentTender` | Cash=0, Card=1, QrWallet=2, Points=4, Online=5 | never renumber; 3 (was GiftCard) is retired and must not be reused |
| `ProjectionForm` | TwoD=1, ThreeD=2 | property of screening, not room |
| `PromotionEligibility` | Everyone=0, Members=1, StudentOrTeacher=2, AgeLimit=3 | only Everyone/Members are safe to AutoApply (system can't verify the other two online) |
| `PromotionMechanic` | Info=0, FixedPrice=1, PercentOff=2 | Info never affects price |
| `RoomStatus` | Active=0, Maintenance=1, Inactive=2 | |
| `SalesChannel` | Online=0, Counter=1 | |
| `SalesGroupBy` | Day=0, Movie=1, Theater=2, PaymentMethod=3, Staff=4, Channel=5 | staff report API contract; never renumber |
| `ScanOutcome` | Admitted=0, NotFound=1, NotPaid=2, AlreadyUsed=3, WrongTheater=4, WrongShowTime=5, TooEarly=6, Expired=7, AgeCheckRequired=8 | every value != Admitted means deny entry; AgeCheckRequired does NOT mark the ticket used |
| `SeatKind` | Standard=0, Double=1, Aisle=2 | `Aisle` is valid only on `Seat.Kind` (never a SeatType, PatronCategory or promotion-program seat kind); namespace is `Cinema.Data.Entities`, not `...Enums` (inconsistent with sibling enums) |
| `SeatStatus` | Available=0, Reserved=1, Occupied=2 | |
| `ShowTimeType` | Normal=0, Premiere=1, Special=2 | |
| `StaffReasonCode` | Other=0, CustomerRequest=1, WrongShowtime=2, DuplicateSale=3, ServiceFailure=4, TechnicalIssue=5, PriceMatch=6, Compensation=7 | |
| `StockMovementType` | Receive=0, Sale=1, SaleReversal=2, Adjust=3, Waste=4 | direction carried by signed Quantity |
| `StockReasonCode` | Other=0, Expired=1, Damaged=2, Spilled=3, TheftOrLoss=4, StockCountCorrection=5, OpeningBalance=6 | |
| `TemplateChannel` | Email=1, Sms=2 | |
| `UserStatus` | Active=0, Inactive=1, Banned=2 | |

## 10. Composite-key entities (no own `Id`)

`InvoiceTicket` (4-part PK), `InvoiceFoodAndDrink` (2-part PK), `MovieTypeDetail` (2-part PK), `ShowTimeRoom` (2-part PK) — pure relational join rows, breaking the `BaseEntity.Id` convention.

## 11. Multi-cascade-path avoidance

SQL Server rejects multiple cascade paths to the same table. Wherever two FKs on a join/child row could both cascade from a common ancestor, exactly one leg is `Restrict`/`NoAction` by design: `PromotionProgramSchedule.TheaterId` (Restrict), `RoomTypePatronCategoryPrice.RoomTypeId` (Restrict). Do not "fix" these to Cascade.

AgeRestriction, Holiday, MemberShip, MovieType, News and UserType have no dedicated `Configurations/*.cs` file and rely on EF conventions (verified against `3-Data/Cinema.Data/Configurations/`). Owner input needed: whether this is intentional.
