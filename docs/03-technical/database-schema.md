# Database Schema

> Purpose: Every EF Core entity/table — fields, types, constraints, relationships, indexes, and business meaning of non-obvious fields.
> Sources: `cinemabe/Cinema/3-Data/Cinema.Data.Entities/Entities/*.cs`, `.../Enums/*.cs`, `cinemabe/Cinema/3-Data/Cinema.Data/Configurations/*.cs`, `cinemabe/Cinema/3-Data/Cinema.Data/Contexts/CinemaContext.cs`; cross-checked against `business/domain-model.md` (verified 2026-10-31) — sampled entity/config files (`Invoice`, `InvoiceTicket`, `User`, their configs, `CinemaContext.OnModelCreating`) read directly and found to match that doc exactly as of this review.
> Last reviewed: 2026-11-01

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
| NotifyBookingEmails / NotifyPromotionEmails / NotifyReminderEmails | bool | default true (opt-out) |
| PasswordResetTokenHash | string? | SHA-256 of emailed token |
| PasswordResetExpiresAt | DateTime? | |
| FailedLoginCount | int | default 0 |
| LockoutEndUtc | DateTime? | account lockout until |
| EmailConfirmed | bool | default false |
| EmailVerificationTokenHash / EmailVerificationExpiresAt | string? / DateTime? | |
| TwoFactorEnabled | bool | default false |
| TwoFactorCodeHash / TwoFactorCodeExpiresAt | string? / DateTime? | emailed OTP |
| OverridePinHash | byte[]? | max 64, PBKDF2, staff-terminal manager override |
| OverridePinSalt | byte[]? | max 128 |
| OverridePinFailedCount | int | default 0 |
| OverridePinLockoutEndUtc | DateTime? | |

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
| MinPoints, MaxPoints | int | tier bounds |
| DiscountPercent | double | |

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
Name (200), Address (500), City (100), Phone, Email, ImageUrl, IsActive, Latitude/Longitude (double?). Nav `Rooms` (Cascade).

### Room
Name (100), TheaterId (Cascade), RoomTypeId (Restrict), TotalRows/TotalColumns (int), Status (RoomStatus: Active=0/Maintenance=1/Inactive=2). Nav `Seats` (Cascade), `ShowTimeRooms`.

### RoomType (per-theater commercial class: Standard/IMAX/4DX/etc.)
TheaterId (Cascade), Name (100), Description, SupportsThreeD (bool), ThreeDSurcharge (double), TurnoverBufferMinutes (int, default 0). Nav `Rooms`, `PatronCategoryPrices`.

### Seat
RoomId (Cascade), RowName (max 5), ColIndex (int), IsActive, SeatGroupId (Guid?, **sole source of truth for seat kind** — non-null links the two seats of a double/couple pair; no SeatTypeId on Seat). **Unique** (RoomId, RowName, ColIndex). Index on SeatGroupId.

### SeatType (per-theater: exactly Single + Double)
TheaterId (Cascade), Kind (SeatKind enum — matched by Kind, never Name), Name (display only), Color (hex, default `#808080`). **Unique** (TheaterId, Kind). Nav `PatronCategories`.

### ShowTime
MovieId (Cascade), StartTime/EndTime (DateTime), ProjectionForm (TwoD=1/ThreeD=2 — property of the screening, not the room), ShowTimeType (Normal=0/Premiere=1/Special=2), IsActive. Nav `ShowTimeRooms`.

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
| RequiresIdCheck | bool, default false, UI notice only, not enforced |
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

## 4. Promotions / Discounts / Loyalty / Gift Cards

### Discount
| Field | Notes |
|---|---|
| Code | string?, max 50, unique when present (filtered unique index `WHERE [Code] IS NOT NULL`) |
| Description? | |
| Percent | double |
| MaxDiscountAmount? | double |
| DiscountTypeId | FK → DiscountType, Restrict |
| StartDate/EndDate | DateTime |
| MaxUsage? / UsedCount | int, UsedCount default 0 |
| IsActive, AutoApply | bool |
| ApplyToAllTheaters | bool, default true (else via `DiscountTheaters`) |
| MovieId | Guid?, FK → Movie, **SetNull** |
| DaysOfWeekMask | int? bitmask, bit0=Sunday..bit6=Saturday |
| StartTimeOfDay/EndTimeOfDay | TimeOnly? |

Nav `DiscountTheaters`.

