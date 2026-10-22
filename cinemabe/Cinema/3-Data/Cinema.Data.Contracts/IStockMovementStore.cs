using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

public interface IStockMovementStore : IGenericStore<StockMovement>
{
    /// <summary>
    /// One grouped query over the ledger: SUM(Quantity) of Sale and SaleReversal rows per (invoice, item).
    /// Restocking is driven by this, never by the current combo recipe, and is idempotent.
    /// </summary>
    Task<List<StockNetQuantity>> GetNetSaleQuantitiesAsync(IReadOnlyCollection<Guid> invoiceIds);
}
