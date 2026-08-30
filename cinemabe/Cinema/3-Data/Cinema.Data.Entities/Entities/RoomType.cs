namespace Cinema.Data.Entities;

/// <summary>
/// A room's commercial class, per theater (Standard, IMAX, 4DX, Lagom, …). Two kinds of value live
/// here: licensed projection technologies (IMAX, 4DX, ScreenX) and a theater's own interior brands.
/// Free text on purpose — chains coin new hall brands constantly. Drives the base ticket price.
/// </summary>
public class RoomType : BaseEntity
{
    public Guid TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>Equipment / notes for this format (e.g. "Laser projector, Dolby Atmos").</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether rooms of this class can screen 3D. Not derivable from <see cref="Name"/>: a premium
    /// class such as "Lagom" is an interior brand that may or may not have a 3D projector.
    /// </summary>
    public bool SupportsThreeD { get; set; }

    /// <summary>Flat amount added per ticket when the screening is 3D, on top of the room class base price.</summary>
    public double ThreeDSurcharge { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
