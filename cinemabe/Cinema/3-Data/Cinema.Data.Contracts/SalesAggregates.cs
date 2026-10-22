namespace Cinema.Data.Contracts;

/// <summary>Sold = invoices paid in the window.</summary>
public class SalesAggregates
{
    public List<SalesAggregateRow> Sold { get; set; } = new();
    /// <summary>
    /// Distinct invoice count, set only for dimensions where one invoice can fall in several groups (Movie,
    /// PaymentMethod), where summing the group counts would double-count. Null = the group counts add up.
    /// </summary>
    public int? SoldInvoices { get; set; }
}
