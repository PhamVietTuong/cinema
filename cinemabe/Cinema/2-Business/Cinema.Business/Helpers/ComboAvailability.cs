using Cinema.Business.DTO.Catalog;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Helpers;

/// <summary>Computes the public stock view (AvailableQuantity / IsOutOfStock) of food &amp; drink items.</summary>
public static class ComboAvailability
{
    /// <summary>Public exposure cap: the storefront never reveals more than this many units.</summary>
    public const int PublicCap = 10;

    /// <summary>
    /// Fills the read-only inventory fields of <paramref name="dtos"/> (parallel to <paramref name="items"/>).
    /// Constant query count: at most one recipe read for the combos on the page plus one batched component read.
    /// </summary>
    public static async Task PopulateAsync(IApplicationUnitOfWork uow, IReadOnlyList<FoodAndDrink> items, IReadOnlyList<FoodAndDrinkDTO> dtos)
    {
        var comboIds = items.Where(i => i.IsCombo).Select(i => i.Id).Distinct().ToList();
        var recipes = new Dictionary<Guid, List<ComboItem>>();
        var components = new Dictionary<Guid, FoodAndDrink>();

        if (comboIds.Count > 0)
        {
            var rows = await uow.ComboItemStore.GetByCombosAsync(comboIds);
            recipes = rows.GroupBy(r => r.ComboId).ToDictionary(g => g.Key, g => g.ToList());
            var componentIds = rows.Select(r => r.ComponentId).Distinct().ToList();
            if (componentIds.Count > 0)
            {
                components = await uow.FoodAndDrinkStore.GetByIdsAsync(componentIds);
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            var recipe = recipes.TryGetValue(items[i].Id, out var found) ? found : new List<ComboItem>();
            var (available, outOfStock) = Compute(items[i], recipe, components);
            dtos[i].AvailableQuantity = available;
            dtos[i].IsOutOfStock = outOfStock;
        }
    }

    public static (int? AvailableQuantity, bool IsOutOfStock) Compute(
        FoodAndDrink item, IReadOnlyList<ComboItem> recipe, IReadOnlyDictionary<Guid, FoodAndDrink> components)
    {
        if (!item.IsCombo)
        {
            if (!item.TrackInventory)
            {
                return (null, false);
            }
            return (Math.Min(Math.Max(item.QuantityOnHand, 0), PublicCap), item.QuantityOnHand <= 0);
        }

        int? available = null;
        var anyComponentOff = false;
        foreach (var line in recipe)
        {
            if (!components.TryGetValue(line.ComponentId, out var component) || !component.IsAvailable)
            {
                anyComponentOff = true;
                continue;
            }
            if (!component.TrackInventory)
            {
                continue;
            }
            var possible = line.Quantity > 0 ? Math.Max(component.QuantityOnHand, 0) / line.Quantity : 0;
            available = available.HasValue ? Math.Min(available.Value, possible) : possible;
        }

        if (available.HasValue)
        {
            available = Math.Min(available.Value, PublicCap);
        }
        return (available, available == 0 || anyComponentOff);
    }
}
