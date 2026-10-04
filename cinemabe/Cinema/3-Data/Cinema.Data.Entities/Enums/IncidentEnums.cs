namespace Cinema.Data.Enums;

/// <summary>What kind of problem an incident reports. Stored as int; never renumber.</summary>
public enum IncidentCategory
{
    Other = 0,
    Seat = 1,
    Room = 2,
    Projection = 3,
    Sound = 4,
    Safety = 5,
    Customer = 6,
    Cleanliness = 7
}

/// <summary>How urgent an incident is. Stored as int; never renumber.</summary>
public enum IncidentSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>Lifecycle of an incident (Open until a staff member resolves it). Stored as int; never renumber.</summary>
public enum IncidentStatus
{
    Open = 0,
    Resolved = 1
}
