namespace Cinema.Data.Contracts;

/// <summary>An active staff member of a theater, for the roster picker.</summary>
public record TheaterStaffRow(Guid Id, string Name, string RoleName);
