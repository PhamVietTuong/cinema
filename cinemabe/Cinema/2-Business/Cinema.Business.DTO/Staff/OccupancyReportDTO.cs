using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Staff;

public class OccupancyReportDTO
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<OccupancyRowDTO> Rows { get; set; } = new();
    public int TotalSoldSeats { get; set; }
    public int TotalActiveSeats { get; set; }
    public double OccupancyRate { get; set; }
}
