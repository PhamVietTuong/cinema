namespace Cinema.Data.Contracts;

/// <summary>Read-only projection of one ticket row in a gate lookup.</summary>
public sealed class GateLookupRow
{
    public string InvoiceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public bool IsUsed { get; set; }
}
