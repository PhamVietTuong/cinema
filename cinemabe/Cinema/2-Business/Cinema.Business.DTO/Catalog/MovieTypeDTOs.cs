using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class MovieTypeDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateMovieTypeRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateMovieTypeRequest : IHasId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
