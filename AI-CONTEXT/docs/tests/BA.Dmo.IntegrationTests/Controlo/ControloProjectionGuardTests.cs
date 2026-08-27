namespace BA.Dmo.IntegrationTests.Controlo;

public class ControloProjectionGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ProductionContextLookup_ProjectsExactlyTheFiveResumoFamilies()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Infrastructure", "Access",
            "DapperControloProductionContextLookup.cs"));

        Assert.Contains(
            "c.family IN ('MP_CM', 'MF', 'BQ', 'PU', 'CS')",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "c.family IN ('MP_CM', 'MF', 'BQ')",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BA-DMO.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root (BA-DMO.sln) not found.");
    }
}
