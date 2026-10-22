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
