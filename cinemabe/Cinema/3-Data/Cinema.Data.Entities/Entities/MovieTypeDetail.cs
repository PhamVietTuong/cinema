namespace Cinema.Data.Entities;
public class MovieTypeDetail
{
    public Guid MovieId { get; set; }
    public Guid MovieTypeId { get; set; }
    public Movie Movie { get; set; } = null!;
    public MovieType MovieType { get; set; } = null!;
}
