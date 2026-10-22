using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.Catalog;

public class ComboComponentDTO
{
    public Guid ComponentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool TrackInventory { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>Only set for tracked components.</summary>
    public int? QuantityOnHand { get; set; }
}
