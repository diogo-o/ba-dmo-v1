namespace BA.Dmo.Domain.Shared.Kernel;

/// <summary>
/// Structured failure of a domain operation or use case (Plan-V3 GLM-ARCH-03).
/// Carries a uniform <see cref="ErrorCategory"/>, a stable machine-readable <see cref="Code"/>
/// and a human-readable <see cref="Message"/>. Warnings are not errors: a warning never
/// prevents recording an operational fact (GLM-CORE-01).
/// </summary>
public sealed record DomainError
{
    public ErrorCategory Category { get; }

    /// <summary>Stable identifier of the error (e.g. INTERNAL_USER_INACTIVE).</summary>
    public string Code { get; }

    public string Message { get; }

    private DomainError(ErrorCategory category, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Domain error code must not be empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Domain error message must not be empty.", nameof(message));

        Category = category;
        Code = code.Trim();
        Message = message.Trim();
    }

    public static DomainError Validation(string code, string message) =>
        new(ErrorCategory.ValidationError, code, message);

    public static DomainError DomainConflict(string code, string message) =>
        new(ErrorCategory.DomainConflict, code, message);

    public static DomainError NotFound(string code, string message) =>
        new(ErrorCategory.NotFound, code, message);

    public static DomainError Unauthorized(string code, string message) =>
        new(ErrorCategory.Unauthorized, code, message);

    public static DomainError Forbidden(string code, string message) =>
        new(ErrorCategory.Forbidden, code, message);

    public static DomainError ConcurrencyConflict(string code, string message) =>
        new(ErrorCategory.ConcurrencyConflict, code, message);

    public static DomainError BackendUnavailable(string code, string message) =>
        new(ErrorCategory.BackendUnavailable, code, message);

    public static DomainError Unexpected(string code, string message) =>
        new(ErrorCategory.Unexpected, code, message);

    public override string ToString() => $"[{Category}] {Code}: {Message}";
}
