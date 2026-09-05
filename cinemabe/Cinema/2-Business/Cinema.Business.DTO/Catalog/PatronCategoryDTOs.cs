using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class PatronCategoryDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Seat types this category may book. Empty = unrestricted (may book any seat type).</summary>
    public List<Guid> AllowedSeatTypeIds { get; set; } = new();
}

public class CreatePatronCategoryRequest
{
    public Guid TheaterId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, 100)]
    public double DiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Seat types this category may book. Empty = unrestricted.</summary>
    public List<Guid> AllowedSeatTypeIds { get; set; } = new();
}

public class UpdatePatronCategoryRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, 100)]
    public double DiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Seat types this category may book. Empty = unrestricted.</summary>
    public List<Guid> AllowedSeatTypeIds { get; set; } = new();
}
