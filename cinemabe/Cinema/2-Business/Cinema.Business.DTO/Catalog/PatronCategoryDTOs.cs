using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

/// <summary>One independent pricing row = (Name, seat kind). Rows sharing a Name are unrelated; the
/// Name is a display label only. Omitting a seat kind for a category means that category cannot book
/// that kind of seat at all — this is the entire eligibility rule.</summary>
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

/// <summary>Creates one independent pricing row.</summary>
public class CreatePatronCategoryRequest
{
    public Guid TheaterId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid SeatTypeId { get; set; }

    [Range(0, double.MaxValue)]
    public double Price { get; set; }
}

/// <summary>Updates exactly the row with this Id — never touches rows sharing its Name.</summary>
public class UpdatePatronCategoryRequest : IHasId
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid SeatTypeId { get; set; }

    [Range(0, double.MaxValue)]
    public double Price { get; set; }
}
