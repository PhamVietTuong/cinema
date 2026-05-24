namespace Cinema.Data.Entities;
public class MovieType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<MovieTypeDetail> MovieTypeDetails { get; set; } = new List<MovieTypeDetail>();
}
