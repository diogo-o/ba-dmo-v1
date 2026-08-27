namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// Guards the Registo convergence against static authority demo rows and the
/// filename-only L-prefix leaking into visible Lote content.
/// </summary>
public class ArmazemRecentMovementsGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Registo_RendersMovementBackedRecentListAndFilters()
    {
        var page = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml");
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "armazem.js");
        var program = Read("src", "BA.Dmo.Web", "Program.cs");

        Assert.Contains("Registar movimento", page, StringComparison.Ordinal);
        Assert.Contains("Últimos registos", page, StringComparison.Ordinal);
        Assert.Contains("id=\"recentSearch\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"recentMovement\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"recentLimit\"", page, StringComparison.Ordinal);
        Assert.Contains("/api/armazem/movimentos?limit=60", script, StringComparison.Ordinal);
        Assert.Contains("app.MapGet(\"/api/armazem/movimentos\"", program, StringComparison.Ordinal);
        Assert.DoesNotContain("9389T194", page, StringComparison.Ordinal);
        Assert.DoesNotContain("5447T173", page, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleLot_UsesStoredContentWithoutFilenamePrefix()
    {
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "armazem.js");

        Assert.Contains("appendRecentCell(row, item.lot)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"L\" + item.lot", script, StringComparison.Ordinal);
        Assert.DoesNotContain("`L${item.lot}`", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Consulta_UsesAuthorityStyleRealRowFilters()
    {
        var page = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml");
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "armazem.js");

        Assert.Contains("id=\"queryText\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"queryType\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"queryContext\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"queryVerification\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"queryLimit\"", page, StringComparison.Ordinal);
        Assert.Contains("consultationRows = await api(\"/api/armazem/consulta\")", script, StringComparison.Ordinal);
        Assert.Contains("tr.appendChild(td(row.lot))", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Historico_IsMovementBackedAndDoesNotActivateMovementCorrection()
    {
        var page = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml");
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "armazem.js");

        Assert.Contains("id=\"historyCalendarGrid\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"historyQuery\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"historyToolType\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"historyMovement\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"historyOperator\"", page, StringComparison.Ordinal);
        Assert.Contains("/api/armazem/movimentos?limit=500", script, StringComparison.Ordinal);
        Assert.Contains("appendRecentCell(row, item.lot)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Corrigir movimento", page, StringComparison.Ordinal);
        Assert.DoesNotContain("correctMovement", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Programadas_RemainsDormantWithoutAnActiveTab()
    {
        var page = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml");

        Assert.DoesNotContain("class=\"tab\" data-view=\"programadas\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"programadas\" hidden aria-hidden=\"true\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Saídas programadas", page, StringComparison.Ordinal);
        Assert.DoesNotContain("confirmChecks", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_HasCompactAndPrintSpecificComposition()
    {
        var css = Read("src", "BA.Dmo.Web", "wwwroot", "styles", "modules", "armazem-layout.css");

        Assert.Contains("@media (max-width: 980px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 720px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 375px)", css, StringComparison.Ordinal);
        Assert.Contains("@media print", css, StringComparison.Ordinal);
        Assert.Contains(".armazem-view.active", css, StringComparison.Ordinal);
        Assert.Contains("size: A4 landscape", css, StringComparison.Ordinal);
        Assert.Contains(".dmo-table thead", css, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray()));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BA-DMO.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Repository root (BA-DMO.sln) not found.");
    }
}
