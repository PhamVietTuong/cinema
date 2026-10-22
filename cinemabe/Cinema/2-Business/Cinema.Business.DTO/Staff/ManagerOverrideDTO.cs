using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Staff;

/// <summary>
/// Manager approval attached to a sensitive staff request: the approver picks themselves from
/// <c>GetOverrideApprovers</c> and types their PIN at the terminal. Never cached client-side.
/// </summary>
public class ManagerOverrideDTO
{
    public Guid ApproverUserId { get; set; }

    [Required]
    [StringLength(20)]
    public string Pin { get; set; } = string.Empty;
}
