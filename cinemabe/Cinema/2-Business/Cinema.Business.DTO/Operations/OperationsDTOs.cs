using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

// ── Schedule board ───────────────────────────────────────────────────────────

public class ScheduleBoardRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }

    /// <summary>The day to show (only the date part is used).</summary>
    public DateTime Date { get; set; }
}

public class ScheduleBoardDTO
{
    public Guid TheaterId { get; set; }
    public DateTime Date { get; set; }
    public List<ScheduleBoardRoomDTO> Rooms { get; set; } = new();
}

public class ScheduleBoardRoomDTO
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public RoomStatus RoomStatus { get; set; }
    /// <summary>Cleaning/turnover minutes the room class needs after each screening.</summary>
    public int TurnoverBufferMinutes { get; set; }
    /// <summary>Sellable (active) seats of the room.</summary>
    public int Capacity { get; set; }
    public List<ScheduleBoardShowTimeDTO> ShowTimes { get; set; } = new();
}

public class ScheduleBoardShowTimeDTO
{
    public Guid ShowTimeId { get; set; }
    public Guid MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    /// <summary>End plus the room class's turnover buffer: when the room is free for the next screening.</summary>
    public DateTime BufferEnd { get; set; }
    public int Sold { get; set; }
    public int Capacity { get; set; }
    public RoomStatus RoomStatus { get; set; }
}

// ── Incidents ────────────────────────────────────────────────────────────────

public class ReportIncidentRequest
{
    /// <summary>Required for admins and multi-theater callers; staff may omit it (their own theater is used).</summary>
    public Guid? TheaterId { get; set; }
    public IncidentCategory Category { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? SeatId { get; set; }
    public Guid? ShowTimeId { get; set; }
}

public class ResolveIncidentRequest
{
    public Guid IncidentId { get; set; }

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    /// <summary>Also put the blocked seat/room back on sale. Needs an approver (or an override).</summary>
    public bool Unblock { get; set; }

    /// <summary>Manager approval when the caller is not an approver and <see cref="Unblock"/> is set.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}

public class GetIncidentRequest
{
    public Guid IncidentId { get; set; }
}

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

    /// <summary>Manager approval when the caller is not an approver.</summary>
    public ManagerOverrideDTO? Override { get; set; }
}

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

public class IncidentDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomName { get; set; }
    public Guid? SeatId { get; set; }
    public string? SeatLabel { get; set; }
    public Guid? ShowTimeId { get; set; }
    public IncidentCategory Category { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string? ReportedByName { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public bool BlocksSeat { get; set; }
    public bool BlocksRoom { get; set; }
    public DateTime CreationTime { get; set; }
}

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

public class BlockResultDTO
{
    public Guid IncidentId { get; set; }
    /// <summary>Seats taken out of sale (a double seat blocks both halves); empty for a room block.</summary>
    public List<Guid> BlockedSeatIds { get; set; } = new();
    public Guid? BlockedRoomId { get; set; }
    /// <summary>Upcoming active tickets on the blocked seat(s)/room, so staff can relocate the customers.</summary>
    public List<AffectedTicketDTO> AffectedTickets { get; set; } = new();
}
