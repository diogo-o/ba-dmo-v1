using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Kernel;

/// <summary>
/// U-01 kernel unit tests: uniform error categories (Plan-V3 GLM-ARCH-03).
/// </summary>
public class DomainErrorTests
{
    [Theory]
    [InlineData(ErrorCategory.ValidationError)]
    [InlineData(ErrorCategory.DomainConflict)]
    [InlineData(ErrorCategory.NotFound)]
    [InlineData(ErrorCategory.Unauthorized)]
    [InlineData(ErrorCategory.Forbidden)]
    [InlineData(ErrorCategory.ConcurrencyConflict)]
    [InlineData(ErrorCategory.BackendUnavailable)]
    [InlineData(ErrorCategory.Unexpected)]
    public void EveryCategory_HasAFactory(ErrorCategory category)
    {
        var error = Create(category);

        Assert.Equal(category, error.Category);
        Assert.Equal("TEST_CODE", error.Code);
        Assert.Equal("Mensagem de teste.", error.Message);
    }

    [Fact]
    public void AllEightCategories_AreCovered()
    {
        Assert.Equal(8, Enum.GetValues<ErrorCategory>().Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyCode_IsRejected(string code)
    {
        Assert.Throws<ArgumentException>(() => DomainError.Validation(code, "Mensagem."));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyMessage_IsRejected(string message)
    {
        Assert.Throws<ArgumentException>(() => DomainError.Validation("CODE", message));
    }

    [Fact]
    public void ToString_IncludesCategoryCodeAndMessage()
    {
        var error = DomainError.Unauthorized("INTERNAL_USER_INACTIVE", "Utilizador interno inativo.");

        Assert.Equal(
            "[Unauthorized] INTERNAL_USER_INACTIVE: Utilizador interno inativo.",
            error.ToString());
    }

    private static DomainError Create(ErrorCategory category) => category switch
    {
        ErrorCategory.ValidationError => DomainError.Validation("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.DomainConflict => DomainError.DomainConflict("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.NotFound => DomainError.NotFound("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.Unauthorized => DomainError.Unauthorized("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.Forbidden => DomainError.Forbidden("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.ConcurrencyConflict => DomainError.ConcurrencyConflict("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.BackendUnavailable => DomainError.BackendUnavailable("TEST_CODE", "Mensagem de teste."),
        ErrorCategory.Unexpected => DomainError.Unexpected("TEST_CODE", "Mensagem de teste."),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
