using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IStoragePlanStore : IGenericStore<StoragePlan>
{
    /// <summary>The plan with its items, tracked so the caller can modify and save it.</summary>
    Task<StoragePlan?> GetWithItemsAsync(Guid id);
}
