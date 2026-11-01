# Glossary

> Purpose: Domain and technical terms mapped to their meaning and corresponding code location.
> Sources: E:\cinema\business\domain-model.md, E:\cinema\cinemabe\Cinema\3-Data\Cinema.Data.Entities, E:\cinema\CLAUDE.md
> Last reviewed: 2026-11-01

## Business / domain terms

| Term | Meaning | Code |
|---|---|---|
| Invoice | The order/booking header; one row per transaction (online booking or counter sale). Status lifecycle: Pending → Paid/Cancelled/Failed. | `Cinema.Data.Entities/Entities/Invoice.cs`; DTOs `InvoiceDTO`, `InvoiceAdminDTO` |
| InvoiceTicket | One sold seat on an invoice; composite key `(InvoiceId, ShowTimeId, RoomId, SeatId)`, no own `Id`. `IsActive=false` on cancel/expire (soft delete). Unique filtered index on active rows is the DB-level anti-double-booking guard. | `Cinema.Data.Entities/Entities/InvoiceTicket.cs` |
| InvoicePayment | One tender line on an invoice (cash/card/QR/gift-card/points/online), via `PaymentTender` enum. | `Cinema.Data.Entities/Entities/InvoicePayment.cs` |
| SeatLock | In-memory, process-local, 5-minute advisory hold on a seat during checkout. Lives in a `static ConcurrentDictionary` inside `BookingManager`; does not survive a restart and is not shared across instances. Not the actual anti-double-booking mechanism — see InvoiceTicket's unique filtered index. | `Cinema.Business/Managers/BookingManager.cs`; `BookingHub` (`/hubs/booking`) |
| PatronCategory | The pricing + eligibility engine: one row per (Theater, SeatKind-via-SeatType, category name), e.g. Adult/Single. Omitting a row for a seat kind is the entire eligibility rule. | `Cinema.Data.Entities/Entities/PatronCategory.cs` |
| SeatType | Per-theater, exactly `Single` + `Double`, keyed by `SeatKind` enum (matched by Kind, never by Name). | `Cinema.Data.Entities/Entities/SeatType.cs` |
| SeatKind | Enum: `Standard=0`, `Double=1`. Lives in `Cinema.Data.Entities` namespace, not `...Enums` like sibling enums (documented inconsistency). | `Cinema.Data.Entities/SeatKind.cs` (namespace quirk noted in `business/domain-model.md` §9) |
| MemberShip | Loyalty tier: `Name`, `MinPoints`/`MaxPoints`, `DiscountPercent`. Also referenced by `PromotionProgram.MinMemberShipId` as a minimum-tier gate. | `Cinema.Data.Entities/Entities/MemberShip.cs` |
| Discount | Older of two promotion mechanisms: flat-percent code/auto-apply engine, optionally theater-scoped, day/time-windowed. | `Cinema.Data.Entities/Entities/Discount.cs` |
| PromotionProgram | Newer, richer "campaign card" promotion (Cinestar-style), deliberately separate from `Discount`. Drives CinemaUser's promotions page; only lowers checkout price when `AutoApply=true`. | `Cinema.Data.Entities/Entities/PromotionProgram.cs` |
| GiftCard | Prepaid balance identified by a unique `Code`. No FK from `Invoice` — referenced loosely via `Invoice.GiftCardId`. | `Cinema.Data.Entities/Entities/GiftCard.cs` |
| Holiday | Global (no TheaterId) calendar-date price multiplier (default 1.5) that overrides `TicketPrice`'s day-of-week multiplier for that date. | `Cinema.Data.Entities/Entities/Holiday.cs` |
| TicketPrice | Time-of-day/day-of-week price multiplier keyed by (TheaterId, RoomTypeId, TimeSlotId, DayOfWeek). | `Cinema.Data.Entities/Entities/TicketPrice.cs` |
| ShowTimeRoom | Join of a ShowTime to the Room it's screened in; carries `BasePrice` (flat per-showtime manual adjustment, not the seat's base price). | `Cinema.Data.Entities/Entities/ShowTimeRoom.cs` |
| ComboItem | A component line of a food/drink combo (join between a `Combo` FoodAndDrink row and a `Component` FoodAndDrink row). Combos hold no stock themselves — they deduct component stock. | `Cinema.Data.Entities/Entities/ComboItem.cs` |
| StockMovement | Insert-only signed-quantity ledger for food/drink inventory; `FoodAndDrink.QuantityOnHand` is only ever changed together with a StockMovement row (app-level invariant). | `Cinema.Data.Entities/Entities/StockMovement.cs` |
| AuditLog | Single shared log table for every audit/log need, discriminated by `AuditAction` enum. No FKs by design — survives deletion of what it references. | `Cinema.Data.Entities/Entities/AuditLog.cs` |
| Incident | Operational incident report (seat/room/projection/sound/safety/etc.), no FKs by design (like AuditLog). Can flag a seat or room out of sale (`BlocksSeat`/`BlocksRoom`). | `Cinema.Data.Entities/Entities/Incident.cs` |
| StaffShift | A theater-staff work shift (TheaterId, UserId, StartTime/EndTime). Confirmed still live/wired, not removed despite a past "remove staff features" effort. | `Cinema.Data.Entities/Entities/StaffShift.cs` |
| ReminderLog | Dedup table (`Unique(UserId, ShowTimeId)`) so a showtime reminder job doesn't resend after a process restart. | `Cinema.Data.Entities/Entities/ReminderLog.cs` |
| Template | Email/SMS notification template, looked up by an immutable `Code`, rendered via `{{Token}}` placeholders. | `Cinema.Data.Entities/Entities/Template.cs`; `TemplateRenderer` |
| RoomType | Per-theater commercial screening class (Standard/IMAX/4DX/etc.), carries 3D surcharge and turnover buffer. | `Cinema.Data.Entities/Entities/RoomType.cs` |
| PendingBookingReaper | The separate 15-minute background job that expires unpaid invoices/releases seats at the DB level — distinct from the 5-minute in-memory SeatLock timer. | `business/booking.md` §1 (see also `Cinema.Business/` background job registration) |

