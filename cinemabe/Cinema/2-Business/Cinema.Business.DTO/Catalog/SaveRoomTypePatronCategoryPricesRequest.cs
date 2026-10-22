using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

public class SaveRoomTypePatronCategoryPricesRequest
{
    public Guid RoomTypeId { get; set; }
    public List<SaveRoomTypePatronCategoryPriceItem> Items { get; set; } = new();
}
