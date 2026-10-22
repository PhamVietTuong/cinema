using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

public class StaffKpisDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    /// <summary>Invoices paid in the range.</summary>
    public int PaidInvoices { get; set; }
    /// <summary>Paid invoices that contain tickets.</summary>
    public int TicketInvoices { get; set; }
    /// <summary>Paid ticket invoices that also contain food or drink.</summary>
    public int TicketInvoicesWithFood { get; set; }
    /// <summary>TicketInvoicesWithFood / TicketInvoices (0 when there are no ticket invoices).</summary>
    public double AttachRate { get; set; }
    public int TicketsSold { get; set; }
    /// <summary>Final amount of paid ticket invoices / tickets sold (0 when none).</summary>
    public double AverageSpendPerHead { get; set; }
}
