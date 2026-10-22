using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

public class GetPickupQueueRequest
{
    /// <summary>Required for admins (and multi-theater roles); staff may omit it.</summary>
    public Guid? TheaterId { get; set; }

    /// <summary>The (local) day whose showtimes the queue covers. Null = today.</summary>
    public DateTime? Day { get; set; }
}
