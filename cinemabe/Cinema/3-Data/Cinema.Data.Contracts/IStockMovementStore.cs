using Cinema.Data.Entities;

namespace Cinema.Data.Contracts;

/// <summary>Net units sold (Sale + SaleReversal) for one item on one invoice. Negative = still owed back to the shelf.</summary>
public record StockNetQuantity(Guid InvoiceId, Guid FoodAndDrinkId, int NetQuantity);

public interface IStockMovementStore : IGenericStore<StockMovement>
{
    /// <summary>
    /// One grouped query over the ledger: SUM(Quantity) of Sale and SaleReversal rows per (invoice, item).
    /// Restocking is driven by this, never by the current combo recipe, and is idempotent.
    /// </summary>
    Task<List<StockNetQuantity>> GetNetSaleQuantitiesAsync(IReadOnlyCollection<Guid> invoiceIds);
}
