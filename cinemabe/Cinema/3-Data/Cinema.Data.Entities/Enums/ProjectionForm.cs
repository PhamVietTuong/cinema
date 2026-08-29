namespace Cinema.Data.Enums;

/// <summary>
/// The image dimension a screening is presented in. This is a property of the screening, not of
/// the room: a 3D-capable room runs 2D content most of the week. The room's commercial class
/// (Standard, IMAX, 4DX, …) is a separate axis and lives in <c>RoomType</c>.
/// </summary>
public enum ProjectionForm { TwoD = 1, ThreeD = 2 }
