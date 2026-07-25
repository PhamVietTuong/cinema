namespace Cinema.Data.Contracts;

/// <summary>
/// Raised when a booking insert violates the active-seat unique index — i.e. another booking (possibly on
/// a different server instance) claimed one of the seats first. Lets the business layer surface a friendly
/// "seat no longer available" instead of a raw persistence error.
/// </summary>
public class SeatUnavailableException : Exception
{
    public SeatUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
