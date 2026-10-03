using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

public class ComboManager : IComboManager
{
    private readonly IApplicationUnitOfWork _uow;

    public ComboManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<ComboComponentDTO>> GetComponentsAsync(Guid comboId)
    {
        var recipe = await _uow.ComboItemStore.GetByCombosAsync(new[] { comboId });
        if (recipe.Count == 0)
        {
            return new List<ComboComponentDTO>();
        }

        var components = await _uow.FoodAndDrinkStore.GetByIdsAsync(recipe.Select(r => r.ComponentId).Distinct().ToList());
        var result = new List<ComboComponentDTO>();
        foreach (var line in recipe)
        {
            if (!components.TryGetValue(line.ComponentId, out var component))
            {
                continue;
            }
            result.Add(new ComboComponentDTO
            {
                ComponentId = component.Id,
                Name = component.Name,
                Quantity = line.Quantity,
                TrackInventory = component.TrackInventory,
                IsAvailable = component.IsAvailable,
                QuantityOnHand = component.TrackInventory ? component.QuantityOnHand : null
            });
        }
        return result.OrderBy(c => c.Name).ToList();
    }

    public async Task SaveAsync(SaveComboRequest request)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var combo = await _uow.FoodAndDrinkStore.GetByIdAsync(request.ComboId);
            if (combo == null)
            {
                throw new KeyNotFoundException($"FoodAndDrink {request.ComboId} not found.");
            }

            if (!request.IsCombo)
            {
                await _uow.ComboItemStore.ReplaceForComboAsync(combo.Id, new List<ComboItem>());
                combo.IsCombo = false;
                await _uow.FoodAndDrinkStore.UpdateAsync(combo);
                await _uow.CommitTransactionAsync();
                return;
            }

            var lines = request.Components ?? new List<ComboComponentItem>();
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("A combo needs at least one component.");
            }
            if (lines.Select(l => l.ComponentId).Distinct().Count() != lines.Count)
            {
                throw new InvalidOperationException("A component can only appear once in a combo.");
            }
            if (lines.Any(l => l.ComponentId == combo.Id))
            {
                throw new InvalidOperationException("A combo cannot contain itself.");
            }
            if (combo.TrackInventory)
            {
                throw new InvalidOperationException("A combo holds no stock: turn off inventory tracking on it first.");
            }

            var usedIn = await _uow.ComboItemStore.GetCombosUsingAsync(combo.Id);
            if (usedIn.Count > 0)
            {
                throw new InvalidOperationException($"This item is a component of combo(s): {string.Join(", ", usedIn.Select(u => u.ComboName))}. Combos cannot be nested.");
            }

            var components = await _uow.FoodAndDrinkStore.GetByIdsAsync(lines.Select(l => l.ComponentId).ToList());
            foreach (var line in lines)
            {
                if (!components.TryGetValue(line.ComponentId, out var component))
                {
                    throw new KeyNotFoundException($"FoodAndDrink {line.ComponentId} not found.");
                }
                if (component.TheaterId != combo.TheaterId)
                {
                    throw new InvalidOperationException($"Component '{component.Name}' belongs to a different theater than the combo.");
                }
                if (component.IsCombo)
                {
                    throw new InvalidOperationException($"Component '{component.Name}' is itself a combo; combos cannot be nested.");
                }
            }

            var items = lines.Select(l => new ComboItem { ComboId = combo.Id, ComponentId = l.ComponentId, Quantity = l.Quantity }).ToList();
            await _uow.ComboItemStore.ReplaceForComboAsync(combo.Id, items);
            combo.IsCombo = true;
            await _uow.FoodAndDrinkStore.UpdateAsync(combo);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }
}
