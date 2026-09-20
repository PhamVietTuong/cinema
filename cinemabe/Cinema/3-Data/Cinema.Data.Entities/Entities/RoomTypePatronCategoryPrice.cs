namespace Cinema.Data.Entities;

/// <summary>
/// A per-RoomType override of a PatronCategory row's Price (e.g. a Deluxe RoomType charges more for
/// Adult/Standard than the theater-wide PatronCategory.Price). No row for a (RoomType, PatronCategory)
/// pair means that RoomType uses the theater-wide price — there is no separate NULL-price encoding for
/// "not overridden", only the absence of a row.
/// </summary>
public class RoomTypePatronCategoryPrice : BaseEntity
{
    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public Guid PatronCategoryId { get; set; }
    public PatronCategory PatronCategory { get; set; } = null!;

    public double Price { get; set; }
}
