namespace Cinema.Business.DTO.Theaters;
public class TheaterDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ImageUrl { get; set; }
    public int RoomCount { get; set; }
    public bool IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int MaxSeatsPerBooking { get; set; } = 10;
    public int MaxPromotionDiscountPercent { get; set; } = 50;
    public int MaxHolidayPromotionDiscountPercent { get; set; } = 50;
}
