using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Operations;

public class BlockSeatRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public Guid SeatId { get; set; }

    /// <summary>An existing open incident to attach the block to; omit to open a new one.</summary>
    public Guid? IncidentId { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}
