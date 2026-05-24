namespace Cinema.Data.Entities;
public class AgeRestriction : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinAge { get; set; }
    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
}
