namespace Cinema.Data.Enums;

/// <summary>Lifecycle of an incident (Open until a staff member resolves it). Stored as int; never renumber.</summary>
public enum IncidentStatus
{
    Open = 0,
    Resolved = 1
}