### DiscountTheater (join)
DiscountId (Cascade), TheaterId (**Restrict** — avoids SQL Server multi-cascade-path). **Unique** (DiscountId, TheaterId).

### DiscountType
`Name`. Nav `Discounts`.

### PromotionProgram (campaign-card promotion, display + optional auto-apply pricing)
| Field | Notes |
|---|---|
| Title (200), Summary (500)?, BannerUrl? | |
| Mechanic | enum: Info=0/FixedPrice=1/PercentOff=2 |
| PercentOff? | double, required iff Mechanic=PercentOff, (0,100] |
| Eligibility | enum: Everyone=0/Members=1/StudentOrTeacher=2/AgeLimit=3 |
| MinMemberShipId? | FK → MemberShip, **Restrict**, required iff Eligibility=Members |
| MaxAge? | int, required iff Eligibility=AgeLimit |
| StartDate/EndDate | DateTime |
| ExcludeHolidays | bool |
| AllowedChannelsMask? | int bitmask of SalesChannel; null = all |
| MaxTicketsPerTransaction? | int |
| StacksWithMembershipDiscount | bool |
| TermsText (2000)?, DisplayOrder, IsPublished | |
| AutoApply | bool; requires Mechanic != Info, Eligibility in (Everyone, Members), LinkedDiscountId null |
| LinkedDiscountId? | FK → Discount, **SetNull**, cosmetic only |

Index: (IsPublished, EndDate), MinMemberShipId, LinkedDiscountId. Nav `Prices`, `Schedules`.

### PromotionProgramPrice
PromotionProgramId (Cascade), ProjectionForm?/SeatKind? (null = "any"). Price (double, all-in). **Unique** (PromotionProgramId, ProjectionForm, SeatKind).

### PromotionProgramSchedule
PromotionProgramId (Cascade), TheaterId (Guid?, **Restrict**, null = all theaters), DaysOfWeekMask?, StartTimeOfDay/EndTimeOfDay?. Zero rows = applies everywhere/always.

### SplashBanner
Title, ImageUrl (required), LinkUrl (optional), StartDate/EndDate (DateTime, date-only, inclusive), IsPublished (bool). No FKs, no nav. "Active" is computed against Vietnam local date, not stored.

### LoyaltySetting
TheaterId (Guid?, **unique when not null** — one global default row with TheaterId=null plus optional per-theater overrides), PointsPerUnit (int), PointValueVnd (int).

### GiftCard
Code (max 50, **unique**), InitialBalance (double), Balance (double), IsActive, ExpiresAt (DateTime?), IssuedToEmail (string?, optional). No FKs — `Invoice.GiftCardId` references it without a DB FK constraint.

## 5. Booking / Invoicing / Payments

### Invoice (order/booking header)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | redeclared `new` |
| Code | string | max 50, **unique** |
| UserId | Guid? | FK → User, Restrict; null = walk-in counter sale |
| TotalAmount, DiscountAmount, FinalAmount | double | column type `float`; DiscountAmount default 0 |
| Status | InvoiceStatus | Pending=0/Paid=1/Cancelled=2/Failed=3 (4 reserved, "Refunded", never reused) |
| PaymentMethod?, PaymentReference? | string | |
| PaidAt | DateTime? | |
| PointsRedeemed | int | reserved at creation, restored on cancel/expire |
| GiftCardId | Guid? | **no FK** |
| GiftCardAmount | double | |
| DiscountId | Guid? | FK → Discount, SetNull |
| PromotionProgramId | Guid? | FK → PromotionProgram, SetNull; mutually exclusive with DiscountId as pricing source |
| TheaterId | Guid? | FK (shadow), Restrict; null only on rows predating the column |
| Channel | SalesChannel | Online=0/Counter=1 |
| SoldByUserId | Guid? | no FK |
| CashDrawerSessionId | Guid? | no FK |
| FoodStatus | FoodOrderStatus | None=0/Pending=1/Preparing=2/Ready=3/HandedOver=4/Cancelled=5 |
| FoodHandedOverAt / FoodHandedOverByUserId | | |
| ExchangedFromInvoiceId | Guid? | no FK |

Index: (TheaterId, PaidAt). Nav `InvoiceTickets`, `InvoiceFoodAndDrinks`, `Payments`.

**No Refund entity exists.** `InvoiceStatus` value 4 and `AuditAction.Refund` are reserved/retired — see `business/payments.md` §6 for the full history (a refund feature was built, then fully reverted).

