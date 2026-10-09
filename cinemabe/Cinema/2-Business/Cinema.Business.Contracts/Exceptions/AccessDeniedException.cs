namespace Cinema.Business.Contracts.Exceptions;

/// <summary>
/// An authenticated caller is not allowed to do this (out-of-scope theater, missing/invalid manager override...).
/// Controllers map it to HTTP 403. Use it instead of <see cref="UnauthorizedAccessException"/> for authorization
/// failures: that one maps to 401 and the frontend logs the user out on 401.
/// </summary>
public class AccessDeniedException : Exception
{
    public AccessDeniedException(string message) : base(message)
    {
    }
}
