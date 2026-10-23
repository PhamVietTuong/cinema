using Cinema.Data.Enums;

namespace Cinema.Data.Contracts;

/// <summary>Flattened MovieCredit x Person row for the movie-detail Cast &amp; Crew section.
/// Projected + untracked: one query, joined server-side, no N+1 per credited person.</summary>
public record MovieCreditRow(
    Guid Id,
    Guid PersonId,
    string PersonName,
    string? PersonPhotoUrl,
    CreditType CreditType,
    string? CharacterName,
    int DisplayOrder);
