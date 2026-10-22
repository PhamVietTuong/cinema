using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;

namespace Cinema.Business.DTO.Catalog;

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

    public bool RequiresIdCheck { get; set; }

    [StringLength(1000)]
    public string? IdCheckMessage { get; set; }
}
