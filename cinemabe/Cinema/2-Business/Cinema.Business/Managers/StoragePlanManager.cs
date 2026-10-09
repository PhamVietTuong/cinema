using Cinema.Business.Contracts;
using Cinema.Business.DTO.Inventory;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Extensions;
using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

public class StoragePlanManager : IStoragePlanManager
{
    private const string _concurrencyMessage = "The plan was modified by someone else, please reload.";
    private const int _lowStockPageSize = 5000;
    private const int _maxPlannedQuantity = 100000;

    private readonly IApplicationUnitOfWork _uow;

    public StoragePlanManager(IApplicationUnitOfWork uow)
    {
        _uow = uow;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<DefaultSearchResults<StoragePlanListItemDTO>> GetAsync(PagingSearchDTO search, Guid? theaterScope)
    {
        search ??= new PagingSearchDTO();
        var (page, pageSize) = PagingHelper.ResolvePaging(search);
        var filters = search.Filters;

        var criteria = new StoragePlanSearchCriteria(
            TheaterId: theaterScope ?? filters.GetGuid("theaterId"),
            Status: filters.GetEnum<StoragePlanStatus>("status"),
            Keyword: filters.GetString("keyword"),
            PageIndex: page - 1,
            PageSize: pageSize);

        var (rows, total) = await _uow.StoragePlanStore.SearchAsync(criteria);

        // Batched id -> name lookups (one query each), so the page costs a fixed number of round-trips.
        var theaterNames = await _uow.TheaterStore.GetNamesByIdsAsync(rows.Select(r => r.TheaterId).Distinct().ToList());
        var userNames = await _uow.UserStore.GetNamesByIdsAsync(rows.Select(r => r.CreatedByUserId).Distinct().ToList());

        var results = rows.Select(r => new StoragePlanListItemDTO
        {
            Id = r.Id,
            Code = r.Code,
            TheaterId = r.TheaterId,
            TheaterName = theaterNames.TryGetValue(r.TheaterId, out var theaterName) ? theaterName : null,
            Status = r.Status,
            TargetDate = r.TargetDate,
            Supplier = r.Supplier,
            ItemCount = r.ItemCount,
            TotalPlannedQuantity = r.TotalPlannedQuantity,
            CreatedByName = userNames.TryGetValue(r.CreatedByUserId, out var userName) ? userName : null,
            CreationTime = r.CreationTime
        }).ToList();

        return new DefaultSearchResults<StoragePlanListItemDTO>
        {
            Results = results,
            TotalCount = total,
            CountPerPage = pageSize,
            Page = page
        };
    }

    public async Task<StoragePlanDTO> GetByIdAsync(Guid id, Guid? theaterScope)
    {
        var plan = await LoadPlanAsync(id, theaterScope);
        return await ToDtoAsync(plan);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    public async Task<StoragePlanDTO> CreateAsync(SaveStoragePlanRequest request, Guid userId, Guid? theaterScope)
    {
        if (theaterScope.HasValue && request.TheaterId != theaterScope.Value)
        {
            throw new KeyNotFoundException($"{nameof(Theater)} {request.TheaterId} not found.");
        }

        await ValidateAsync(request, request.TheaterId);

        var plan = new StoragePlan
        {
            Code = await GenerateCodeAsync(),
            TheaterId = request.TheaterId,
            Status = StoragePlanStatus.Draft,
            TargetDate = request.TargetDate,
            Supplier = NormalizeText(request.Supplier),
            Note = NormalizeText(request.Note),
            CreatedByUserId = userId
        };
        foreach (var item in request.Items)
        {
            plan.Items.Add(ToEntity(item));
        }

        await _uow.StoragePlanStore.CreateAsync(plan);
        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> UpdateAsync(SaveStoragePlanRequest request, Guid userId, Guid? theaterScope)
    {
        if (!request.Id.HasValue || request.Id.Value == Guid.Empty)
        {
            throw new InvalidOperationException("The plan id is required.");
        }

        var plan = await LoadPlanAsync(request.Id.Value, theaterScope);
        if (plan.Status != StoragePlanStatus.Draft && plan.Status != StoragePlanStatus.Rejected)
        {
            throw IllegalTransition(plan, "edited");
        }
        if (request.TheaterId != plan.TheaterId)
        {
            throw new InvalidOperationException("The theater of a plan cannot be changed.");
        }

        await ValidateAsync(request, plan.TheaterId);

        plan.Status = StoragePlanStatus.Draft;
        plan.RejectionReason = null;
        plan.TargetDate = request.TargetDate;
        plan.Supplier = NormalizeText(request.Supplier);
        plan.Note = NormalizeText(request.Note);

        // Replace the item list, reusing the row of a food that stays (the (plan, food) pair is unique).
        var requested = request.Items.ToDictionary(i => i.FoodAndDrinkId);
        foreach (var existing in plan.Items.ToList())
        {
            if (!requested.ContainsKey(existing.FoodAndDrinkId))
            {
                plan.Items.Remove(existing);
            }
        }
        foreach (var item in request.Items)
        {
            var existing = plan.Items.FirstOrDefault(i => i.FoodAndDrinkId == item.FoodAndDrinkId);
            if (existing == null)
            {
                plan.Items.Add(ToEntity(item));
            }
            else
            {
                existing.PlannedQuantity = item.PlannedQuantity;
                existing.UnitCost = item.UnitCost;
                existing.Note = NormalizeText(item.Note);
            }
        }

        await SaveAsync(plan);
        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> SubmitAsync(Guid id, Guid userId, Guid? theaterScope)
    {
        var plan = await LoadPlanAsync(id, theaterScope);
        if (plan.Status != StoragePlanStatus.Draft)
        {
            throw IllegalTransition(plan, "submitted");
        }
        if (plan.Items.Count == 0)
        {
            throw new InvalidOperationException("A plan needs at least one item before it can be submitted.");
        }

        plan.Status = StoragePlanStatus.Submitted;
        plan.SubmittedAt = DateTime.UtcNow;
        await SaveAsync(plan);
        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> ApproveAsync(Guid id, Guid userId, Guid? theaterScope)
    {
        var plan = await LoadPlanAsync(id, theaterScope);
        if (plan.Status != StoragePlanStatus.Submitted)
        {
            throw IllegalTransition(plan, "approved");
        }

        plan.Status = StoragePlanStatus.Approved;
        plan.DecidedByUserId = userId;
        plan.DecidedAt = DateTime.UtcNow;
        plan.RejectionReason = null;
        await SaveAsync(plan);
        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> RejectAsync(Guid id, string? reason, Guid userId, Guid? theaterScope)
    {
        var plan = await LoadPlanAsync(id, theaterScope);
        if (plan.Status != StoragePlanStatus.Submitted)
        {
            throw IllegalTransition(plan, "rejected");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A reason is required to reject a plan.");
        }

        plan.Status = StoragePlanStatus.Rejected;
        plan.DecidedByUserId = userId;
        plan.DecidedAt = DateTime.UtcNow;
        plan.RejectionReason = reason.Trim();
        await SaveAsync(plan);
        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> CancelAsync(Guid id, Guid userId, Guid? theaterScope)
    {
        var plan = await LoadPlanAsync(id, theaterScope);
        if (plan.Status != StoragePlanStatus.Draft
            && plan.Status != StoragePlanStatus.Submitted
            && plan.Status != StoragePlanStatus.Approved)
        {
            throw IllegalTransition(plan, "cancelled");
        }

        plan.Status = StoragePlanStatus.Cancelled;
        await SaveAsync(plan);
        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> ReceiveAsync(ReceiveStoragePlanRequest request, Guid userId, Guid? theaterScope)
    {
        var plan = await LoadPlanAsync(request.Id, theaterScope);
        if (plan.Status != StoragePlanStatus.Approved)
        {
            throw IllegalTransition(plan, "received");
        }

        var actualById = new Dictionary<Guid, int>();
        foreach (var line in request.Items ?? new List<ReceiveStoragePlanItem>())
        {
            if (line.ReceivedQuantity < 0)
            {
                throw new InvalidOperationException("A received quantity cannot be negative.");
            }
            if (plan.Items.All(i => i.Id != line.StoragePlanItemId))
            {
                throw new InvalidOperationException($"Item {line.StoragePlanItemId} is not part of plan {plan.Code}.");
            }
            actualById[line.StoragePlanItemId] = line.ReceivedQuantity;
        }

        await _uow.BeginTransactionAsync();
        try
        {
            // The foods must still be tracked: stock of an untracked item is meaningless and never changed.
            var foods = await _uow.FoodAndDrinkStore.GetByIdsAsync(plan.Items.Select(i => i.FoodAndDrinkId).ToList());
            foreach (var item in plan.Items)
            {
                if (!foods.TryGetValue(item.FoodAndDrinkId, out var food) || !food.TrackInventory || food.IsCombo)
                {
                    var name = foods.TryGetValue(item.FoodAndDrinkId, out var named) ? named.Name : item.FoodAndDrinkId.ToString();
                    throw new InvalidOperationException($"Inventory tracking is no longer enabled for '{name}'.");
                }
                item.ReceivedQuantity = actualById.TryGetValue(item.Id, out var actual) ? actual : item.PlannedQuantity;
            }

            plan.Status = StoragePlanStatus.Received;
            plan.ReceivedByUserId = userId;
            plan.ReceivedAt = DateTime.UtcNow;

            // Saved first: the rowversion guard makes a second concurrent receive fail here, before any stock moves.
            await SaveAsync(plan);

            // Ascending item order keeps concurrent receives from deadlocking on the stock rows.
            foreach (var item in plan.Items.Where(i => i.ReceivedQuantity > 0).OrderBy(i => i.FoodAndDrinkId))
            {
                var quantity = item.ReceivedQuantity!.Value;
                var applied = await _uow.FoodAndDrinkStore.TryApplyStockDeltaAsync(item.FoodAndDrinkId, quantity);
                if (!applied)
                {
                    throw new InvalidOperationException("Stock could not be updated.");
                }

                await _uow.StockMovementStore.CreateAsync(new StockMovement
                {
                    FoodAndDrinkId = item.FoodAndDrinkId,
                    TheaterId = plan.TheaterId,
                    Type = StockMovementType.Receive,
                    Quantity = quantity,
                    ReasonCode = null,
                    StoragePlanId = plan.Id,
                    UserId = userId,
                    Reason = plan.Code
                });
            }

            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return await ToDtoAsync(plan);
    }

    public async Task<StoragePlanDTO> CreateFromLowStockAsync(CreatePlanFromLowStockRequest request, Guid userId, Guid? theaterScope)
    {
        if (theaterScope.HasValue && request.TheaterId != theaterScope.Value)
        {
            throw new KeyNotFoundException($"{nameof(Theater)} {request.TheaterId} not found.");
        }

        // One query: tracked, non-combo items at or below their threshold (combos are excluded by the store).
        var criteria = new InventorySearchCriteria(
            TheaterId: request.TheaterId,
            Keyword: null,
            TrackedOnly: true,
            LowStock: true,
            OutOfStock: null,
            SortByQuantity: false,
            Ascending: true,
            PageIndex: 0,
            PageSize: _lowStockPageSize);
        var (lowItems, _) = await _uow.FoodAndDrinkStore.SearchInventoryAsync(criteria);

        var candidates = lowItems.Where(f => f.TargetStockLevel > 0).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No low-stock items with a target level.");
        }

        var save = new SaveStoragePlanRequest
        {
            TheaterId = request.TheaterId,
            TargetDate = DateTime.UtcNow.Date,
            Items = candidates.Select(f => new SaveStoragePlanItem
            {
                FoodAndDrinkId = f.Id,
                PlannedQuantity = Math.Min(_maxPlannedQuantity, Math.Max(1, f.TargetStockLevel - f.QuantityOnHand))
            }).ToList()
        };
        return await CreateAsync(save, userId, theaterScope);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<StoragePlan> LoadPlanAsync(Guid id, Guid? theaterScope)
    {
        var plan = await _uow.StoragePlanStore.GetWithItemsAsync(id);
        // Out-of-scope rows are indistinguishable from missing ones, so staff cannot probe other theaters.
        if (plan == null || (theaterScope.HasValue && plan.TheaterId != theaterScope.Value))
        {
            throw new KeyNotFoundException($"{nameof(StoragePlan)} {id} not found.");
        }
        return plan;
    }

    private async Task SaveAsync(StoragePlan plan)
    {
        try
        {
            await _uow.StoragePlanStore.UpdateAsync(plan);
        }
        catch (ConcurrencyConflictException)
        {
            throw new InvalidOperationException(_concurrencyMessage);
        }
    }

    private static InvalidOperationException IllegalTransition(StoragePlan plan, string action)
    {
        return new InvalidOperationException($"A plan that is {plan.Status} cannot be {action}.");
    }

    private async Task ValidateAsync(SaveStoragePlanRequest request, Guid theaterId)
    {
        if (request.TargetDate.Date < DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException("The target date cannot be in the past.");
        }
        if (request.Supplier != null && request.Supplier.Length > 200)
        {
            throw new InvalidOperationException("The supplier is too long (max 200 characters).");
        }
        if (request.Note != null && request.Note.Length > 1000)
        {
            throw new InvalidOperationException("The note is too long (max 1000 characters).");
        }
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("A plan needs at least one item.");
        }

        var ids = request.Items.Select(i => i.FoodAndDrinkId).ToList();
        if (ids.Distinct().Count() != ids.Count)
        {
            throw new InvalidOperationException("The same item cannot appear twice in a plan.");
        }

        foreach (var item in request.Items)
        {
            if (item.PlannedQuantity < 1 || item.PlannedQuantity > _maxPlannedQuantity)
            {
                throw new InvalidOperationException($"A planned quantity must be between 1 and {_maxPlannedQuantity}.");
            }
            if (item.UnitCost.HasValue && item.UnitCost.Value < 0)
            {
                throw new InvalidOperationException("A unit cost cannot be negative.");
            }
        }

        var foods = await _uow.FoodAndDrinkStore.GetByIdsAsync(ids);
        foreach (var id in ids)
        {
            if (!foods.TryGetValue(id, out var food) || food.TheaterId != theaterId)
            {
                throw new InvalidOperationException($"Item {id} does not belong to this theater.");
            }
            if (food.IsCombo)
            {
                throw new InvalidOperationException($"'{food.Name}' is a combo; plan its component items instead.");
            }
            if (!food.TrackInventory)
            {
                throw new InvalidOperationException($"Inventory tracking is not enabled for '{food.Name}'.");
            }
        }
    }

    private async Task<string> GenerateCodeAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"SP{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(0, 10000):D4}";
            if (!await _uow.StoragePlanStore.ExistsAsync(p => p.Code == code))
            {
                return code;
            }
        }
        throw new InvalidOperationException("Could not generate a unique plan code, please try again.");
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static StoragePlanItem ToEntity(SaveStoragePlanItem item)
    {
        return new StoragePlanItem
        {
            FoodAndDrinkId = item.FoodAndDrinkId,
            PlannedQuantity = item.PlannedQuantity,
            UnitCost = item.UnitCost,
            Note = NormalizeText(item.Note)
        };
    }

    /// <summary>Maps a plan to its DTO using four batched lookups (foods, users, theater) regardless of item count.</summary>
    private async Task<StoragePlanDTO> ToDtoAsync(StoragePlan plan)
    {
        var foods = await _uow.FoodAndDrinkStore.GetByIdsAsync(plan.Items.Select(i => i.FoodAndDrinkId).Distinct().ToList());

        var userIds = new List<Guid> { plan.CreatedByUserId };
        if (plan.DecidedByUserId.HasValue)
        {
            userIds.Add(plan.DecidedByUserId.Value);
        }
        if (plan.ReceivedByUserId.HasValue)
        {
            userIds.Add(plan.ReceivedByUserId.Value);
        }
        var userNames = await _uow.UserStore.GetNamesByIdsAsync(userIds.Distinct().ToList());
        var theaterNames = await _uow.TheaterStore.GetNamesByIdsAsync(new[] { plan.TheaterId });

        return new StoragePlanDTO
        {
            Id = plan.Id,
            Code = plan.Code,
            TheaterId = plan.TheaterId,
            TheaterName = theaterNames.TryGetValue(plan.TheaterId, out var theaterName) ? theaterName : null,
            Status = plan.Status,
            TargetDate = plan.TargetDate,
            Supplier = plan.Supplier,
            ItemCount = plan.Items.Count,
            TotalPlannedQuantity = plan.Items.Sum(i => i.PlannedQuantity),
            CreatedByName = userNames.TryGetValue(plan.CreatedByUserId, out var createdBy) ? createdBy : null,
            CreationTime = plan.CreationTime,
            Note = plan.Note,
            SubmittedAt = plan.SubmittedAt,
            DecidedByName = plan.DecidedByUserId.HasValue && userNames.TryGetValue(plan.DecidedByUserId.Value, out var decidedBy) ? decidedBy : null,
            DecidedAt = plan.DecidedAt,
            RejectionReason = plan.RejectionReason,
            ReceivedByName = plan.ReceivedByUserId.HasValue && userNames.TryGetValue(plan.ReceivedByUserId.Value, out var receivedBy) ? receivedBy : null,
            ReceivedAt = plan.ReceivedAt,
            Items = plan.Items.Select(i => new StoragePlanItemDTO
            {
                Id = i.Id,
                FoodAndDrinkId = i.FoodAndDrinkId,
                FoodAndDrinkName = foods.TryGetValue(i.FoodAndDrinkId, out var food) ? food.Name : null,
                QuantityOnHand = foods.TryGetValue(i.FoodAndDrinkId, out var stocked) ? stocked.QuantityOnHand : 0,
                PlannedQuantity = i.PlannedQuantity,
                ReceivedQuantity = i.ReceivedQuantity,
                UnitCost = i.UnitCost,
                Note = i.Note
            }).ToList()
        };
    }
}
