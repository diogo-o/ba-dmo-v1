namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// Static acceptance guards for the confirmed normal BQ warehouse path. BQ is
/// available wherever Armazém selects a tool type; PU/CS remain outside stock.
/// </summary>
public class ArmazemBqGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void RequiredTypeSelectors_ExposeBQButNotPuOrCs()
    {
        var page = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml"));

        foreach (var selectorId in new[]
        {
            "entradaType",
            "saidaType",
            "novoType",
            "queryType",
            "historyToolType"
        })
        {
            var selectStart = page.IndexOf($"<select id=\"{selectorId}\"", StringComparison.Ordinal);
            Assert.True(selectStart >= 0, $"Selector {selectorId} is missing.");
            var selectEnd = page.IndexOf("</select>", selectStart, StringComparison.Ordinal);
            Assert.True(selectEnd > selectStart, $"Selector {selectorId} is not closed.");
            var select = page[selectStart..selectEnd];

            Assert.Contains("value=\"BQ\"", select, StringComparison.Ordinal);
            Assert.DoesNotContain("value=\"PU\"", select, StringComparison.Ordinal);
            Assert.DoesNotContain("value=\"CS\"", select, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BA-DMO.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Repository root (BA-DMO.sln) not found.");
    }
}
