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
