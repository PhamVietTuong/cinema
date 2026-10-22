using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.Catalog;

public class ComboComponentItem
{
    [NotEmptyGuid]
    public Guid ComponentId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}
