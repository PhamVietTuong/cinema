using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

public class BlockRoomRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public Guid RoomId { get; set; }

    /// <summary>An existing open incident to attach the block to; omit to open a new one.</summary>
    public Guid? IncidentId { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>Manager approval when the caller is not an approver.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}
