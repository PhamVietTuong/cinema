namespace Cinema.Data.Enums;

/// <summary>The decision of a gate scan. Every value other than <see cref="Admitted"/> means "do not let in".
/// Serialized by name into AuditLog.DataJson; never rename a member.</summary>
public enum ScanOutcome
{
    Admitted = 0,
    NotFound = 1,
    NotPaid = 2,
    AlreadyUsed = 3,
    WrongTheater = 4,
    WrongShowTime = 5,
    TooEarly = 6,
    Expired = 7,
    /// <summary>The movie is age restricted: the gate must check ID, then re-scan with AgeConfirmed = true.
    /// The ticket is NOT marked used.</summary>
    AgeCheckRequired = 8
}
