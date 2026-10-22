using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

public class SalesReportDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public SalesGroupBy GroupBy { get; set; }
    public List<SalesReportRowDTO> Rows { get; set; } = new();
    /// <summary>Column totals over <see cref="Rows"/> (Key/Label empty).</summary>
    public SalesReportRowDTO Totals { get; set; } = new();
}
