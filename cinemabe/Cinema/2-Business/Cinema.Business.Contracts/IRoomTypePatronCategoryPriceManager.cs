using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IRoomTypePatronCategoryPriceManager
{
    /// <summary>One row per PatronCategory row of the RoomType's theater, with the theater-wide price
    /// (DefaultPrice) and, where one exists, the RoomType-specific override (Price defaults to
    /// DefaultPrice when no override row exists).</summary>
    Task<List<RoomTypePatronCategoryPriceDTO>> GetByRoomTypeAsync(Guid roomTypeId);

    /// <summary>Replaces the whole override set for a RoomType: a null Price on an item deletes any
    /// existing override for that category (falls back to the theater-wide price).</summary>
    Task SaveAsync(SaveRoomTypePatronCategoryPricesRequest request);
}
