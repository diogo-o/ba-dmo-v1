namespace BA.Dmo.Domain.Shared.Kernel;

/// <summary>
/// Time abstraction of the shared kernel (Plan-V3 GLM-ARCH-03).
/// All timestamps/authorship recorded by the application flow through this contract so that
/// tests can fix dates (09_TEST §5). Implementations must report UTC.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
