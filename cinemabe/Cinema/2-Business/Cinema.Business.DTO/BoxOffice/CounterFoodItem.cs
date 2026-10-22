using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.BoxOffice;

public class CounterFoodItem
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    /// <summary>Manager-approved unit price. Needs a manager override; between 0 and the list price. Null = list price.</summary>
    [Range(0, 100000000)]
    public double? OverrideUnitPrice { get; set; }
}
