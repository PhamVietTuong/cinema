using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

/// <summary>
/// Puts food stock back for invoices that did not complete (cancelled, expired, refunded, deleted while
/// Pending). Driven purely by the stock ledger — never by the current combo recipe — so history stays
/// correct after recipe edits, and idempotent: once a SaleReversal is recorded the net is 0 and a repeat
/// call does nothing. The caller owns the surrounding transaction.
/// </summary>
internal static class FoodStockRestorer
{
    public static async Task RestoreAsync(IApplicationUnitOfWork uow, IReadOnlyCollection<Guid> invoiceIds, string reason, Guid? userId)
    {
        if (invoiceIds.Count == 0)
        {
            return;
        }

        // One grouped ledger query for every invoice. Negative net = units still owed back to the shelf.
        var owed = (await uow.StockMovementStore.GetNetSaleQuantitiesAsync(invoiceIds))
            .Where(n => n.NetQuantity < 0)
            .ToList();
        if (owed.Count == 0)
        {
            return;
        }

        var foodIds = owed.Select(n => n.FoodAndDrinkId).Distinct().ToList();
        var foods = await uow.FoodAndDrinkStore.GetByIdsAsync(foodIds);

        // Per-item stock updates are atomic conditional UPDATEs (one per distinct item); the ledger rows
        // are written in a single batch afterwards.
        var movements = new List<StockMovement>();
        foreach (var net in owed.OrderBy(n => n.FoodAndDrinkId))
        {
            // An item that was deleted or is no longer stock-tracked has no shelf quantity to restore.
            if (!foods.TryGetValue(net.FoodAndDrinkId, out var food) || !food.TrackInventory)
            {
                continue;
            }

            var quantity = -net.NetQuantity;
            if (!await uow.FoodAndDrinkStore.TryApplyStockDeltaAsync(food.Id, quantity))
            {
                continue;
            }

            movements.Add(new StockMovement
            {
                FoodAndDrinkId = food.Id,
                TheaterId      = food.TheaterId,
                Type           = StockMovementType.SaleReversal,
                Quantity       = quantity,
                Reason         = reason,
                InvoiceId      = net.InvoiceId,
                UserId         = userId,
            });
        }

        if (movements.Count > 0)
        {
            await uow.StockMovementStore.CreateRangeAsync(movements);
        }
    }
}
