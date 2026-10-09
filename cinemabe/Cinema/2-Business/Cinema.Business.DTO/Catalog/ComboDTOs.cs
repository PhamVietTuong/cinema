using System.ComponentModel.DataAnnotations;
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

public class ComboComponentItem
{
    [NotEmptyGuid]
    public Guid ComponentId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}

public class SaveComboRequest
{
    [NotEmptyGuid]
    public Guid ComboId { get; set; }

    public bool IsCombo { get; set; }

    public List<ComboComponentItem> Components { get; set; } = new();
}
