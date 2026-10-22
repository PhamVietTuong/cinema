namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of one food or drink line of a pickup order.</summary>
public sealed class PickupItemRow
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
