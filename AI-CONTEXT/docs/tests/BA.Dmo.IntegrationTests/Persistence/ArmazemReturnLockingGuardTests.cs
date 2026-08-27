namespace BA.Dmo.IntegrationTests.Persistence;

public sealed class ArmazemReturnLockingGuardTests
{
    [Fact]
    public void ConfirmReturnAsync_LocksLocationBeforeOccupancyCheckAndMutation()
    {
        var source = File.ReadAllText(FindRepositorySource());
        var methodStart = source.IndexOf(
            "public async Task<Result<bool, DomainError>> ConfirmReturnAsync(",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private async Task InsertMovementAsync(", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        var locationLock = method.IndexOf("const string lockLocation", StringComparison.Ordinal);
        var occupancyRead = method.IndexOf("const string occupant", StringComparison.Ordinal);
        var conflictCheck = method.IndexOf("if (existing is not null)", StringComparison.Ordinal);
        var stockInsert = method.IndexOf("INSERT INTO warehouse_stock", StringComparison.Ordinal);
        var movementInsert = method.IndexOf("InsertMovementAsync", StringComparison.Ordinal);

        Assert.True(locationLock >= 0);
        Assert.Contains("FROM warehouse_locations", method[locationLock..], StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", method[locationLock..occupancyRead], StringComparison.Ordinal);
        Assert.True(locationLock < occupancyRead);
        Assert.Contains("FOR UPDATE", method[occupancyRead..conflictCheck], StringComparison.Ordinal);
        Assert.True(occupancyRead < conflictCheck);
        Assert.Contains("ARMZ_REPAIR_POSITION_OCCUPIED", method[conflictCheck..stockInsert], StringComparison.Ordinal);
        Assert.True(conflictCheck < stockInsert);
        Assert.True(stockInsert < movementInsert);
        Assert.Contains("uow.Transaction", method, StringComparison.Ordinal);
    }

    private static string FindRepositorySource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "BA.Dmo.Infrastructure",
                "Access", "DapperArmazemRepairMovementRepository.cs");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BA-DMO repository root.");
    }
}
