namespace Cinema.Business.DTO.Auth;
public class UpdateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Avatar { get; set; }
}
