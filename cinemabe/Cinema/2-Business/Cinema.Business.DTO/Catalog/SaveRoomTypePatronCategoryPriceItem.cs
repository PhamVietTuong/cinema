using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

/// <summary>Included=false deletes any existing row for this category regardless of Price.
/// Included=true upserts a row at Price, or at the category's current theater default when Price
/// is null.</summary>
public class SaveRoomTypePatronCategoryPriceItem
{
    public Guid PatronCategoryId { get; set; }
    public bool Included { get; set; }
    public double? Price { get; set; }
}
