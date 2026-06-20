namespace Cinema.Business.DTO.Booking;
public class CreateBookingRequest
{
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public List<BookingSeatItem> Seats { get; set; } = new();
    public List<BookingFoodItem> Foods { get; set; } = new();
    public string? DiscountCode { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
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
