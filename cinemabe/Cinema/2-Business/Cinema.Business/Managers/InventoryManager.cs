using Cinema.Business.Contracts;
using Cinema.Business.DTO.Inventory;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class InventoryManager : IInventoryManager
{
    private const string _openingBalanceReason = "Opening balance";
    private const string _negativeStockMessage = "Stock cannot go below zero.";

    private readonly IApplicationUnitOfWork _uow;

    public InventoryManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<DefaultSearchResults<InventoryItemDTO>> GetInventoryAsync(PagingSearchDTO search, Guid? theaterScope)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var filters = search.Filters;

        var sortByQuantity = search.Sort != null
            && string.Equals(search.Sort.Field, nameof(InventoryItemDTO.QuantityOnHand), StringComparison.OrdinalIgnoreCase);
        var ascending = search.Sort == null || string.IsNullOrEmpty(search.Sort.Field) || search.Sort.Ascending;

        var criteria = new InventorySearchCriteria(
            TheaterId: theaterScope ?? filters.GetGuid("theaterId"),
            Keyword: filters.GetString("keyword"),
            TrackedOnly: filters.GetBool("trackedOnly") == true,
            LowStock: filters.GetBool("lowStock"),
            OutOfStock: filters.GetBool("outOfStock"),
            SortByQuantity: sortByQuantity,
            Ascending: ascending,
            PageIndex: page - 1,
            PageSize: pageSize);

        var (items, total) = await _uow.FoodAndDrinkStore.SearchInventoryAsync(criteria);
        return new DefaultSearchResults<InventoryItemDTO>
        {
            Results = items.Select(ToDTO).ToList(),
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<DefaultSearchResults<StockMovementDTO>> GetMovementsAsync(PagingSearchDTO search, Guid? theaterScope)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var filters = search.Filters;

        var to = filters.GetDateTime("to");
        if (to.HasValue && to.Value.TimeOfDay == TimeSpan.Zero)
        {
            // A bare date means "through the end of that day" (SQL datetime resolution is ~3 ms).
            to = to.Value.AddDays(1).AddMilliseconds(-3);
        }

        var criteria = new StockMovementSearchCriteria(
            TheaterId: theaterScope ?? filters.GetGuid("theaterId"),
            FoodAndDrinkId: filters.GetGuid("foodAndDrinkId"),
            Type: filters.GetEnum<StockMovementType>("type"),
            From: filters.GetDateTime("from"),
            To: to,
            PageIndex: page - 1,
            PageSize: pageSize);

        var (movements, total) = await _uow.StockMovementStore.SearchAsync(criteria);

        // Batched id -> label lookups (one query each), so the page costs a fixed number of round-trips.
        var itemIds = movements.Select(m => m.FoodAndDrinkId).Distinct().ToList();
        var invoiceIds = movements.Where(m => m.InvoiceId.HasValue).Select(m => m.InvoiceId!.Value).Distinct().ToList();
        var planIds = movements.Where(m => m.StoragePlanId.HasValue).Select(m => m.StoragePlanId!.Value).Distinct().ToList();
        var userIds = movements.Where(m => m.UserId.HasValue).Select(m => m.UserId!.Value).Distinct().ToList();

        var items = await _uow.FoodAndDrinkStore.GetByIdsAsync(itemIds);
        var invoiceCodes = await _uow.InvoiceStore.GetCodesByIdsAsync(invoiceIds);
        var planCodes = await _uow.StoragePlanStore.GetCodesByIdsAsync(planIds);
        var userNames = await _uow.UserStore.GetNamesByIdsAsync(userIds);

        var results = movements.Select(m => new StockMovementDTO
        {
            Id = m.Id,
            FoodAndDrinkId = m.FoodAndDrinkId,
            FoodAndDrinkName = items.TryGetValue(m.FoodAndDrinkId, out var item) ? item.Name : null,
            TheaterId = m.TheaterId,
            Type = m.Type,
            Quantity = m.Quantity,
            ReasonCode = m.ReasonCode,
            Reason = m.Reason,
            InvoiceId = m.InvoiceId,
            InvoiceCode = m.InvoiceId.HasValue && invoiceCodes.TryGetValue(m.InvoiceId.Value, out var invoiceCode) ? invoiceCode : null,
            StoragePlanId = m.StoragePlanId,
            StoragePlanCode = m.StoragePlanId.HasValue && planCodes.TryGetValue(m.StoragePlanId.Value, out var planCode) ? planCode : null,
            UserId = m.UserId,
            UserName = m.UserId.HasValue && userNames.TryGetValue(m.UserId.Value, out var userName) ? userName : null,
            CreationTime = m.CreationTime
        }).ToList();

        return new DefaultSearchResults<StockMovementDTO>
        {
            Results = results,
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    public async Task<InventoryItemDTO> UpdateSettingsAsync(UpdateInventorySettingsRequest request, Guid userId, Guid? theaterScope)
    {
        // Tracked on purpose: EF then writes only the settings columns we change here, never QuantityOnHand.
        var item = await _uow.FoodAndDrinkStore.GetByIdAsync(request.FoodAndDrinkId);
        EnsureInScope(item, request.FoodAndDrinkId, theaterScope);
        if (item!.IsCombo)
        {
            throw new InvalidOperationException("A combo holds no stock of its own; track its component items instead.");
        }

        var enablingTracking = request.TrackInventory && !item.TrackInventory;
        if (enablingTracking && request.OpeningQuantity == null)
        {
            throw new InvalidOperationException("An opening quantity is required when inventory tracking is turned on.");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            item.TrackInventory = request.TrackInventory;
            item.LowStockThreshold = request.LowStockThreshold;
            item.TargetStockLevel = request.TargetStockLevel;
            await _uow.FoodAndDrinkStore.UpdateAsync(item);

            if (enablingTracking)
            {
                // The one place a quantity is "set": still routed through the guarded delta path.
                var delta = request.OpeningQuantity!.Value - item.QuantityOnHand;
                if (delta != 0)
                {
                    await ApplyAndRecordAsync(item.Id, item.TheaterId, delta, StockMovementType.Adjust,
                        StockReasonCode.OpeningBalance, _openingBalanceReason, userId);
                }
            }

            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return await LoadDtoAsync(item.Id);
    }

    public async Task<InventoryItemDTO> RecordMovementAsync(RecordStockMovementRequest request, Guid userId, Guid? theaterScope)
    {
        if (request.Type != StockMovementType.Adjust && request.Type != StockMovementType.Waste)
        {
            throw new InvalidOperationException("Only Adjust and Waste movements can be recorded manually.");
        }
        if (request.ReasonCode == StockReasonCode.OpeningBalance)
        {
            throw new InvalidOperationException("The OpeningBalance reason is reserved for the system.");
        }
        if (request.ReasonCode == StockReasonCode.Other && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new InvalidOperationException("A note is required when the reason is Other.");
        }

        int signedQuantity;
        if (request.Type == StockMovementType.Waste)
        {
            if (request.Quantity <= 0)
            {
                throw new InvalidOperationException("A waste quantity must be greater than zero.");
            }
            signedQuantity = -request.Quantity;
        }
        else
        {
            if (request.Quantity == 0)
            {
                throw new InvalidOperationException("An adjustment cannot be zero.");
            }
            signedQuantity = request.Quantity;
        }

        var item = await LoadTrackedItemAsync(request.FoodAndDrinkId, theaterScope);
        var reason = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await _uow.BeginTransactionAsync();
        try
        {
            await ApplyAndRecordAsync(item.Id, item.TheaterId, signedQuantity, request.Type, request.ReasonCode, reason, userId);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return await LoadDtoAsync(item.Id);
    }

    public async Task<InventoryItemDTO> RecordStockCountAsync(RecordStockCountRequest request, Guid userId, Guid? theaterScope)
    {
        var item = await LoadTrackedItemAsync(request.FoodAndDrinkId, theaterScope);
        var difference = request.CountedQuantity - item.QuantityOnHand;
        if (difference == 0)
        {
            return ToDTO(item);
        }

        var reason = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _uow.BeginTransactionAsync();
        try
        {
            await ApplyAndRecordAsync(item.Id, item.TheaterId, difference, StockMovementType.Adjust,
                StockReasonCode.StockCountCorrection, reason, userId);
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return await LoadDtoAsync(item.Id);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>The only stock-write path: guarded delta on the item plus its ledger row. Call inside a transaction.</summary>
    private async Task ApplyAndRecordAsync(Guid itemId, Guid theaterId, int delta, StockMovementType type,
        StockReasonCode reasonCode, string? reason, Guid userId)
    {
        var applied = await _uow.FoodAndDrinkStore.TryApplyStockDeltaAsync(itemId, delta);
        if (!applied)
        {
            throw new InvalidOperationException(_negativeStockMessage);
        }

        await _uow.StockMovementStore.CreateAsync(new StockMovement
        {
            FoodAndDrinkId = itemId,
            TheaterId = theaterId,
            Type = type,
            Quantity = delta,
            ReasonCode = reasonCode,
            Reason = reason,
            UserId = userId
        });
    }

    /// <summary>Loads (untracked) an in-scope, tracked, non-combo item or throws.</summary>
    private async Task<FoodAndDrink> LoadTrackedItemAsync(Guid id, Guid? theaterScope)
    {
        var items = await _uow.FoodAndDrinkStore.GetByIdsAsync(new[] { id });
        items.TryGetValue(id, out var item);
        EnsureInScope(item, id, theaterScope);
        if (item!.IsCombo)
        {
            throw new InvalidOperationException("A combo holds no stock of its own; its component items are tracked instead.");
        }
        if (!item.TrackInventory)
        {
            throw new InvalidOperationException("Inventory tracking is not enabled for this item.");
        }
        return item;
    }

    private static void EnsureInScope(FoodAndDrink? item, Guid id, Guid? theaterScope)
    {
        // Out-of-scope rows are indistinguishable from missing ones, so staff cannot probe other theaters.
        if (item == null || (theaterScope.HasValue && item.TheaterId != theaterScope.Value))
        {
            throw new KeyNotFoundException($"{nameof(FoodAndDrink)} {id} not found.");
        }
    }

    private async Task<InventoryItemDTO> LoadDtoAsync(Guid id)
    {
        var items = await _uow.FoodAndDrinkStore.GetByIdsAsync(new[] { id });
        if (!items.TryGetValue(id, out var item))
        {
            throw new KeyNotFoundException($"{nameof(FoodAndDrink)} {id} not found.");
        }
        return ToDTO(item);
    }

    private static InventoryItemDTO ToDTO(FoodAndDrink item)
    {
        return new InventoryItemDTO
        {
            Id = item.Id,
            TheaterId = item.TheaterId,
            Name = item.Name,
            ImageUrl = item.ImageUrl,
            Price = item.Price,
            IsAvailable = item.IsAvailable,
            TrackInventory = item.TrackInventory,
            QuantityOnHand = item.QuantityOnHand,
            LowStockThreshold = item.LowStockThreshold,
            TargetStockLevel = item.TargetStockLevel
        };
    }
}
