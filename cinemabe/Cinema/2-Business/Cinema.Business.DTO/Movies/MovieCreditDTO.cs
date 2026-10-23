using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Movies;
public class MovieCreditDTO
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonPhotoUrl { get; set; }
    public CreditType CreditType { get; set; }
    public string? CharacterName { get; set; }
    public int DisplayOrder { get; set; }
}
