using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>Store for per-RoomType overrides of PatronCategory.Price.</summary>
public interface IRoomTypePatronCategoryPriceStore : IGenericStore<RoomTypePatronCategoryPrice>
{
    Task<IReadOnlyList<RoomTypePatronCategoryPrice>> FindByPatronCategoriesAsync(IReadOnlyCollection<Guid> patronCategoryIds);
    Task<IReadOnlyList<RoomTypePatronCategoryPrice>> FindByRoomTypeAsync(Guid roomTypeId);
    Task DeleteByRoomTypeAsync(Guid roomTypeId);
}
