using Cinema.Business.DTO.Catalog;

namespace Cinema.Business.Contracts;

public interface IRoomTypePatronCategoryPriceManager
{
    /// <summary>One row per PatronCategory of the RoomType's theater, with IsIncluded showing whether
    /// it's in this RoomType's allow-list and Price giving the effective price either way (the row's
    /// price when included, else the theater default, so the UI can pre-fill the input).</summary>
    Task<List<RoomTypePatronCategoryPriceDTO>> GetByRoomTypeAsync(Guid roomTypeId);

    /// <summary>Applies each item: Included=false deletes any existing row for that category;
    /// Included=true upserts a row at Price, or at the category's current theater default when Price
    /// is null. Only touches the categories present in Items — a RoomType offers EXACTLY the
    /// categories with a row, so saving zero included items leaves the RoomType offering nothing
    /// (see RoomTypePatronCategoryPrice's doc comment). This is a legitimate, allowed state.</summary>
    Task SaveAsync(SaveRoomTypePatronCategoryPricesRequest request);
}
