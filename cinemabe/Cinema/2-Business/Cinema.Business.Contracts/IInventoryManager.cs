using Cinema.Business.DTO.Inventory;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.Contracts;

/// <summary>
/// Warehouse stock for tracked food &amp; drinks (one quantity per product per theater). Every method takes the
/// caller's theater scope: null = admin (all theaters); otherwise anything outside that theater behaves as
/// not found (KeyNotFoundException).
/// </summary>
public interface IInventoryManager
{
    Task<DefaultSearchResults<InventoryItemDTO>> GetInventoryAsync(PagingSearchDTO search, Guid? theaterScope);
    Task<InventoryItemDTO> UpdateSettingsAsync(UpdateInventorySettingsRequest request, Guid userId, Guid? theaterScope);
    Task<InventoryItemDTO> RecordMovementAsync(RecordStockMovementRequest request, Guid userId, Guid? theaterScope);
    Task<InventoryItemDTO> RecordStockCountAsync(RecordStockCountRequest request, Guid userId, Guid? theaterScope);
    Task<DefaultSearchResults<StockMovementDTO>> GetMovementsAsync(PagingSearchDTO search, Guid? theaterScope);
}
