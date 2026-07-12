namespace Cinema.Business.DTO.Invoices;

/// <summary>Ticket revenue for one grouping key (movie title or theater name).</summary>
public class RevenueBreakdownDTO
{
    public string Name { get; set; } = string.Empty;
    public double Total { get; set; }
}
