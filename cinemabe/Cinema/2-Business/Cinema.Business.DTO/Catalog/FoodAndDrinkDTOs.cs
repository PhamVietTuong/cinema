using Cinema.Business.DTO.Requests;

namespace Cinema.Business.DTO.Catalog;

public class FoodAndDrinkDTO
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; }
}

public class CreateFoodAndDrinkRequest
{
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class UpdateFoodAndDrinkRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid TheaterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
}
