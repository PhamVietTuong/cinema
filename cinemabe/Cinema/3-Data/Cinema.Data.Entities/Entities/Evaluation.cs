namespace Cinema.Data.Entities;
public class Evaluation : BaseEntity
{
    public Guid MovieId { get; set; }
    public Guid UserId { get; set; }
    public int Score { get; set; }
    public string? Review { get; set; }
    public Movie Movie { get; set; } = null!;
    public User User { get; set; } = null!;
}
