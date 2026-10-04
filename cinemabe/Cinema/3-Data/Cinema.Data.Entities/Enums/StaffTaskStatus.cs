namespace Cinema.Data.Enums;

/// <summary>Lifecycle of a staff task. Stored as int; never renumber.</summary>
public enum StaffTaskStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2,
    Cancelled = 3
}
