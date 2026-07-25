namespace Cinema.Business.DTO.Invoices;

/// <summary>A gift card as seen by an admin.</summary>
public class GiftCardDTO
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public double InitialBalance { get; set; }
    public double Balance { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? IssuedToEmail { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>Admin request to issue a new gift card. The code is generated server-side.</summary>
public class IssueGiftCardRequest
{
    public double Amount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? IssuedToEmail { get; set; }
}

/// <summary>Result of a customer checking a gift-card code before applying it at checkout.</summary>
public class GiftCardValidationDTO
{
    public bool Valid { get; set; }
    public double Balance { get; set; }
    public string? Message { get; set; }
}
