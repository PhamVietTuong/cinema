using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Filter + sort + page for the warehouse stock list. Combos are always excluded. Page is 0-based.</summary>
public record InventorySearchCriteria(
    Guid? TheaterId,
    string? Keyword,
    bool TrackedOnly,
    bool? LowStock,
    bool? OutOfStock,
    bool SortByQuantity,
    bool Ascending,
    int PageIndex,
    int PageSize);

/// <summary>Filter + page for the storage plan list (newest first). Page is 0-based.</summary>
public record StoragePlanSearchCriteria(
    Guid? TheaterId,
    StoragePlanStatus? Status,
    string? Keyword,
    int PageIndex,
    int PageSize);

/// <summary>A storage plan list row with its item totals computed in SQL.</summary>
public record StoragePlanListRow(
    Guid Id,
    string Code,
    Guid TheaterId,
    StoragePlanStatus Status,
    DateTime TargetDate,
    string? Supplier,
    int ItemCount,
    int TotalPlannedQuantity,
    Guid CreatedByUserId,
    DateTime CreationTime);

/// <summary>Filter + page for the stock ledger (newest first). Page is 0-based.</summary>
public record StockMovementSearchCriteria(
    Guid? TheaterId,
    Guid? FoodAndDrinkId,
    StockMovementType? Type,
    DateTime? From,
    DateTime? To,
    int PageIndex,
    int PageSize);
