namespace Cinema.Business.DTO.Invoices;

/// <summary>Result of a customer checking a gift-card code before applying it at checkout.</summary>
public class GiftCardValidationDTO
{
    public bool Valid { get; set; }
    public double Balance { get; set; }
    public string? Message { get; set; }
}
