using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

public class SetFoodStatusRequest
{
    public Guid? TheaterId { get; set; }

    [NotEmptyGuid]
    public Guid InvoiceId { get; set; }

    /// <summary>The state to move to: Preparing, Ready or HandedOver (see <see cref="FoodOrderStatus"/>).</summary>
    public FoodOrderStatus Status { get; set; }
}
