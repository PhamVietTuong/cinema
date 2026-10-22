namespace Cinema.Business.DTO.BoxOffice;

public class CounterQuoteLineDTO
{
    /// <summary>"Seat" or "Food".</summary>
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    /// <summary>Price before any manager override.</summary>
    public double ListUnitPrice { get; set; }
    public double LineTotal { get; set; }
}
