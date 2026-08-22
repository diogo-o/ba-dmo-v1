namespace BA.Dmo.Domain.Shared.Kernel;

/// <summary>
/// Uniform error categories of the BA DMO shared kernel (Plan-V3 GLM-ARCH-03).
/// Every domain/use-case failure is classified with one of these categories so that
/// callers (web guards, CLI, tests) can react uniformly.
/// </summary>
public enum ErrorCategory
{
    ValidationError,
    DomainConflict,
    NotFound,
    Unauthorized,
    Forbidden,
    ConcurrencyConflict,
    BackendUnavailable,
    Unexpected
}
