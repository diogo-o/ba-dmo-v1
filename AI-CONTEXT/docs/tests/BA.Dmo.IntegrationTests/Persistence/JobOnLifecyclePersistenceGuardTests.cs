namespace BA.Dmo.IntegrationTests.Persistence;

public sealed class JobOnLifecyclePersistenceGuardTests
{
    [Fact]
    public void TransitionLifecycleAsync_PersistsCompleteLifecycleAndAuditInOneTransaction()
    {
        var source = File.ReadAllText(FindSource(
            "src", "BA.Dmo.Infrastructure", "Access", "DapperJobOnRepository.cs"));
        var methodStart = source.IndexOf(
            "public async Task TransitionLifecycleAsync(", StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "public async Task InsertRevisionAsync(", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        Assert.Contains("DapperUnitOfWork.RunAsync", method, StringComparison.Ordinal);
        Assert.Contains("status = @Status", method, StringComparison.Ordinal);
        Assert.Contains("closed_at_utc = @ClosedAtUtc", method, StringComparison.Ordinal);
        Assert.Contains("canceled_at_utc = @CanceledAtUtc", method, StringComparison.Ordinal);
        Assert.Contains("canceled_by = @CanceledBy", method, StringComparison.Ordinal);
        Assert.Contains("cancel_reason = @CancelReason", method, StringComparison.Ordinal);
        Assert.Contains("jobOn.ClosedAtUtc", method, StringComparison.Ordinal);
        Assert.Contains("jobOn.CancelledAtUtc", method, StringComparison.Ordinal);
        Assert.Contains("InsertAuditEventCoreAsync", method, StringComparison.Ordinal);
        Assert.Contains("connection, transaction", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("Db.ExecuteAsync", StringComparison.Ordinal) <
            method.IndexOf("InsertAuditEventCoreAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void TransitionAsync_UsesDomainTerminalOperationsBeforeRepositoryWrite()
    {
        var source = File.ReadAllText(FindSource(
            "src", "BA.Dmo.Application", "Modules", "JobOn", "JobOnService.cs"));
        var methodStart = source.IndexOf(
            "public async Task<Result<JobOnLifecycleState, DomainError>> TransitionAsync(",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "/// <summary>\r\n    /// Canonical activity lookup", methodStart,
            StringComparison.Ordinal);
        if (methodEnd < 0)
            methodEnd = source.IndexOf("/// <summary>\n    /// Canonical activity lookup", methodStart,
                StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        Assert.Contains("jobOn.Close(now)", method, StringComparison.Ordinal);
        Assert.Contains("jobOn.Cancel(", method, StringComparison.Ordinal);
        Assert.Contains("jobOn.TransitionTo(request.NewState)", method, StringComparison.Ordinal);
        Assert.Contains("_repository.TransitionLifecycleAsync(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("InsertAuditEventAsync", method, StringComparison.Ordinal);
    }

    private static string FindSource(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BA-DMO repository root.");
    }
}
