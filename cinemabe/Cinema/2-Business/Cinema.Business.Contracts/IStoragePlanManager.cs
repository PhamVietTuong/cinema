using Cinema.Business.DTO.Inventory;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>
/// Restock plans for a theater's warehouse: drafted by staff, approved by a manager/admin, then received into stock.
/// Every method takes the caller's theater scope: null = admin (all theaters); otherwise anything outside that
/// theater behaves as not found (KeyNotFoundException). Illegal state transitions throw InvalidOperationException.
/// </summary>
public interface IStoragePlanManager
{
    Task<DefaultSearchResults<StoragePlanListItemDTO>> GetAsync(PagingSearchDTO search, Guid? theaterScope);
    Task<StoragePlanDTO> GetByIdAsync(Guid id, Guid? theaterScope);
    Task<StoragePlanDTO> CreateAsync(SaveStoragePlanRequest request, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> UpdateAsync(SaveStoragePlanRequest request, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> SubmitAsync(Guid id, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> ApproveAsync(Guid id, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> RejectAsync(Guid id, string? reason, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> ReceiveAsync(ReceiveStoragePlanRequest request, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> CancelAsync(Guid id, Guid userId, Guid? theaterScope);
    Task<StoragePlanDTO> CreateFromLowStockAsync(CreatePlanFromLowStockRequest request, Guid userId, Guid? theaterScope);
}
