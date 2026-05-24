namespace Cinema.Business.DTO.Theaters;
public class CreateTheaterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ImageUrl { get; set; }
}
