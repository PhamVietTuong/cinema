namespace Cinema.Data.Contracts;

public class KpiRaw
{
    /// <summary>Invoices paid in the window.</summary>
    public int SoldInvoices { get; set; }
    /// <summary>Currently Paid invoices (paid in the window) that contain at least one ticket, and their final amounts.</summary>
    public int TicketInvoices { get; set; }
    public double TicketInvoicesAmount { get; set; }
    /// <summary>Of those, how many also contain food or drink.</summary>
    public int TicketInvoicesWithFood { get; set; }
    /// <summary>Tickets on those invoices (the head count).</summary>
    public int Tickets { get; set; }
}
