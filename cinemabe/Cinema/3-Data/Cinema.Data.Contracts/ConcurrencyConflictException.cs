namespace Cinema.Data.Contracts;

/// <summary>
/// Raised when a row changed underneath the caller (optimistic-concurrency token mismatch), e.g. two people
/// receiving the same storage plan at once. Lets the business layer ask the user to reload.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
