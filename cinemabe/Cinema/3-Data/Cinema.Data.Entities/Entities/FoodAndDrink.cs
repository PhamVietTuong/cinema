namespace Cinema.Data.Entities;
public class FoodAndDrink : BaseEntity
{
    /// <summary>The theater this item belongs to (food &amp; drinks are per-theater).</summary>
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;

    /// <summary>Opt-in stock tracking. Untracked items are sold without any stock checks.</summary>
    public bool TrackInventory { get; set; }
    /// <summary>Units in the theater warehouse. Only meaningful (and only changed) while tracked.</summary>
    public int QuantityOnHand { get; set; }
    /// <summary>Reorder signal: the item is "low" when <see cref="QuantityOnHand"/> is at or below this.</summary>
    public int LowStockThreshold { get; set; }
    /// <summary>Stock level a restock should bring the item back up to (0 = not set).</summary>
    public int TargetStockLevel { get; set; }
    /// <summary>A combo holds no stock itself; selling it deducts its <see cref="ComboItems"/> components.</summary>
    public bool IsCombo { get; set; }

    public ICollection<InvoiceFoodAndDrink> InvoiceFoodAndDrinks { get; set; } = new List<InvoiceFoodAndDrink>();
    /// <summary>Components of this item when it is a combo.</summary>
    public ICollection<ComboItem> ComboItems { get; set; } = new List<ComboItem>();
}
