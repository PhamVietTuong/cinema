namespace Cinema.Business.Tests;

internal sealed class FixedClock : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedClock(DateTime utcNow)
    {
        _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    public override DateTimeOffset GetUtcNow() => _now;

    // Local time == UTC, so the test's "now" is also the theater's local now.
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
