using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class SeatAssignmentItem
{
    public Guid SeatId { get; set; }
    public Guid? SeatGroupId { get; set; }
    public bool IsActive { get; set; } = true;
}
