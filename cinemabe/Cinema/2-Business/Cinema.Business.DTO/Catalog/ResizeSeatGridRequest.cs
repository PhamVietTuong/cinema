using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

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
