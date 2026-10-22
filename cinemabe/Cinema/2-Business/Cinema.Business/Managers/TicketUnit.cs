using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

/// <summary>One priced, non-overridden seat row, for PromotionProgram auto-apply evaluation. A
/// Double seat's ListUnitPrice is already the half-price of the pair (see ComposeInvoiceAsync).</summary>
internal sealed record TicketUnit(SeatKind Kind, ProjectionForm Form, double ListUnitPrice);
