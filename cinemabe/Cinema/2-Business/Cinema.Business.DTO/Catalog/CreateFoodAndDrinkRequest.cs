
namespace Cinema.Business.DTO.Catalog;

public class CreateFoodAndDrinkRequest
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
}