## Technical / infrastructure terms

| Term | Meaning | Code |
|---|---|---|
| `PagingSearchDTO` | Standard request shape for every list/search endpoint (POST body); carries paging + a generic `Filters` bag read via extension methods like `GetGuid("key")`. | `Cinema.Business.DTO/Requests/PagingSearchDTO.cs`; `Cinema.Business/Extensions/FilterExtensions.cs` |
| `DefaultSearchResults<T>` | Standard paged-list response shape returned by every search endpoint, paired with `PagingSearchDTO`. | `Cinema.Data.Entities/Entities/DefaultSearchResults.cs` |
| `IApplicationUnitOfWork` | The one interface exposing every typed store (repository) plus transaction methods; the seam through which business managers touch the database. | `Cinema.Data.Contracts/IApplicationUnitOfWork.cs` |
| `BaseEntity` | Base class every entity inherits: Guid `Id` (client-generated), `CreationTime`, `LastUpdatedTime`. `Invoice` and `User` redeclare `Id` via `new` (same semantics). | `Cinema.Data.Entities/Entities/BaseEntity.cs` |
| `CatalogManager<T>` (does **not** exist) | Despite being mentioned in older notes, there is **no** generic `CatalogManager<T>` base class — each catalog manager hand-implements the same `GetQuery`/`FilterQuery`/`OrderQuery` + `PagingHelper` convention independently. | `business/catalog.md` §1 |
| `ILog` / `LogProvider` | Serilog-backed logging abstraction, accessed globally via `LogProvider.Current.Information(...)` and registered with `services.AddFoundationLogging()`. | `Cinema.Foundation/Logging/` |
| `ClaimsPrincipalExtensions.GetUserId()` | Canonical way controllers read the authenticated user's GUID from JWT claims. | `Cinema.Service.WebApiHost` (extension method) |
| `RoleNames` | Static class mapping coarse roles (`Admin`, `Customer`, `TheaterStaff`) to capability-composite "role groups" (e.g. `Sellers`, `GateKeepers`, `Approvers`) that all resolve back to `Admin` or `Admin,TheaterStaff` — there is no finer-grained staff permission system. | `Cinema.Business/RoleNames.cs` (per `business/auth.md` §4) |
| `API_BASE_URL` / `HUB_BASE_URL` | Angular DI tokens exported from `CinemaLib`'s `public-api.ts` for the Web API base URL and SignalR hub base URL. | `cinemafe/projects/CinemaLib/src/public-api.ts` |
| `authGuard` / `adminGuard` | Angular route guards exported from `CinemaLib`, used by `CinemaUser`/`CinemaAdmin` route configs. | `cinemafe/projects/CinemaLib` |
| `*ServiceAgent` | Post-processed NSwag output class wrapping the generated namespace for each API group (Cinema/Identity/Payment) so Angular apps can import a single class. | `cinemafe/projects/CinemaLib/src/lib/services/*-http.service.ts` (generated — do not hand-edit) |

<!-- TODO: confirm with team whether PendingBookingReaper's exact file/class name should be added here — it was only located via business/booking.md's description, not directly grepped in this pass. -->