### InvoiceTicket (one sold seat; no own Id)
PK: composite (InvoiceId, ShowTimeId, RoomId, SeatId). Price (double), PatronCategoryId (Guid?, **no FK**, snapshot), PatronCategoryName (max 100, snapshot), PatronDiscountPercent (double), QrCode?, IsUsed (bool, default false) + UsedAt (DateTime?) + UsedByUserId (Guid?, no FK), IsActive (bool, default true), SeatLabel (max 16, frozen at sale time, never recomputed).

**Key invariant**: filtered unique index on (ShowTimeId, RoomId, SeatId) `WHERE IsActive = 1` — the sole DB-level guard against double-booking across instances. Relationships: Invoice (Cascade), ShowTimeRoom (composite FK, Restrict), Seat (Restrict).

### InvoiceFoodAndDrink (join, no own Id)
PK (InvoiceId, FoodAndDrinkId). Quantity (int), UnitPrice/TotalPrice (double). Invoice (Cascade), FoodAndDrink (Restrict).

### InvoicePayment (one tender line)
InvoiceId (Cascade), Method (PaymentTender: Cash=0/Card=1/QrWallet=2/GiftCard=3/Points=4/Online=5), Amount (double), TenderedAmount/ChangeAmount (double?, cash only), Reference (string?, max 100).

## 6. Concession / Inventory

### FoodAndDrink
TheaterId (Cascade), Name (200), Price (double), ImageUrl?, Description?, IsAvailable (bool, default true), TrackInventory (bool), QuantityOnHand (int, **check constraint `>= 0`**), LowStockThreshold (int), TargetStockLevel (int, 0=unset), IsCombo (bool). Nav `InvoiceFoodAndDrinks`, `ComboItems`.

### ComboItem
ComboId (Cascade, FK → FoodAndDrink "Combo"), ComponentId (NoAction, FK → FoodAndDrink "Component"), Quantity (int, default 1). **Unique** (ComboId, ComponentId). Check constraints: `Quantity > 0`, `ComboId <> ComponentId`.

### StockMovement (insert-only ledger)
FoodAndDrinkId (NoAction), TheaterId (denormalized, no FK object), Type (StockMovementType: Receive=0/Sale=1/SaleReversal=2/Adjust=3/Waste=4), Quantity (int, **signed**), ReasonCode (StockReasonCode?, Adjust/Waste only), Reason (500)?, InvoiceId (Guid?, no FK), StoragePlanId (Guid?), UserId (Guid?, null = system/expiry reaper).

## 7. Staff / Operations

### StaffShift
TheaterId, UserId (plain Guid columns, no FK objects, indexed), StartTime/EndTime (DateTime, theater local wall-clock), Note (500)?. Index (TheaterId, StartTime), (UserId, StartTime).

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

See `business/domain-model.md` §9 for the full enum table (23 enums with every member and numeric value). Key rule: several enums are explicitly append-only with reserved/retired gaps that must never be renumbered or reused — `AuditAction` (4 retired, was Refund), `InvoiceStatus` (4 retired, was Refunded), plus `FoodOrderStatus`, `IncidentCategory/Severity/Status`, `PaymentTender`, `SalesChannel`, `SalesGroupBy`, `ScanOutcome`, `StockMovementType/ReasonCode`. `SeatKind` is namespaced under `Cinema.Data.Entities`, not `Cinema.Data.Enums` like its siblings.

## 10. Composite-key entities (no own `Id`)

`InvoiceTicket` (4-part PK), `InvoiceFoodAndDrink` (2-part PK), `MovieTypeDetail` (2-part PK), `ShowTimeRoom` (2-part PK) — pure relational join rows, breaking the `BaseEntity.Id` convention.

## 11. Multi-cascade-path avoidance

SQL Server rejects multiple cascade paths to the same table. Wherever two FKs on a join/child row could both cascade from a common ancestor, exactly one leg is `Restrict`/`NoAction` by design: `DiscountTheater.TheaterId` (Restrict), `PromotionProgramSchedule.TheaterId` (Restrict), `RoomTypePatronCategoryPrice.RoomTypeId` (Restrict). Do not "fix" these to Cascade.

<!-- TODO: confirm with team — some entities (AgeRestriction, Holiday, MemberShip, MovieType, News, UserType) have no dedicated Configurations/*.cs file and rely on EF conventions; verify this is intentional if touched. -->
