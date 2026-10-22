using System.ComponentModel.DataAnnotations;
using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

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

    public bool RequiresIdCheck { get; set; }

    [StringLength(1000)]
    public string? IdCheckMessage { get; set; }
}
