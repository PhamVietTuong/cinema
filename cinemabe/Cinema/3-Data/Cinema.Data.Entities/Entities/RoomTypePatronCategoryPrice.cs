namespace Cinema.Data.Entities;

/// <summary>
/// A RoomType's patron-category allow-list: a RoomType offers EXACTLY the categories that have a row
/// here, at that row's Price (e.g. Room Type 1 = Adult Standard + Child Standard; Room Type 2 = Child
/// Double + Child Standard + Senior Standard). Zero rows means the RoomType offers nothing — there is
/// no fallback to the theater-wide PatronCategory.Price. RoomTypeManager.CreateAsync seeds a new
/// RoomType with one row per active theater-wide PatronCategory so it starts bookable; the admin then
/// removes the ones that shouldn't be offered there.
/// </summary>
public class RoomTypePatronCategoryPrice : BaseEntity
{
    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public Guid PatronCategoryId { get; set; }
    public PatronCategory PatronCategory { get; set; } = null!;

    public double Price { get; set; }
}
