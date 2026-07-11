using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class RoomDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; }
}

public class CreateRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Active;
}

public class UpdateRoomRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TheaterId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Active;
}

// ── Seat-map editor (assign seat types + group double seats) ───────────────────
public class RoomSeatDTO
{
    public Guid Id { get; set; }
    public string RowName { get; set; } = string.Empty;
    public int ColIndex { get; set; }
    public Guid SeatTypeId { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public string SeatTypeColor { get; set; } = "#808080";
    public double PriceMultiplier { get; set; } = 1;
    public Guid? SeatGroupId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SaveSeatMapRequest
{
    public Guid RoomId { get; set; }
    public List<SeatAssignmentItem> Seats { get; set; } = new();
}

/// <summary>
/// Resizes a room's seat grid to a target row/column count, keeping the seats that
/// remain inside the new grid (their type + grouping are preserved) and only adding
/// or removing the appended/trimmed rows and columns.
/// </summary>
public class ResizeSeatGridRequest
{
    public Guid RoomId { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
}

public class SeatAssignmentItem
{
    public Guid SeatId { get; set; }
    public Guid SeatTypeId { get; set; }
    public Guid? SeatGroupId { get; set; }
    public bool IsActive { get; set; } = true;
}
