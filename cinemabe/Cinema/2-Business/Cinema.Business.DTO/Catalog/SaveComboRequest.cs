using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.Catalog;

public class SaveComboRequest
{
    [NotEmptyGuid]
    public Guid ComboId { get; set; }

    public bool IsCombo { get; set; }

    public List<ComboComponentItem> Components { get; set; } = new();
}
