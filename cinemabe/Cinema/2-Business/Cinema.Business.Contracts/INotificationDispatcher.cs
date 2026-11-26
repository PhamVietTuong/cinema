namespace Cinema.Business.Contracts;

/// <summary>
/// Hands slow, non-critical delivery work (confirmation email/SMS) to a background worker so it can never delay or
/// fail the request that triggered it (notably the payment IPN, which must answer within seconds).
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Queues the work. It runs once, on its own; an exception it throws is logged and swallowed.</summary>
    void Enqueue(Func<Task> work);
}
