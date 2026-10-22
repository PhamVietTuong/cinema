namespace Cinema.Business.DTO.Invoices;

/// <summary>Admin request to issue a new gift card. The code is generated server-side.</summary>
public class IssueGiftCardRequest
{
    public double Amount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? IssuedToEmail { get; set; }
}
