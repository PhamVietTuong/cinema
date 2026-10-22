namespace Cinema.Data.Contracts;

/// <summary>Net units sold (Sale + SaleReversal) for one item on one invoice. Negative = still owed back to the shelf.</summary>
public record StockNetQuantity(Guid InvoiceId, Guid FoodAndDrinkId, int NetQuantity);
