using Cinema.Data.Entities;

namespace Cinema.Business.Managers;

internal sealed record ResolvedCategory(Guid Id, string Name, Guid SeatTypeId, SeatKind Kind, double Price, bool RequiresIdCheck, string? IdCheckMessage);
