using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IStoragePlanStore : IGenericStore<StoragePlan>
{
    /// <summary>The plan with its items, tracked so the caller can modify and save it.</summary>
    Task<StoragePlan?> GetWithItemsAsync(Guid id);

    /// <summary>Plan code by plan id for a batch of ids (one query). Unknown ids are absent.</summary>
    Task<Dictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
