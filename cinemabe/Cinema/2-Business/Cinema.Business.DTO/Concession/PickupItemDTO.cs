using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

public class PickupItemDTO
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
