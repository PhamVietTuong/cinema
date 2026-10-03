using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class FoodAndDrinkDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; }

    // Read-only inventory/combo view. Deliberately absent from the Create/Update requests: UpdateAsync
    // patches the entity from every same-named property, so these must never be writable from there.
    public bool TrackInventory { get; set; }
    public bool IsCombo { get; set; }

    /// <summary>Units that can be sold right now, capped at 10 for public exposure. Null = unlimited.</summary>
    public int? AvailableQuantity { get; set; }

    /// <summary>Tracked item with nothing on hand; for a combo also true when any component is switched off.</summary>
    public bool IsOutOfStock { get; set; }
}

public class CreateFoodAndDrinkRequest
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class UpdateFoodAndDrinkRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
}
