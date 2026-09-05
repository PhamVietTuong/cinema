using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>Composite-key store for the PatronCategory ↔ SeatType gating join table.</summary>
public interface IPatronCategorySeatTypeStore
{
    Task<IEnumerable<PatronCategorySeatType>> FindByPatronCategoriesAsync(IReadOnlyCollection<Guid> patronCategoryIds);
    Task ReplaceForPatronCategoryAsync(Guid patronCategoryId, IReadOnlyCollection<Guid> seatTypeIds);
    Task DeleteBySeatTypeAsync(Guid seatTypeId);
}
