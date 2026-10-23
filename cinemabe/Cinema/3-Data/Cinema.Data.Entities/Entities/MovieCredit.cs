using Cinema.Data.Enums;

namespace Cinema.Data.Entities;
public class MovieCredit : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public CreditType CreditType { get; set; }
    public string? CharacterName { get; set; }
    public int DisplayOrder { get; set; }
}
