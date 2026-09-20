using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IRoomTypePatronCategoryPriceManager
{
    /// <summary>One row per PatronCategory row of the RoomType's theater, with the theater-wide price
    /// (DefaultPrice) and, where one exists, the RoomType-specific override (Price defaults to
    /// DefaultPrice when no override row exists).</summary>
    Task<List<RoomTypePatronCategoryPriceDTO>> GetByRoomTypeAsync(Guid roomTypeId);

    /// <summary>Replaces the whole override set for a RoomType: a null Price on an item deletes any
    /// existing override for that category. WARNING: this set also gates eligibility for bookings —
    /// leaving it empty keeps the RoomType unrestricted (every theater-wide category bookable at its
    /// default price), but saving even one entry restricts the RoomType to exactly the categories
    /// saved here (see RoomTypePatronCategoryPrice's doc comment).</summary>
    Task SaveAsync(SaveRoomTypePatronCategoryPricesRequest request);
}
