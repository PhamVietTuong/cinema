namespace Cinema.Data.Entities;

/// <summary>A screening room format (2D, 3D, IMAX, 4DX, …), per theater. Drives equipment + ticket pricing.</summary>
public class RoomType : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>Equipment / notes for this format (e.g. "Laser projector, Dolby Atmos").</summary>
    public string? Description { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
