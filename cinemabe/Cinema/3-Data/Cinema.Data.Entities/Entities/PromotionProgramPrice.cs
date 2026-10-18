using Cinema.Data.Enums;

namespace Cinema.Data.Entities;

/// <summary>One fixed price row for a PromotionProgram with Mechanic = FixedPrice. Null ProjectionForm
/// or SeatKind means "any" for that dimension; the most specific matching row wins when resolving a
/// ticket's price (see BookingManager.UnitSaving). At most one row per (ProjectionForm, SeatKind) pair
/// per program.</summary>
public class PromotionProgramPrice : BaseEntity
{
    public Guid PromotionProgramId { get; set; }
    public PromotionProgram PromotionProgram { get; set; } = null!;

    public ProjectionForm? ProjectionForm { get; set; }
    public SeatKind? SeatKind { get; set; }

    /// <summary>All-in VND price for one bookable unit of this shape (a Double row prices the whole
    /// pair, same convention as PatronCategory.Price).</summary>
    public double Price { get; set; }
}
