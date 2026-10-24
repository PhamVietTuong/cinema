using System.ComponentModel.DataAnnotations;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Movies;

/// <summary>PersonId, when set, edits that Person's Name/PhotoUrl in place (applies to every movie
/// they're credited on). PersonId absent means match-or-create: an existing Person whose Name matches
/// PersonName case-insensitively is reused (a non-blank PersonPhotoUrl then replaces theirs), otherwise
/// a brand-new Person is created.</summary>
public class MovieCreditRequest
{
    public Guid? PersonId { get; set; }

    [Required]
    [MaxLength(200)]
    public string PersonName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PersonPhotoUrl { get; set; }

    public CreditType CreditType { get; set; }

    [MaxLength(200)]
    public string? CharacterName { get; set; }

    [Range(0, 10000)]
    public int DisplayOrder { get; set; }
}
