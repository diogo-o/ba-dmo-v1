namespace BA.Dmo.Domain.Shared.Kernel;

/// <summary>
/// Discriminated outcome of an operation: either a success value or an error (Plan-V3 GLM-ARCH-03
/// "Result<T, Error> próprio"). The error channel is generic so domain services and use cases can
/// carry <see cref="DomainError"/> (the default) or another error representation.
/// </summary>
public readonly struct Result<TSuccess, TError>
{
    private readonly TSuccess _value;
    private readonly TError _error;

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    private Result(TSuccess value, TError error, bool isSuccess)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>Success value. Throws when the result is a failure.</summary>
    public TSuccess Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot read Value of a failed Result. Check IsSuccess first.");

    /// <summary>Error. Throws when the result is a success.</summary>
    public TError Error => IsFailure
        ? _error
        : throw new InvalidOperationException("Cannot read Error of a successful Result. Check IsFailure first.");

    public static Result<TSuccess, TError> Success(TSuccess value) =>
        new(value, default!, isSuccess: true);

    public static Result<TSuccess, TError> Failure(TError error) =>
        new(default!, error, isSuccess: false);

    /// <summary>Projects the success value, preserving any failure.</summary>
    public Result<TNext, TError> Map<TNext>(Func<TSuccess, TNext> projection) =>
        IsSuccess
            ? Result<TNext, TError>.Success(projection(_value))
            : Result<TNext, TError>.Failure(_error);

    /// <summary>Chains a follow-up operation, preserving any failure.</summary>
    public Result<TNext, TError> Bind<TNext>(Func<TSuccess, Result<TNext, TError>> next) =>
        IsSuccess ? next(_value) : Result<TNext, TError>.Failure(_error);

    public override string ToString() =>
        IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}

/// <summary>Convenience factories for the common <see cref="DomainError"/> channel.</summary>
public static class Result
{
    public static Result<TSuccess, DomainError> Success<TSuccess>(TSuccess value) =>
        Result<TSuccess, DomainError>.Success(value);

    public static Result<TSuccess, DomainError> Failure<TSuccess>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Result<TSuccess, DomainError>.Failure(error);
    }
}
