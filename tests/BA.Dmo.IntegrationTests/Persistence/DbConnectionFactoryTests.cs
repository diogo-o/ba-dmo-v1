using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// U-03 connection factory + configuration contract tests (reuses the U-02
/// server-side env contract; no secrets in the repository; no live SQL).
/// </summary>
public class DbConnectionFactoryTests
{
    private const string SampleConnectionString =
        "Host=127.0.0.1;Port=5432;Database=ba_dmo;Username=ba_dmo_app;Password=secret-value";

    private static Func<string, string?> EnvironmentWith(
        string? primary = null, string? fallback = null) =>
        name => name switch
        {
            DatabaseConnectionSettings.ConnectionStringVariable => primary,
            DatabaseConnectionSettings.FallbackConnectionStringVariable => fallback,
            _ => null
        };

    [Fact]
    public void ResolveConnectionString_PrefersPrimaryVariable()
    {
        var resolved = DatabaseConnectionSettings.ResolveConnectionString(
            EnvironmentWith(primary: "primary", fallback: "fallback"));

        Assert.Equal("primary", resolved);
    }

    [Fact]
    public void ResolveConnectionString_FallsBackToDatabaseUrl()
    {
        var resolved = DatabaseConnectionSettings.ResolveConnectionString(
            EnvironmentWith(fallback: "fallback"));

        Assert.Equal("fallback", resolved);
    }

    [Fact]
    public void ResolveConnectionString_ReturnsNull_WhenUnconfigured()
    {
        Assert.Null(DatabaseConnectionSettings.ResolveConnectionString(EnvironmentWith()));
        Assert.Null(DatabaseConnectionSettings.ResolveConnectionString(
            EnvironmentWith(primary: "   ")));
    }

    [Fact]
    public void FromEnvironment_MissingConfiguration_FailsClearly()
    {
        var ex = Assert.Throws<DatabaseConnectionException>(
            () => DbConnectionFactory.FromEnvironment(EnvironmentWith()));

        Assert.Contains(
            DatabaseConnectionSettings.ConnectionStringVariable,
            ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromEnvironment_ConfiguresTheFactory_WithTheResolvedString()
    {
        var factory = DbConnectionFactory.FromEnvironment(
            EnvironmentWith(primary: SampleConnectionString));

        Assert.Equal(SampleConnectionString, factory.ConnectionString);
    }

    [Fact]
    public void Constructor_RejectsEmptyConnectionString()
    {
        Assert.Throws<DatabaseConnectionException>(() => new DbConnectionFactory("  "));
    }

    [Fact]
    public void Constructor_RejectsUriFormat_WithActionableMessage_AndNoLeak()
    {
        // Npgsql 10 does not parse the postgresql:// URI form; the factory
        // must fail eagerly with a configuration error that tells the
        // operator the expected format — without echoing the credentials.
        var ex = Assert.Throws<DatabaseConnectionException>(
            () => new DbConnectionFactory("postgresql://user:***@127.0.0.1:5432/db"));

        Assert.Contains("keyword/value", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Host=", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("user:***", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("postgresql://user:***@127.0.0.1:5432/db", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsUnparseableString_WithConfigurationError()
    {
        // A non-URI string that Npgsql cannot parse is a configuration
        // failure at first use (fail closed), not a raw parse exception.
        Assert.Throws<DatabaseConnectionException>(
            () => new DbConnectionFactory("Host=127.0.0.1;Port=not-a-port"));
    }

    [Fact]
    public async Task OpenFailure_IsTranslated_AndNeverLeaksCredentials()
    {
        // Unreachable endpoint (port 9, discard): proves error translation
        // without touching any real database.
        var factory = new DbConnectionFactory(
            "Host=127.0.0.1;Port=9;Database=ba_dmo;Username=u;Password=super-secret;Timeout=2");

        var ex = await Assert.ThrowsAsync<DatabaseConnectionException>(
            () => factory.OpenConnectionAsync());

        Assert.Contains("Unable to open the database connection", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task OpenAsync_HonorsCancellation()
    {
        var factory = new DbConnectionFactory(
            "Host=127.0.0.1;Port=9;Database=ba_dmo;Timeout=15");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => factory.OpenConnectionAsync(cts.Token));
    }
}
