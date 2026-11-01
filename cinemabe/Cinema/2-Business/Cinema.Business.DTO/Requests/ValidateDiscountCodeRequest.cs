namespace Cinema.Business.DTO.Requests;

public class ValidateDiscountCodeRequest
{
    public string Code { get; set; } = string.Empty;

    public Guid RoomId { get; set; }

    public Guid ShowTimeId { get; set; }

    public double Total { get; set; }
}
