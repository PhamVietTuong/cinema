namespace Cinema.Data.Entities;

/// <summary>
/// A per-RoomType override of a PatronCategory row's Price (e.g. a Deluxe RoomType charges more for
/// Adult/Standard than the theater-wide PatronCategory.Price). There is no separate NULL-price
/// encoding for "not overridden", only the absence of a row.
///
/// This also doubles as the RoomType's eligibility gate: a RoomType with ZERO rows here is
/// unrestricted (every theater-wide PatronCategory is bookable there at its default price); a
/// RoomType with ANY row here is restricted to exactly the categories that have one — e.g. an
/// auditorium seeded with only Adult and Student rows will not offer Senior/Child at all, even
/// though those categories exist theater-wide. Same "empty = unrestricted, any row = restricted"
/// pattern the old PatronCategorySeatType gate used.
/// </summary>
public class RoomTypePatronCategoryPrice : BaseEntity
{
    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public Guid PatronCategoryId { get; set; }
    public PatronCategory PatronCategory { get; set; } = null!;

    public double Price { get; set; }
}
