using Cinema.Business.DTO.Staff;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Operations;

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
