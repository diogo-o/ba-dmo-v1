using System.Text.RegularExpressions;

namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// U-13 static Job On script safety guards. The page injects a catalog label
/// typed by an operator into the Definições row via <c>insertAdjacentHTML</c>
/// (F-07). That label is user-controlled, so it MUST pass through the local
/// <c>esc()</c> helper before interpolation — otherwise an operator could close
/// the <c>&lt;strong&gt;</c> and inject markup. This guard pins the escape at the
/// source: no raw unescaped interpolation of the label is allowed. Static (file
/// content) — no live Supabase/DB.
/// </summary>
public class JobOnScriptSafetyGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string JobOnScript = Path.Combine(
        RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "scripts", "jobon.js");

    private static readonly string ControloScript = Path.Combine(
        RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "scripts", "controlo.js");

    private static readonly string ReparacaoInternaScript = Path.Combine(
        RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "scripts", "reparacao-interna.js");

    [Fact]
    public void CatalogLabel_IsEscaped_BeforeInsertAdjacentHtml()
    {
        var script = File.ReadAllText(JobOnScript);

        // The operator-typed catalog label must be wrapped in the local esc()
        // helper wherever it is interpolated into HTML.
        Assert.Contains("${esc(label)}", script);
    }

    [Fact]
    public void NoRawUnescapedCatalogLabel_IsInterpolatedIntoHtml()
    {
        var script = File.ReadAllText(JobOnScript);

        // Guard against regression: the raw label must never be inserted as-is.
        // The only interpolation of `label` into the markup must be the escaped one.
        var rawInterpolations = Regex.Matches(script, @"\$\{label\}");
        Assert.Empty(rawInterpolations);
    }

    [Fact]
    public void EscHelper_IsDefinedInTheScript()
    {
        var script = File.ReadAllText(JobOnScript);

        // The esc() helper is a local closure and must remain available so the
        // escape is self-contained in this file (consistent with other scripts).
        // It must escape the dangerous character classes via its map + replace.
        Assert.Contains("function esc(", script);
        Assert.Contains("&<>\"'", script);       // the char-class regex covers all five
        Assert.Contains("\"&\" + \"amp;\"", script); // & escapes to &amp;
        Assert.Contains(".replace(", script);
        Assert.Contains("String(value ?? \"\")", script);
    }

    [Fact]
    public void ArticleImage_UsesFileSelection_NotPersistedBrowserDirectoryHandles()
    {
        var script = File.ReadAllText(JobOnScript);

        Assert.Contains("file.name", script, StringComparison.Ordinal);
        Assert.Contains("persistImageAction", script, StringComparison.Ordinal);
        Assert.DoesNotContain("showDirectoryPicker", script, StringComparison.Ordinal);
        Assert.DoesNotContain("indexedDB", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cria uma nova revisão", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JobOnCrossModuleLinks_ActivateRequestedControloSection()
    {
        var script = File.ReadAllText(ControloScript);

        Assert.Contains("params.get('jobOn')", script, StringComparison.Ordinal);
        Assert.Contains("params.get('section')", script, StringComparison.Ordinal);
        Assert.Contains("selectSection", script, StringComparison.Ordinal);
    }

    [Fact]
    public void JobOnRepairLink_OpensHistoryFilteredByStableJobOnIdentity()
    {
        var script = File.ReadAllText(ReparacaoInternaScript);

        Assert.Contains("query.get('jobOnId')", script, StringComparison.Ordinal);
        Assert.Contains("query.get('line')", script, StringComparison.Ordinal);
        Assert.Contains("query.get('view')", script, StringComparison.Ordinal);
        Assert.Contains("q.set('jobOnId', f.jobOnId)", script, StringComparison.Ordinal);
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
