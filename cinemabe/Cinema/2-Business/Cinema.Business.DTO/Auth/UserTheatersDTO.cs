namespace Cinema.Business.DTO.Auth;

/// <summary>The theaters a RegionalManager is assigned to (read and replace-all write shape).</summary>
public class UserTheatersDTO
{
    public Guid UserId { get; set; }
    public List<Guid> TheaterIds { get; set; } = new();
}
