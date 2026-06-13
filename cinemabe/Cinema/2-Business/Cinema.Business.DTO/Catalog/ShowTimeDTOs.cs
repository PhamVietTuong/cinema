using Cinema.Business.DTO.Requests;
using Cinema.Data.Enums;

namespace Cinema.Business.DTO.Catalog;

public class ShowTimeDTO
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; }
    public ShowTimeType ShowTimeType { get; set; }
    public bool IsActive { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomName { get; set; }
    public int BasePrice { get; set; }
}

public class CreateShowTimeRequest
{
    public Guid MovieId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; } = ProjectionForm.TwoD;
    public ShowTimeType ShowTimeType { get; set; } = ShowTimeType.Normal;
    public bool IsActive { get; set; } = true;
    public Guid RoomId { get; set; }
    public int BasePrice { get; set; }
}

public class UpdateShowTimeRequest : IHasId
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ProjectionForm ProjectionForm { get; set; } = ProjectionForm.TwoD;
    public ShowTimeType ShowTimeType { get; set; } = ShowTimeType.Normal;
    public bool IsActive { get; set; } = true;
    public Guid RoomId { get; set; }
    public int BasePrice { get; set; }
}
