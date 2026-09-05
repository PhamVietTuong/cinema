namespace Cinema.Data.Entities;

/// <summary>
/// Join table gating which SeatTypes a PatronCategory may book. A PatronCategory with no rows here
/// is unrestricted (may book any SeatType in its theater) — this keeps every existing category
/// working with no data migration when the gate ships.
/// </summary>
public class PatronCategorySeatType
{
    public Guid PatronCategoryId { get; set; }
    public PatronCategory PatronCategory { get; set; } = null!;

    public Guid SeatTypeId { get; set; }
    public SeatType SeatType { get; set; } = null!;
}
