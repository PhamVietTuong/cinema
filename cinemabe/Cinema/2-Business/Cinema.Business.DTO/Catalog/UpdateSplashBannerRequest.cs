using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class UpdateSplashBannerRequest : IHasId
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(500)]
    public string? LinkUrl { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsPublished { get; set; }
}
