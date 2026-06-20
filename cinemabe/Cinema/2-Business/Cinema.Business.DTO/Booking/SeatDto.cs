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
    public double Price { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>Set when this seat is part of a linked group (e.g. a double seat). Both
    /// seats in a group share the same id and must be selected/booked together.</summary>
    public Guid? SeatGroupId { get; set; }
}
