using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

public class DeleteStaffShiftRequest
{
    public Guid ShiftId { get; set; }
}
