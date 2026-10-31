# Catalog / Scheduling / Pricing / Promotions — Business Logic Reference

Scope: Movies, Theaters/Rooms/Seats, PatronCategory, ShowTime scheduling, ticket pricing, PromotionProgram,
loyalty points, gift cards. Backend only (`cinemabe`). All facts verified against current code as of this
writing (branch `feature/business-docs`, after the omdb-imdb-id, promotion-programs-redesign, pricing-matrix,
redeem-points-config, loyalty-redemption and gift-cards merges).

Primary files:
- Controller: `cinemabe/Cinema/1-Service/Cinema.Service.WebApiHost/Controllers/CinemaController.cs` (single controller, `#region` per entity — see CLAUDE.md "Only 3 controllers")
- Managers: `cinemabe/Cinema/2-Business/Cinema.Business/Managers/*.cs`
- DTOs: `cinemabe/Cinema/2-Business/Cinema.Business.DTO/{Catalog,Movies,Theaters,Loyalty,Invoices}/*.cs`
- Entities: `cinemabe/Cinema/3-Data/Cinema.Data.Entities/Entities/*.cs`, enums in `.../Enums/*.cs`

---

## 1. Catalog manager conventions

Every simple-lookup manager (`AgeRestrictionManager`, `MovieTypeManager`, `SeatTypeManager`,
`PatronCategoryManager`, `TimeSlotManager`, `TicketPriceManager`, `PersonManager`,
`PromotionProgramManager`, etc.) follows the same shape:

1. **`GetFilteredQuery(Dictionary<string,string>? filters)`** — starts from `_uow.XStore.GetQuery()`
   (an `IQueryable`), then `switch`es over `filters.Keys` and calls `_uow.XStore.FilterQuery(query, predicate)`
   per recognized key. Unknown/empty keys are ignored, not errors. Guid/bool/enum filter values are parsed
   with `Guid.TryParse`/`bool.TryParse`/`Enum.TryParse` inline, or via extension helpers
   `Cinema.Business.Extensions.FilterExtensions` (`GetString`, `GetGuid`, `GetDateOnly`, `GetDateTime`,
   `GetEnum<T>`, `GetBool`) — newer managers (Person, PatronCategory, TicketPrice, PromotionProgram,
   ShowTime) prefer the extension methods over raw `filters[key]` string juggling.
2. **`ApplySort(IQueryable<T>, SortDTO? sort)`** — `switch` on `sort.Field` (string), calling
   `_uow.XStore.OrderQuery(query, keySelector, sort.Ascending)`; unrecognized field = no-op (query unchanged).
3. **`GetAsync(PagingSearchDTO search)`** — `search ??= new PagingSearchDTO()`; resolve paging via
   `PagingHelper.ResolvePaging(search)` (newer managers) or manual `search.PageIndex > 0 ? ... : 1` /
   `PageSize > 0 ? ... : <default>` (older managers, inconsistent default page sizes: 12 for movies, 20 for
   most catalog lists). Then `CountAsync(query)` + `AllPageAsync(query, page-1, pageSize)`, mapped via
   `PagingHelper.ToPagedResult<TEntity,TDto>(items,total,page,pageSize)` or manual `DefaultSearchResults<T>`
   construction.
