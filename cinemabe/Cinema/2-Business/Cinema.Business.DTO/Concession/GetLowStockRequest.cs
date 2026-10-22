using Cinema.Business.DTO.Validation;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Concession;

public class GetLowStockRequest
{
    public Guid? TheaterId { get; set; }
}
