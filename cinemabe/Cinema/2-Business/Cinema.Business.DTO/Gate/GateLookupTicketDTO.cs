using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Gate;

public class GateLookupTicketDTO
{
    public string QrCode { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowTime { get; set; }
    public bool IsUsed { get; set; }
}
