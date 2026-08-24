using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Kernel;

/// <summary>
/// U-01 kernel unit tests: Result&lt;T,E&gt; (Plan-V3 GLM-ARCH-03; roadmap U-01 "kernel unit tests").
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_CarriesValue_AndIsNotFailure()
    {
        var result = Result<int, DomainError>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_CarriesError_AndIsNotSuccess()
    {
        var error = DomainError.Validation("INVALID_LINE", "Linha inválida.");
        var result = Result<int, DomainError>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Value_OnFailure_Throws()
    {
        var result = Result<int, DomainError>.Failure(
            DomainError.NotFound("NOT_FOUND", "Registo inexistente."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_OnSuccess_Throws()
    {
        var result = Result<int, DomainError>.Success(1);

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void ConvenienceFactories_UseDomainErrorChannel()
    {
        var success = Result.Success("ok");
        var failure = Result.Failure<string>(DomainError.Unexpected("BOOM", "Falha."));

        Assert.True(success.IsSuccess);
        Assert.Equal("ok", success.Value);
        Assert.True(failure.IsFailure);
        Assert.Equal(ErrorCategory.Unexpected, failure.Error.Category);
    }

    [Fact]
    public void Failure_WithNullDomainError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure<string>(null!));
    }

    [Fact]
    public void Map_TransformsSuccessValue_AndPreservesFailure()
    {
        var mapped = Result<int, DomainError>.Success(21).Map(v => v * 2);
        var error = DomainError.Forbidden("NO_GRANT", "Sem acesso.");
        var mappedFailure = Result<int, DomainError>.Failure(error).Map(v => v * 2);

        Assert.Equal(42, mapped.Value);
        Assert.True(mappedFailure.IsFailure);
        Assert.Same(error, mappedFailure.Error);
    }

    [Fact]
    public void Bind_ChainsSuccess_AndShortCircuitsFailure()
    {
        var chained = Result<int, DomainError>.Success(20)
            .Bind(v => Result<int, DomainError>.Success(v + 5));

        var error = DomainError.ConcurrencyConflict("STALE", "Versão antiga.");
        var shortCircuited = Result<int, DomainError>.Failure(error)
            .Bind(v => Result<int, DomainError>.Success(v + 5));

        Assert.Equal(25, chained.Value);
        Assert.True(shortCircuited.IsFailure);
        Assert.Same(error, shortCircuited.Error);
    }

    [Fact]
    public void GenericErrorChannel_IsNotLimitedToDomainError()
    {
        var result = Result<string, string>.Failure("raw error");

        Assert.True(result.IsFailure);
        Assert.Equal("raw error", result.Error);
    }
}
