using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

public class StaffShiftDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Note { get; set; }
}
