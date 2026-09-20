using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

/// <summary>One row = one (logical category, seat kind) pair, e.g. Adult/Standard, Adult/Double.
/// Omitting a seat kind for a category means that category cannot book that kind of seat at all —
/// this is the entire eligibility rule.</summary>
public class PatronCategoryDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public Guid SeatTypeId { get; set; }
    public string SeatTypeName { get; set; } = string.Empty;
    public SeatKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Absolute VND price for one seat row of this kind, independently configured per kind
    /// (a Double row is never computed as 2x Standard).</summary>
    public double Price { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>One (SeatTypeId, Price) entry per seat kind the category may book. A kind omitted from
/// this list means the category cannot book that kind at all.</summary>
public class PatronCategoryPriceItem
{
    public Guid SeatTypeId { get; set; }

    [Range(0, double.MaxValue)]
    public double Price { get; set; }
}

/// <summary>Creates/replaces a whole logical category (e.g. "Adult") across all its seat-kind rows in
/// one call. Id/UpdateAsync always operate on every row sharing (TheaterId, Name).</summary>
public class CreatePatronCategoryRequest
{
    public Guid TheaterId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>At least one entry required.</summary>
    public List<PatronCategoryPriceItem> Prices { get; set; } = new();
}

public class UpdatePatronCategoryRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>At least one entry required.</summary>
    public List<PatronCategoryPriceItem> Prices { get; set; } = new();
}
