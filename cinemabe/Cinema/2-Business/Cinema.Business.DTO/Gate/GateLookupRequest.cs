using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Gate;

public class GateLookupRequest
{
    [StringLength(50)]
    public string? InvoiceCode { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    public Guid? TheaterId { get; set; }
}
