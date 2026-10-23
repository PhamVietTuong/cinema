using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Movies;

/// <summary>Exactly one of PersonId or NewPersonName must be set. PersonId references an existing
/// Person. NewPersonName creates one inline (or reuses an existing Person whose name matches
/// case-insensitively) — NewPersonPhotoUrl is used only when a brand-new Person is actually created.</summary>
public class MovieCreditRequest
{
    public Guid? PersonId { get; set; }

    [MaxLength(200)]
    public string? NewPersonName { get; set; }

    [MaxLength(500)]
    public string? NewPersonPhotoUrl { get; set; }

    public CreditType CreditType { get; set; }

    [MaxLength(200)]
    public string? CharacterName { get; set; }

    [Range(0, 10000)]
    public int DisplayOrder { get; set; }
}
