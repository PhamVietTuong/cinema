namespace Cinema.Business.DTO.Invoices;

/// <summary>One day's paid-invoice revenue, used by the admin dashboard trend chart.</summary>
public class RevenueByDayDTO
{
    public DateTime Date { get; set; }
    public double Total { get; set; }
}
