using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

/// <summary>One row per theater-wide PatronCategory, showing whether it is included in this
/// RoomType's allow-list. A RoomType only offers categories with IsIncluded=true; there is no
/// "unrestricted" fallback for a RoomType with none included.</summary>
public class RoomTypePatronCategoryPriceDTO
{
    public Guid Id { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid PatronCategoryId { get; set; }
    public string PatronCategoryName { get; set; } = string.Empty;
    public Guid SeatTypeId { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public SeatKind Kind { get; set; }
    /// <summary>The theater-wide PatronCategory.Price, shown for reference.</summary>
    public double DefaultPrice { get; set; }
    public bool IsIncluded { get; set; }
    /// <summary>The row's price when included, else the theater default (for pre-filling the UI).</summary>
    public double Price { get; set; }
}

public class SaveRoomTypePatronCategoryPricesRequest
{
    public Guid RoomTypeId { get; set; }
    public List<SaveRoomTypePatronCategoryPriceItem> Items { get; set; } = new();
}

/// <summary>Included=false deletes any existing row for this category regardless of Price.
/// Included=true upserts a row at Price, or at the category's current theater default when Price
/// is null.</summary>
public class SaveRoomTypePatronCategoryPriceItem
{
    public Guid PatronCategoryId { get; set; }
    public bool Included { get; set; }
    public double? Price { get; set; }
}
