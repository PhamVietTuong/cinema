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

/// <summary>Filter + page for the stock ledger (newest first). Page is 0-based.</summary>
public record StockMovementSearchCriteria(
    Guid? TheaterId,
    Guid? FoodAndDrinkId,
    StockMovementType? Type,
    DateTime? From,
    DateTime? To,
    int PageIndex,
    int PageSize);
