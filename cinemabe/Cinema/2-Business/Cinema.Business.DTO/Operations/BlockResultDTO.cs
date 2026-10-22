using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

public class BlockResultDTO
{
    public Guid IncidentId { get; set; }
    /// <summary>Seats taken out of sale (a double seat blocks both halves); empty for a room block.</summary>
    public List<Guid> BlockedSeatIds { get; set; } = new();
    public Guid? BlockedRoomId { get; set; }
    /// <summary>Upcoming active tickets on the blocked seat(s)/room, so staff can relocate the customers.</summary>
    public List<AffectedTicketDTO> AffectedTickets { get; set; } = new();
}
