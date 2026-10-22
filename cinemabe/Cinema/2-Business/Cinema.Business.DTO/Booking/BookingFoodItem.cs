using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;

namespace Cinema.Business.DTO.Booking;

public class BookingFoodItem
{
    [NotEmptyGuid]
    public Guid FoodAndDrinkId { get; set; }

    // Lower bound matters: BookingManager sums Price * Quantity, so a negative quantity would
    // subtract from the order total.
    [Range(1, 100)]
    public int Quantity { get; set; }
}
