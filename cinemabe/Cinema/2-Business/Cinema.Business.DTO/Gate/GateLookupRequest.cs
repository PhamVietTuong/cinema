using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Gate;

public class GateLookupRequest
{
    [StringLength(50)]
    public string? InvoiceCode { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    public Guid? TheaterId { get; set; }
}
