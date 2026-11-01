namespace Cinema.Data.Entities;

/// <summary>
/// A single-image popup banner shown to CinemaUser visitors once per browser session. Deliberately
/// separate from <see cref="PromotionProgram"/>, which drives the homepage promotions list — this is
/// a one-off splash/popup, not a catalog entry.
/// </summary>
public class SplashBanner : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }

    /// <summary>Date-only (time-of-day is zeroed); the banner is active while the current local
    /// (Asia/Ho_Chi_Minh) date falls within [StartDate, EndDate], inclusive.</summary>
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsPublished { get; set; }
}