4. Mapping uses generic extension methods `entity.ToDTO<TEntity,TDto>()`, `request.ToNewEntity<TReq,TEntity>()`,
   `entity.PatchEntity<TEntity,TReq>(request)` (reflection/AutoMapper-style generic copy — not hand-written
   per-field mapping) for simple 1:1 shapes; hand-written mapping is used whenever a DTO needs joined/computed
   fields (Movie, ShowTime, PromotionProgram, PatronCategory's `SeatTypeName`/`Kind`).
5. Reads are `AsNoTracking`-friendly via `GetQuery()`; writes always re-fetch the tracked entity with
   `GetByIdAsync` before mutating (never patch a projection).
6. `ExistsAsync(Guid id)` is used by the controller's `EnsureExistsAsync` helper to return 404 before calling
   `GetByIdAsync`/`UpdateAsync`/`DeleteAsync` — the manager itself still re-validates existence internally where
   relevant (defense in depth, not redundant given concurrent deletes).
7. **Bulk/page-safe mapping pattern** (PromotionProgramManager.`MapPageAsync`, MovieManager rating batch): a
   list endpoint resolves child collections (prices, schedules, ratings) in ONE extra grouped query per
   related table for the whole page, not per row — see Behavioral Guideline §5.2 in CLAUDE.md. Always check for
   this pattern before adding a new list endpoint field that needs a join.

### Controller plumbing specific to this area
- `CinemaController` has helpers `Run(name, func)` / `RunNoContent(name, func)` / `EnsureExistsAsync(existsFunc, name, entityTypeName, id)` under `#region Catalog helpers` (~line 1778) that wrap the standard try/catch + `LogProvider` + 404 pattern so most catalog CRUD actions are one-liners.
- All list endpoints are `[HttpPost]` taking `PagingSearchDTO`, even though they're reads (see CLAUDE.md convention — filters go in the POST body).
- Catalog CRUD is public for reads, `[Authorize(Roles="Admin")]` for writes, consistent across every `#region`.

---

## 2. Movie rules

Entity: `Cinema.Data.Entities.Movie` (Title, Slug, Description, Duration (minutes), ReleaseDate/EndDate
(`DateOnly`), PosterUrl, TrailerUrl, Director, Cast (plain string), Language, Subtitle, **ImdbId**, IsActive,
AgeRestrictionId FK, `MovieTypeDetails` (genre link table), `MovieCredits` (structured cast/crew), Comments,
Evaluations).

### Lifecycle
- **Create** (`MovieManager.CreateAsync`): generates a slug once (`GenerateUniqueSlugAsync`), normalizes
  `ImdbId` (trim, null if blank), maps genre ids to `MovieTypeDetail` rows, and — if `Credits` were supplied —
  resolves/creates `Person` rows and `MovieCredit` rows, all committed atomically with the movie.
- **Update** (`UpdateAsync`): **slug is never re-generated on title edits** — it's set once at creation and
  stays stable so shared links/bookmarks keep working. `EndDate`, `ImdbId`, genre links, and credits are all
  reconciled (`SyncCredits` diffs by `(PersonId, CreditType)` key, updating/adding/removing rows rather than
  clear-and-reinsert, so credit IDs stay stable).
- **Delete** (`DeleteAsync`): **soft delete only** — sets `IsActive = false`, never removes the row (showtimes,
  invoices, comments reference it).
- `IsNowShowing` / `IsComingSoon` are **computed, not stored**: `IsNowShowing = ReleaseDate <= today && (EndDate
  == null || EndDate >= today)`; `IsComingSoon = ReleaseDate > today`. A movie with no `EndDate` runs
  indefinitely once released.

### Slug behavior
`SlugHelper` (`cinemabe/Cinema/2-Business/Cinema.Business/Helpers/SlugHelper.cs`):
- `Slugify`: lowercases, converts Vietnamese `đ/Đ` to `d/D` explicitly (Unicode normalization alone doesn't
  decompose đ), strips combining diacritic marks via Unicode normalization (FormD → strip
  `NonSpacingMark` → FormC), replaces runs of non-alphanumeric chars with `-`, trims leading/trailing dashes.
- `ToBaseSlug`: truncates to 300 chars (re-trimming trailing dash after cut); empty slug falls back to
  literal `"movie"`; a slug that happens to parse as a bare GUID is prefixed `movie-<guid>` (avoids colliding
  with any future id-based route).
- `PickUnique(baseSlug, takenSlugs)`: case-insensitive collision check against
  `MovieStore.GetSlugsStartingWithAsync(baseSlug)`; first collision appends `-2`, then `-3`, etc. (first free
  `-n` wins, scanning upward from 2).
- Lookup: `GET /GetMovieBySlug?slug=...` normalizes the incoming slug (`Trim().ToLowerInvariant()`) before
  querying — slugs are stored lowercase already but the lookup is defensive.

### Cast/crew credit management (`MovieManager.NormalizeCreditsAsync`)
`MovieCredit` links a `Movie` to a `Person` with `CreditType` (`Director=1`, `Actor=2`), optional
`CharacterName` (actors only — forced null for Director rows), and `DisplayOrder`. A request credit row
(`MovieCreditRequest`) carries `PersonId?` (explicit — edits that Person **globally**, not just for this movie)
or just a `PersonName`/`PersonPhotoUrl` (match-by-name-or-create). Resolution runs in 6 phases, read-only until
the last:
1. Per-row validation (defined `CreditType`, non-blank name, no duplicate `(explicit PersonId, CreditType)`).
2. One query loads every `Person` referenced by explicit id or by any row's name.
3. Every explicit `PersonId` must resolve to a loaded Person (else 400).
4. Computes the desired `(Name, PhotoUrl)` per explicit id; rejects conflicting edits of the same person in
   one request, and rejects a rename that collides with another loaded/renamed person's name (including a
   two-way name swap — both sides' target name is "currently taken" by the other).
5. Resolves every row to a real `PersonId` in request order: explicit id → rename target match → existing
   person matched by name (non-blank photo overwrites, unless that person already has an explicit edit, which
   wins) → a request-local pending new Person (first non-blank photo among same-named new rows wins).
   Duplicate `(resolved PersonId, CreditType)` still rejected here.
6. Only now are mutations applied: edit existing `Person` rows in place, apply name-matched photo updates,
   and `PersonStore.TrackNew(...)` every brand-new `Person` (needed because `BaseEntity.Id` is already a
   non-default Guid by the time EF sees it — without `TrackNew` the change tracker would treat it as an
   update, not an insert).

Throws `InvalidOperationException` (→ HTTP 400) on any violation; never partially applies.

### OMDb/IMDb rating matching (`OmdbRatingsProvider`, `cinemabe/Cinema/2-Business/Cinema.Business/ExternalRatings/OmdbRatingsProvider.cs`)
- Config key `OMDb:ApiKey`; if blank/missing, returns `null` immediately (no external calls, no throw).
- **Lookup precedence: IMDb id first.** If `Movie.ImdbId` is set (non-blank after trim), queries
  `?apikey=...&i=<imdbId>&type=movie` — an **exact** match. Only when `ImdbId` is null/blank does it fall back
  to `?apikey=...&t=<title>&y=<releaseYear>&type=movie` — a **best-effort title+year** match that misses for
  titles OMDb doesn't carry verbatim (e.g. localized Vietnamese titles).
- Pulls `imdbRating`, `Metascore`, and the `"Rotten Tomatoes"` entry from the `Ratings[]` array (matched by
  `Source` string, case-insensitive) out of the OMDb JSON response. `"N/A"` or blank values are normalized to
  `null`. If all three are null, the whole result is `null` (not an empty DTO).
- **Caching (in-process `IMemoryCache`, not persisted to DB)**: found result cached 24h; a "not found"
  (`Response != "True"` and the error isn't an API-key/rate-limit error) cached 6h; any transient failure
  (non-2xx, timeout, malformed JSON, API-key/limit errors) cached 10 minutes before retry. Cache key includes
  the IMDb id or the title+year pair, so different movies/lookups don't collide.
- **Never throws** — every failure path returns `null`; callers (MovieManager) treat "no data" and
  "couldn't fetch" identically. Request URLs are deliberately never logged (they carry the API key).
- Called from `MovieManager.BuildDetailAsync`: kicked off **before** the DB queries (`externalRatingsTask`)
  so the HTTP round-trip overlaps with local I/O, awaited last.

### Review system — two separate things, don't conflate
| | Table | Scope | Gate |
|---|---|---|---|
| **User rating/review** | `Evaluation` (Score 1-5 + optional free-text `Review`) | Internal, one row per (movie,user) — `RateMovieAsync` upserts | `EnsureCanReviewAsync` → `InvoiceStore.HasWatchedMovieAsync(userId, movieId, now)`: only users who purchased a **Paid** invoice for a showtime of this movie that has already started/ended may rate or comment. Message: `"You can only rate or comment on a movie after watching it."` (`AccessDeniedException`) |
| **User discussion comment** | `Comment` (threaded via `ParentId`, moderatable) | Internal, free text, many per user | Same `EnsureCanReviewAsync` gate as rating |
| **External critic scores** | Not persisted — fetched live from OMDb per detail-page view (cached) | `RottenTomatoes` / `ImdbRating` (OMDb's own IMDb score, independent of user `Evaluation`) / `Metascore` | None — always shown if available |

- `MovieDTO.AverageRating`/`RatingCount` are computed from `Evaluation` rows (`MovieStore.GetAverageRatingsAsync`
  batched for list pages, `GetAverageRatingAsync` single for detail).
- Comment moderation is admin-only (`GetCommentsForModeration`, `ModerateComment`, `DeleteComment` — the
  latter cascades to delete direct replies first, in an explicit transaction, since each store `DeleteAsync`
  commits independently and a mid-loop failure would otherwise orphan replies).
- `GetRecommendedMovies`: candidate pool = up to 200 active movies; for a signed-in user, favors genres from
  movies they rated ≥4, excludes movies already rated by them, orders by (favorite-genre match desc, average
  rating desc, rating count desc); anonymous users just get the global top-rated ordering.

---

## 3. Theater / Room / Seat / PatronCategory hierarchy

```
Theater (1) ──< RoomType (per-theater "class": Standard/IMAX/4DX/…, free-text Name)
Theater (1) ──< Room (TotalRows × TotalColumns, Status, belongs to exactly one RoomType)
Theater (1) ──< SeatType  — EXACTLY 2 rows per theater: Kind=Standard, Kind=Double (seeded by
                             ISeatTypeManager.EnsureDefaultsAsync on theater creation; no create/delete
                             endpoint — only Name/Description/Color are editable; matched by Kind, never Name)
Theater (1) ──< PatronCategory (one row per logical category PER SeatKind it may book — e.g. "Adult"
                             needs separate Standard and Double rows; a missing row = that category
                             cannot book that seat kind at all. Carries an absolute Price in VND,
                             IsActive, RequiresIdCheck + IdCheckMessage)
RoomType (1) ──< RoomTypePatronCategoryPrice — a RoomType's PatronCategory ALLOW-LIST: a RoomType offers
                             EXACTLY the categories that have a row here, at that row's Price. Zero rows
                             = the RoomType offers nothing bookable (no fallback to PatronCategory.Price).
                             RoomTypeManager.CreateAsync seeds one row per active theater-wide
                             PatronCategory so a new RoomType starts fully bookable; admin then removes
                             what shouldn't be offered there.
Room (1) ──< Seat (per-seat SeatTypeId assignment + double-seat grouping, see "Room seat map" region)
```

### Seat-type gating — the full rule (two independent allow-list layers)
1. **PatronCategory ↔ SeatType**: whether a logical category can book a seat *kind at all*, at this
   theater. Enforced structurally — "no row for that (category-name, SeatKind) pair" = impossible, not a
   runtime check. (`PatronCategory.cs` doc comment: *"omitting a row for a kind means that category cannot
   book that kind of seat at all (e.g. no Student/Double row means Students cannot book double seats)"*.)
2. **RoomType ↔ PatronCategory** (`RoomTypePatronCategoryPrice`): whether a category can book in a *room
   class* at all, and at what price there. A RoomType with zero rows for a category = that category is not
   offered in that room class, full stop — there is **no fallback** to the theater-wide `PatronCategory.Price`.
   This is resolved live at seat-pricing time in `BookingManager.BuildSeatPricingContextAsync` (§5 below):
   only categories with an override row for the showtime's RoomType end up priced/bookable; the rest are
   simply absent from `SeatPricingContext`.
3. `PatronCategoryManager.CreateAsync`/`UpdateAsync` validate `SeatTypeId` belongs to the same `TheaterId`
   (`ValidateSeatTypeAsync`) and reject a duplicate `(TheaterId, Name, SeatTypeId)` triple
   (`EnsureNotDuplicateAsync`) — i.e. you cannot have two "Adult" rows both scoped to Standard seats.
4. `RequiresIdCheck`/`IdCheckMessage` is **UI-only** — a customer-facing notice in CinemaUser that ID will be
   checked at the theater; it is **not verified or enforced anywhere in the booking flow** (explicitly called
   out in the entity doc comment).

---

## 4. ShowTime scheduling validation chain (`ShowTimeManager`)

Order of checks on **Create** (`mustBeFuture: true`) vs **Update** (`mustBeFuture: false` — editing a past
showtime record is allowed, e.g. fixing a typo):

1. **`ValidateWindow(start, end, mustBeFuture)`** — `end <= start` → reject ("must end after it starts");
   if `mustBeFuture`, `start <= DateTime.Now` → reject ("cannot be scheduled in the past"). Update skips the
   future-only part.
2. **Load movie runtime** (`LoadMovieRuntimeAsync` — projects Title/Duration/IsActive/ReleaseDate/EndDate only,
   not the full entity).
3. **`ValidateAgainstRuntime`** — reject if `end < start + Duration minutes`, i.e. the showtime window must be
   at least as long as the film. Skipped entirely if `Duration <= 0` (data-quality gap elsewhere, not this
   check's job).
4. **`ValidateMovieAvailability`** (only when `start > DateTime.Now` on Update — "re-arms" if a past showtime
   is moved into the future; always runs on Create since Create already requires a future start):
   - Movie must be `IsActive` (else: "no longer showing; reactivate the movie before scheduling it").
   - Showtime's **date** (not time) must be `>= ReleaseDate` and `<= EndDate` (both boundaries inclusive;
     `EndDate == null` = open-ended run). Only the start DATE is gated — a final-day screening may spill past
     midnight because step 3 already permits that.
5. **If a room is assigned** (`RoomId != Guid.Empty`):
   a. `LoadRoomClassAsync` — projects Room's RoomType.Name/SupportsThreeD/TurnoverBufferMinutes, Room.Status,
      Theater.Name/IsActive in one query.
   b. **`ValidateRoomAvailability`** (same future-only gate as step 4): theater must be `IsActive`
      (checked **first** — broader cause, better message); room `Status` must be `Active` (explicit message
      for `Maintenance`; anything else, including future enum values, is rejected generically as "not in
      service").
   c. **`ValidateRoomSupportsFormat`** — a `ProjectionForm.ThreeD` showtime requires `RoomType.SupportsThreeD`;
      this check is **not** future-gated (always enforced, Create and Update alike).
   d. **`HasRoomOverlapAsync(roomId, start, end, turnoverBufferMinutes, excludeShowTimeId)`** — rejects if the
      new/edited window overlaps any other showtime in that room, **expanded by the RoomType's
      `TurnoverBufferMinutes`** on each side. Message distinguishes a true double-booking (buffer=0) from a
      too-tight turnover gap (buffer>0, names the configured minutes).
6. Showtime + its single `ShowTimeRoom` (carrying `BasePrice`, a per-showtime manual surcharge — see §5) are
   persisted atomically in one `SaveChanges`.

Notes:
- A showtime has **exactly one room assignment** (`ShowTimeRooms.FirstOrDefault()` everywhere) — the
  collection shape suggests multi-room but current logic only ever uses the first/only entry.
  `ApplyRoom` no-ops if the room+price didn't actually change, so editing an already-booked showtime (whose
  room is referenced by invoices) doesn't attempt a disallowed delete-then-recreate of the `ShowTimeRoom` row.
- `ProjectionForm`: `TwoD = 1`, `ThreeD = 2`. `ShowTimeType`: `Normal = 0`, `Premiere = 1`, `Special = 2` (no
  scheduling-rule difference found tied to `ShowTimeType` — informational classification only, as of now).

---

## 5. PRICING — exact formula and precedence (`BookingManager.BuildSeatPricingContextAsync`)

> Source comment, verbatim intent preserved: *"A seat's price is anchored on the resolved PatronCategory row
> ... On top of that: a single multiplier factor applies ... A 3D screening adds RoomType.ThreeDSurcharge; and
> the showtime's own BasePrice is added last as a manual per-showtime surcharge ... NOT a multiplier."*

### Calculation order (computed once per `(ShowTime, Room)` pair, reused for every seat/category in that booking)

```
price = round( roomTypePrice(category, roomType) * priceFactor  +  threeDSurcharge  +  showTimeRoom.BasePrice ,  2 )
```

Step by step:

1. **Base: `roomTypePrice`** — looked up from `RoomTypePatronCategoryPrice` for `(RoomTypeId, PatronCategoryId)`.
   **If no row exists for this category in this RoomType, that category is not priced/offered at all** (see
   §3 — there is no fallback to theater-wide `PatronCategory.Price`; `PatronCategory.Price` is only the
   "before RoomType overrides" seed value documented on the entity, not read at booking time by this code
   path). Only categories that are `IsActive` **and** have an override row make it into `SeatPricingContext`.
2. **`priceFactor` (multiplier, mutually exclusive branches)**:
   - **Holiday wins outright**: if any `Holiday.Date` equals the showtime's date, `priceFactor =
     holiday.PriceMultiplier`. The weekday/TimeSlot matrix below is **not even queried** in this case.
   - **Otherwise, the TicketPrice matrix**: find the theater `TimeSlot` whose `[StartTime, EndTime)` window
     (parsed as `TimeOnly`, non-wrapping only — `end > start` required) contains the showtime's time-of-day;
     then look up the `TicketPrice` row for `(TheaterId, RoomTypeId, TimeSlotId, DayOfWeek)` matching the
     showtime's weekday. If a matching slot+row exists, `priceFactor = TicketPrice.PriceMultiplier`;
     otherwise (no slot matched, or no row for that slot/day) `priceFactor = 1.0`.
3. **`threeDSurcharge`** — `RoomType.ThreeDSurcharge` **only if** `ShowTime.ProjectionForm == ThreeD` (zero
   otherwise; the RoomType lookup for this is skipped entirely for 2D screenings — cheap path by default).
4. **`showTimeRoom.BasePrice`** — a manual flat amount set per showtime+room at ShowTime create/update time
   (e.g. a premium for a hot new release, 0 for a long-running title). **Added last, as a flat addend — it
   never interacts with/gets multiplied by `priceFactor`.**
5. Result rounded to 2 decimals (VND has no subunits in practice, but the DTO is `double`).
6. `SeatKind` (Standard/Double) for a category comes from `SeatTypeStore.GetKindMapAsync(theaterId)` keyed by
   `PatronCategory.SeatTypeId`; `minPriceByKind` (min price per `SeatKind` across resolved categories) is also
   precomputed for UI "from ₫X" display.

### Multi-theater / patron-category pricing summary table

| Factor | Entity | Scope | Combines as |
|---|---|---|---|
| Base price per category | `RoomTypePatronCategoryPrice.Price` | per (RoomType, PatronCategory) | multiplicand |
| Holiday surge/discount | `Holiday.PriceMultiplier` | per calendar date (global, not per-theater) | multiplier (wins over weekday matrix) |
| Weekday/time-slot matrix | `TicketPrice.PriceMultiplier` | per (Theater, RoomType, TimeSlot, DayOfWeek) | multiplier (only used if no Holiday match) |
| 3D surcharge | `RoomType.ThreeDSurcharge` | per RoomType | flat add, only if `ProjectionForm==ThreeD` |
| Per-showtime surcharge | `ShowTimeRoom.BasePrice` | per (ShowTime, Room) | flat add, always |

`TicketPriceManager` enforces: `PriceMultiplier > 0`; `(TheaterId, RoomTypeId, TimeSlotId)` must all belong to
the same theater (`ValidateScopeAsync`); at most one row per `(TheaterId, RoomTypeId, TimeSlotId, DayOfWeek)`
(`DuplicateExistsAsync` pre-check + DB unique index as the actual source of truth — a `DbUpdateException` race
on `UpsertAsync` is caught and translated to the same friendly message). `UpsertTicketPricesRequest` lets an
admin set one multiplier across several `DaysOfWeek` at once in a single transaction (update existing +
insert missing days).

---

## 6. Promotion programs (`PromotionProgram` entity + `PromotionProgramManager` + `BookingManager` auto-apply)

### Shape
- `Mechanic`: `Info = 0` (display-only, never affects price — rejected from `AutoApply`), `FixedPrice = 1`,
  `PercentOff = 2`.
- `Eligibility`: `Everyone = 0`, `Members = 1` (requires `MinMemberShipId`), `StudentOrTeacher = 2`,
  `AgeLimit = 3` (requires positive `MaxAge`). **Only `Everyone`/`Members` can `AutoApply`** —
  `StudentOrTeacher`/`AgeLimit` have no online verification, so `AutoApply=true` is rejected for them at save
  time (manual/code-redemption only, outside this auto-apply path).
- `PercentOff` (double, required+validated `0 < x <= 100` when `Mechanic=PercentOff`, else always `null`).
- `Prices` (`PromotionProgramPrice`: `ProjectionForm?`, `SeatKind?`, `Price`) — only populated when
  `Mechanic=FixedPrice`; at least one row required; no duplicate `(ProjectionForm, SeatKind)` pairs; rows may
  be wildcarded (null dimension = any).
- `Schedules` (`PromotionProgramSchedule`: `TheaterId?` (null = all theaters — **per-theater scoping** lives
  here), `DaysOfWeekMask` (bitmask, bit = `1 << (int)DayOfWeek`), `StartTimeOfDay?`/`EndTimeOfDay?` — both
  present or both absent; start must be `<` end). Zero schedule rows = unrestricted by day/time/theater.
- `StartDate`/`EndDate` (end must be `>= start`), `ExcludeHolidays` (bool), `AllowedChannelsMask` (bitmask over
  `SalesChannel` — 0 or null = all channels), `MaxTicketsPerTransaction?` (must be positive if set — caps how
  many ticket units in one order get the discount), `StacksWithMembershipDiscount` (bool), `AutoApply` (bool),
  `LinkedDiscountId?` (ties the program to a code-based `Discount` for display; **mutually exclusive with
  AutoApply** — "can't auto-apply AND be code-linked"), `DisplayOrder`, `IsPublished`.

### Eligibility/validation summary (`PromotionProgramManager.ValidateAsync`)
| Rule | Enforcement |
|---|---|
| `EndDate >= StartDate` | always |
| FixedPrice needs ≥1 non-negative, non-duplicate price row | if `Mechanic==FixedPrice` |
| `0 < PercentOff <= 100` | if `Mechanic==PercentOff` |
| `MinMemberShipId` must exist | if `Eligibility==Members` |
| `MaxAge > 0` | if `Eligibility==AgeLimit` |
| Schedule start<end, both-or-neither | per schedule row |
| `MaxTicketsPerTransaction > 0` | if set |
| `LinkedDiscountId` must exist | if set |
| `AutoApply` rejects `Mechanic==Info`, `Eligibility in {StudentOrTeacher,AgeLimit}`, and any `LinkedDiscountId` | if `AutoApply==true` |

### How a promotion applies at booking (`BookingManager.FindBestAutoApplyProgramAsync` + `ComputePricingAsync`)
Pricing runs **two independent scenarios** and charges whichever is cheaper (tie favors Scenario A):

- **Scenario A** (pre-existing): membership-tier % discount, then either a customer-entered discount code OR
  the best active auto-apply `Discount` (a *different*, older code-based entity, not `PromotionProgram`) whose
  scope matches.
- **Scenario B** (PromotionProgram auto-apply): candidates = `PromotionProgramStore.GetAutoApplyCandidatesAsync(showDate)`
  (published, `AutoApply=true`, in date range). Each candidate filtered, in order:
  1. `AllowedChannelsMask` bit-check against the booking's `SalesChannel` (Online/Counter).
  2. `ExcludeHolidays` — skip if the showtime's date is a `Holiday`.
  3. If it has `Schedules`, at least one schedule row must match: `(TheaterId == null || == bookingTheaterId)`
     AND `ScheduleWindowMatches` (day-of-week bit test + time-of-day window, inclusive `[from,to]`).
  4. `Eligibility==Members` requires the user's membership tier's `MinPoints` to meet the program's
     `MinMemberShipId`'s `MinPoints` threshold (looked up, not just tier-equality — a **higher** tier than the
     minimum also qualifies, since it's a points-floor comparison).
  5. Compute `ComputeProgramSaving`: per-ticket-unit saving (`UnitSaving` — `PercentOff` =
     `listPrice * percent/100`; `FixedPrice` = `listPrice - mostSpecificMatchingPriceRow` where "most
     specific" = both `ProjectionForm` and `SeatKind` set > one set > neither, and a `Double`-kind row's fixed
     price is halved before comparing since a Double unit already carries the per-seat half-price), sorted
     descending, summed over the **top N** units where `N = MaxTicketsPerTransaction ?? all units` (so if
     capped, the discount is applied to the *most valuable* tickets first — maximizes customer benefit, which
     also maximizes the cost to apply a cap correctly).
  6. Best candidate = highest total saving; ties broken by lowest `DisplayOrder`, then lowest `Id`.
- If Scenario B's program is found and saves > 0: `finalB = total - saving`, then **if
  `StacksWithMembershipDiscount`**, the membership % is additionally applied on top of `finalB` (never
  combined with a Discount code/auto-discount — Scenario B is its own closed track).
- Whichever of `finalA`/`finalB` is lower wins; its `DiscountId` (Scenario A) or `PromotionProgramId` (Scenario
  B) is recorded on the invoice, never both.

### Promotion-program pricing vs. ticket pricing
Promotion pricing (`FixedPrice`/`PercentOff`) is computed **against the already fully-priced ticket unit**
(`unit.ListUnitPrice`, i.e. the §5 pricing-matrix result including holiday/weekday factor, 3D surcharge, and
per-showtime `BasePrice`) — it is a discount layer applied *after* §5's ticket pricing, not a replacement
pricing engine. `GetPublishedAsync` (customer-facing promotions list) additionally filters schedules by
`theaterId` query filter for per-theater display scoping, independent of the booking-time schedule match.

---

## 7. Loyalty points & gift cards

### Loyalty settings (`LoyaltySetting` + `LoyaltySettingManager` + `LoyaltyRates` helper)
- One **global** row (`TheaterId == null`) plus optional **per-theater overrides** (`TheaterId` set).
  Resolution order: theater override → global row → hardcoded fallback constants
  (`LoyaltyRates.DefaultPointsPerUnit = 1000`, `DefaultPointValueVnd = 1000`) if even the global row is
  missing/non-positive (safety net; the table "should never actually be empty once seeded").
- Two independent rates:
  - **`PointsPerUnit`** — VND spent per 1 point earned.
  - **`PointValueVnd`** — VND value of 1 point when redeemed.
  (These are NOT required to be equal/inverse — e.g. 1000 VND spent earns 1 point, but 1 point might redeem
  for more or less than 1000 VND, by design — admin sets both independently.)
- `SaveAsync` requires both rates `> 0`; validates the theater exists when `TheaterId` is set; creates the row
  if none exists yet for that scope, else updates in place. `ClearOverrideAsync(theaterId)` removes a
  theater's override (reverting it to the global rate).

### Earning points (`BookingManager.ApplyPaidSideEffectsAsync`, runs when an invoice transitions to Paid)
```
pointsEarned = (int)(invoice.FinalAmount / pointsPerUnit)     // integer division — truncates, no partial points
user.Points += pointsEarned
```
Then re-evaluates membership tier: `tier = MemberShips.Where(MinPoints <= user.Points).OrderByDescending(MinPoints).First()`
— i.e. the **highest** tier the user's new point total qualifies for is assigned immediately (upgrades happen
the moment a paid booking pushes the balance over a threshold; no explicit downgrade logic was found — points
only increase via this path). Only runs for invoices with a `UserId` (counter sales with no customer account
earn nothing). `FinalAmount` here is **after** all discounts/promotions/points-redemption/gift-card deductions
— points are earned on what was actually paid, not list price.

### Redeeming points (at booking time, `CreateBookingAsync`/quote path, before payment)
```
(_, pointValueVnd) = LoyaltyRates.LoadAsync(theaterId)
maxByBalance = user.Points
maxByAmount  = (int)(finalAmountSoFar / pointValueVnd)
pointsRedeemed = min(requestedPointsToRedeem, maxByBalance, maxByAmount)   // can't go negative or exceed balance
pointsValue = pointsRedeemed * pointValueVnd
finalAmount -= pointsValue; discountAmount += pointsValue
user.Points -= pointsRedeemed                                              // deducted immediately (reserved)
```
Points are **reserved at booking time and restored if the booking is cancelled/expires** (not deducted only on
payment success) — redemption happens *before* the gift-card deduction step, both *after* the §6 promotion
pricing has produced `finalAmount`. A `DryRun` booking computes but does not persist the point deduction.

### Gift cards (`GiftCard` entity + `GiftCardManager` + `BookingManager`)
- **Issue** (`IssueAsync`, admin): `Amount > 0` required; `Code` auto-generated as `"GC-" +
  12-hex-chars-from-a-new-Guid (uppercased)`; `InitialBalance == Balance` at issuance; optional `ExpiresAt`,
  `IssuedToEmail`. No purchase flow found in this manager — issuance is an admin action (e.g. manual
  issue/promo giveaway), not a customer self-service "buy a gift card" endpoint as of current code.
- **Validate** (`ValidateAsync(code)`, used by CinemaUser before applying at checkout): fails if not found,
  inactive, expired (`ExpiresAt <= now`), or zero/negative balance — returns a typed
  `GiftCardValidationDTO{Valid,Message,Balance}` rather than throwing, so the UI can show a friendly reason.
- **Redeem at booking** (`BookingManager`): looked up by trimmed code; usable = `IsActive` AND (`ExpiresAt ==
  null || > now`); an unusable/missing code **throws** (`"Invalid or expired gift card."`) — unlike
  `ValidateAsync`'s soft response, the actual charge path is strict so a customer is never silently charged
  full price on a bad code. Deduction = `min(card.Balance, finalAmount)` (never overdraws, never refunds
  the difference as cash); whole-VND bookings floor the deduction so it never rounds up past the card's actual
  balance. Applied **after** points redemption, i.e. it covers whatever points redemption didn't. Balance is
  decremented immediately (reserved) and restored on cancellation/expiry, same pattern as points.
- `SetActiveAsync` lets admin deactivate a card (e.g. reported lost) without deleting history.

---

## 8. Search/filter key conventions across list endpoints

All list endpoints take `PagingSearchDTO { PageIndex, PageSize, Filters: Dictionary<string,string>, Sort:
SortDTO{Field,Ascending} }` in a POST body. Recurring filter keys across this area:

| Key | Type | Used by |
|---|---|---|
| `theaterId` | Guid | TicketPrice, PatronCategory, PromotionProgram (`GetPublishedAsync`), TimeSlot-scoped lookups |
| `roomTypeId` | Guid | TicketPrice |
| `timeSlotId` | Guid | TicketPrice |
| `dayOfWeek` | enum (`DayOfWeek` name) | TicketPrice |
| `keyword` | string, `Contains` | PatronCategory (matches `Name`), Person (matches `Name`), PromotionProgram (matches `Title`), GiftCard (matches `Code`/`IssuedToEmail`, done in-memory not SQL) |
| `isActive` | bool | PatronCategory, ShowTime |
| `isPublished` | bool | PromotionProgram |
| `search` / `title` | string | Movie (`GetMoviesAsync` — either key matches, `search` takes precedence) |
| `director` | string | Movie |
| `movieTypeId` | Guid | Movie (genre filter) |
| `movieId`, `roomId`, `from`, `to` | Guid/Guid/DateTime/DateTime | ShowTime (`SearchAsync`) |
| `name` | string, `Contains` | Person (separate from `keyword`, same effect — redundant pair, not consolidated) |

Sort fields are a `switch` allow-list per manager (e.g. TicketPrice: `dayOfWeek`, `priceMultiplier`;
PatronCategory: `name`, `price`) — anything else silently no-ops rather than erroring.

---

## 9. Validation / error conditions (this area)

All thrown as `InvalidOperationException` (→ 400 via `ApiControllerBase`/`ExceptionMiddleware`) unless noted;
`KeyNotFoundException` → 404; `AccessDeniedException` → 403(ish, see middleware).

| Condition | Where |
|---|---|
| "A showtime must end after it starts." | ShowTimeManager.ValidateWindow |
| "A showtime cannot be scheduled in the past." | ShowTimeManager.ValidateWindow (create only) |
| "'{movie}' runs {N} minutes; the showtime must end no earlier than {time}." | ShowTimeManager.ValidateAgainstRuntime |
| "'{movie}' is no longer showing; reactivate the movie before scheduling it." | ShowTimeManager.ValidateMovieAvailability |
| "'{movie}' is not released until {date}; ..." / "...finished its run on {date}; ..." | ShowTimeManager.ValidateMovieAvailability |
| "Theater '{name}' is closed; pick a room in an active theater." | ShowTimeManager.ValidateRoomAvailability |
| "That room is under maintenance..." / "...not in service..." | ShowTimeManager.ValidateRoomAvailability |
| "Room class '{name}' cannot screen 3D. Pick a 3D-capable room or set the showtime to 2D." | ShowTimeManager.ValidateRoomSupportsFormat |
| "This room needs {N} minutes of turnover time..." / "...already has a showtime overlapping that time window." | ShowTimeManager.RoomOverlapMessage |
| "{PriceMultiplier} must be greater than 0." | TicketPriceManager.ValidateMultiplier |
| "RoomType does not belong to this theater." / "TimeSlot does not belong to this theater." | TicketPriceManager.ValidateScopeAsync |
| "A ticket price already exists for this room type, time slot and day." | TicketPriceManager (pre-check + DB-race catch) |
| "At least one day of week is required." / "Invalid day of week." | TicketPriceManager.UpsertAsync |
| "The seat kind does not belong to this theater." | PatronCategoryManager.ValidateSeatTypeAsync |
| "A category with this name already exists for this seat kind." | PatronCategoryManager.EnsureNotDuplicateAsync |
| "End date must not be before start date." | PromotionProgramManager.ValidateAsync |
| "FixedPrice requires at least one price row." / "...must not be negative." / "Duplicate price rows for the same format/seat kind." | PromotionProgramManager.ValidateAsync |
| "PercentOff must be between 0 (exclusive) and 100." | PromotionProgramManager.ValidateAsync |
| "A valid membership tier is required for Members eligibility." | PromotionProgramManager.ValidateAsync |
| "A positive MaxAge is required for AgeLimit eligibility." | PromotionProgramManager.ValidateAsync |
| "A schedule row needs both a start and end time, or neither." / "...start time must be before its end time." | PromotionProgramManager.ValidateAsync |
| "MaxTicketsPerTransaction must be positive when set." | PromotionProgramManager.ValidateAsync |
| "Linked discount does not exist." | PromotionProgramManager.ValidateAsync |
| "Info programs cannot be set to auto-apply." | PromotionProgramManager.ValidateAsync |
| "StudentOrTeacher/AgeLimit eligibility cannot be auto-applied online..." | PromotionProgramManager.ValidateAsync |
| "An auto-apply program cannot also be linked to a code-based discount." | PromotionProgramManager.ValidateAsync |
| "Point rates must be positive." | LoyaltySettingManager.SaveAsync |
| "Gift card amount must be positive." | GiftCardManager.IssueAsync |
| "Invalid or expired gift card." | BookingManager (strict redeem path) |
| "Discount code is invalid or no longer available." | BookingManager._invalidDiscountMessage |
| "You can only rate or comment on a movie after watching it." | MovieManager.EnsureCanReviewAsync |
| "The same person cannot be credited twice in the same role." | MovieManager.NormalizeCreditsAsync |
| "A person with this name already exists." / "'{name}' conflicts with a person being renamed in this save." | MovieManager.NormalizeCreditsAsync |
| "Conflicting name/photo for the same person in this save." | MovieManager.NormalizeCreditsAsync |
| "Invalid credit type." / "Each credit must name a person." | MovieManager.NormalizeCreditsAsync |

---

## 10. Key invariants / gotchas

1. **VNPay-style "callback-authoritative" pattern does not apply here** — this section is catalog/pricing, not
   payments; see root CLAUDE.md for payment-specific invariants.
2. **`RoomTypePatronCategoryPrice` is the real gate, not `PatronCategory.Price`.** At booking time,
   `PatronCategory.Price` is never read — only the RoomType-specific override row. A category with no override
   row for a given RoomType is invisible/unbookable there, silently (no error — it just doesn't appear in
   `SeatPricingContext`).
3. **Holiday multiplier fully replaces, never stacks with, the weekday/TimeSlot matrix** — if a showtime's
   date is a Holiday, the `TimeSlot`/`TicketPrice` lookup is skipped entirely, not applied on top.
4. **3D surcharge and per-showtime BasePrice are flat ADDENDS, not multipliers** — they are added after the
   `roomTypePrice * priceFactor` multiplication, so they're immune to holiday/weekday surge pricing.
5. **A showtime has one room, structurally supports more** — `ShowTimeRooms` is a collection but every read
   path uses `.FirstOrDefault()`. Don't assume multi-room showtimes work end-to-end even though the schema
   allows it.
6. **Movie slugs never change after creation**, even on a title rename — this is intentional (stable links),
   not a bug if you notice an outdated slug next to an updated title.
7. **OMDb matching degrades silently** — a movie without `ImdbId` set falls back to fuzzy title+year matching,
   which can mismatch or simply return nothing for non-English/localized titles. Setting `ImdbId` is the fix,
   not a provider bug.
8. **Two parallel "promotion" mechanisms coexist**: the older code-based `Discount` entity (membership %, codes,
   auto-apply discounts) and the newer `PromotionProgram` (fixed-price/percent-off, schedule-scoped,
   auto-apply). `ComputePricingAsync` runs both and picks the cheaper result — they are **never combined**
   except that `StacksWithMembershipDiscount` lets a `PromotionProgram` additionally apply the membership %
   on top of itself (but still never an actual `Discount` row).
9. **Points/gift-card deductions are "reserved," not "charged on payment."** They're deducted from the
   balance at booking time (so a double-spend across two concurrent carts isn't possible) and must be
   explicitly restored by whatever cancellation/expiry path exists — verify that path still exists/works if
   touching booking cancellation logic.
10. **`AutoApply` + `LinkedDiscountId` are mutually exclusive by design** — a PromotionProgram either
    auto-applies invisibly or is a discoverable code tied to a `Discount`, never both.
11. **SeatType is matched by `Kind` enum, never by `Name`** — Name is freely renamable display text; don't key
    any new logic off `SeatType.Name`.
12. **Loyalty "points per unit" and "point value" are independently configured, not inverses** — don't assume
    1 point always equals what it cost to earn; always resolve both via `LoyaltyRates`, never hardcode 1000.
13. **No automated enforcement of `RequiresIdCheck`** — if a future feature needs server-side ID verification
    at booking time, it doesn't exist yet; the current field is purely a UI notice.
