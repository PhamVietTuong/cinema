using Cinema.Data.Enums;
namespace Cinema.Business.DTO.Booking;
public class SeatDTO
{
    public Guid Id { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    public Guid SeatTypeId { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public string SeatTypeColor { get; set; } = string.Empty;
    public SeatStatus Status { get; set; }
    public decimal Price { get; set; }
    public bool IsLocked { get; set; }
}
