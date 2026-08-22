namespace BA.Dmo.Domain.Shared.Kernel;

/// <summary>
/// Production <see cref="IClock"/> backed by the system time (UTC).
/// Tests use fixed-date fakes instead (09_TEST §5).
/// </summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    private SystemClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
