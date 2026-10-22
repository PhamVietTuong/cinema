namespace Cinema.Service.WebApiHost.Controllers;

public record ValidateDiscountCodeRequest(string Code, Guid RoomId, Guid ShowTimeId, double Total);
