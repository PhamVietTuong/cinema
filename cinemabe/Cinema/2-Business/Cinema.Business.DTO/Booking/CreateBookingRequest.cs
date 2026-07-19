namespace Cinema.Business.DTO.Booking;
public class CreateBookingRequest
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public List<BookingSeatItem> Seats { get; set; } = new();
    public List<BookingFoodItem> Foods { get; set; } = new();
    public string? DiscountCode { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    /// <summary>Loyalty points the customer wants to spend on this booking (0 = none). Capped server-side
    /// at the balance and the order total.</summary>
    public int PointsToRedeem { get; set; }
}

public class BookingSeatItem
{
    public Guid SeatId { get; set; }
}

public class BookingFoodItem
{
    public Guid FoodAndDrinkId { get; set; }
    public int Quantity { get; set; }
}
