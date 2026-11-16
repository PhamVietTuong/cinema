using System.ComponentModel.DataAnnotations;

namespace Cinema.Business.DTO.Theaters;
public class CreateTheaterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ImageUrl { get; set; }
    [Range(-90, 90)]
    public double? Latitude { get; set; }
    [Range(-180, 180)]
    public double? Longitude { get; set; }
    [Range(1, 50)]
    public int MaxSeatsPerBooking { get; set; } = 10;
}
