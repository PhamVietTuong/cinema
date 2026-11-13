namespace Cinema.Data.Entities;
public class Theater : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Geo-coordinates (WGS84 decimal degrees) for "nearest theater" search; null if not set.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>Most seats one online booking may hold at this theater (1-50, default 10). Counter sales are not capped.</summary>
    public int MaxSeatsPerBooking { get; set; } = 10;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
