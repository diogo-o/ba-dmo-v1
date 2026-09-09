namespace BA.Dmo.IntegrationTests.Persistence;

public sealed class JobOnRevisionJsonBindingTests
{
    [Fact]
    public void RevisionInsert_BindsEveryJsonSnapshotWithExplicitJsonbCast()
    {
        var source = File.ReadAllText(FindSource("src", "BA.Dmo.Infrastructure", "Access", "DapperJobOnRepository.cs"));

        Assert.Contains("@ProductionSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@ReferenceSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@MachineSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@DatesSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@Sections::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@TypeSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@StopSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@WeightSnapshot::jsonb", source, StringComparison.Ordinal);
        Assert.Contains("@ProcessSnapshot::jsonb", source, StringComparison.Ordinal);
    }

    private static string FindSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository source.");
    }
}
