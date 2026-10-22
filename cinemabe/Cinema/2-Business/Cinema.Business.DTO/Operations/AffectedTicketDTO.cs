using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

/// <summary>An upcoming active ticket affected by a block. Nothing is cancelled: staff relocate these manually.</summary>
public class AffectedTicketDTO
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public Guid ShowTimeId { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public Guid SeatId { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
}
