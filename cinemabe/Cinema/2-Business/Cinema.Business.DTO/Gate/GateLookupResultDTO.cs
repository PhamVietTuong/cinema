using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Gate;

public class GateLookupResultDTO
{
    public string InvoiceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Phone with all but the last 3 digits masked.</summary>
    public string MaskedPhone { get; set; } = string.Empty;
    public List<GateLookupTicketDTO> Tickets { get; set; } = new();
}
