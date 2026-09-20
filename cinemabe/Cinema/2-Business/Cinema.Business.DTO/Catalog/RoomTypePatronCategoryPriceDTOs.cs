using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

public class RoomTypePatronCategoryPriceDTO
{
    public Guid Id { get; set; }
    public Guid RoomTypeId { get; set; }
    public Guid PatronCategoryId { get; set; }
    public string PatronCategoryName { get; set; } = string.Empty;
    public Guid SeatTypeId { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public SeatKind Kind { get; set; }
    /// <summary>The theater-wide PatronCategory.Price, shown alongside the override for reference.</summary>
    public double DefaultPrice { get; set; }
    public double Price { get; set; }
}

public class SaveRoomTypePatronCategoryPricesRequest
{
    public Guid RoomTypeId { get; set; }
    public List<SaveRoomTypePatronCategoryPriceItem> Items { get; set; } = new();
}

public class SaveRoomTypePatronCategoryPriceItem
{
    public Guid PatronCategoryId { get; set; }

    /// <summary>Null clears the override for this category (falls back to the theater-wide price).</summary>
    public double? Price { get; set; }
}
