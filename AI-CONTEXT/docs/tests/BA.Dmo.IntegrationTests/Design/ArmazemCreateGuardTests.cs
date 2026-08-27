namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// Guards the confirmed two-owner create workflow: Ferramentas creates the
/// master first; Armazém records physical Entrada second.
/// </summary>
public class ArmazemCreateGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Surface_IsGatedByFerramentasAndOnlyOffersWarehouseTypes()
    {
        var pageModel = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml.cs");
        var page = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml");

        Assert.Contains("CanCreateNewTool", pageModel, StringComparison.Ordinal);
        Assert.Contains("FerramentasModuleCatalog.ModuleId", pageModel, StringComparison.Ordinal);
        Assert.Contains("@if (Model.CanCreateNewTool)", page, StringComparison.Ordinal);
        Assert.Contains("data-open=\"novo\"", page, StringComparison.Ordinal);

        var selectStart = page.IndexOf("<select id=\"novoType\"", StringComparison.Ordinal);
        var selectEnd = page.IndexOf("</select>", selectStart, StringComparison.Ordinal);
        var typeSelector = page[selectStart..selectEnd];
        Assert.Contains("value=\"CM\"", typeSelector, StringComparison.Ordinal);
        Assert.Contains("value=\"MF\"", typeSelector, StringComparison.Ordinal);
        Assert.Contains("value=\"BQ\"", typeSelector, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"PU\"", typeSelector, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"CS\"", typeSelector, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_CreatesMasterBeforeEntryAndRecoversPartialFailure()
    {
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "armazem.js");
        var masterCall = script.IndexOf("/api/ferramentas/reference", StringComparison.Ordinal);
        var entryCall = script.IndexOf("/api/armazem/entrada", masterCall, StringComparison.Ordinal);

        Assert.True(masterCall >= 0, "Canonical Ferramentas creation call is missing.");
        Assert.True(entryCall > masterCall, "Armazém Entrada must happen after master creation.");
        Assert.Contains("Master criado em Ferramentas; a Entrada não foi registada", script, StringComparison.Ordinal);
        Assert.Contains("el(\"entradaType\").value = v.novoType", script, StringComparison.Ordinal);
        Assert.Contains("el(\"entradaRef\").value = v.novoRef", script, StringComparison.Ordinal);
        Assert.Contains("el(\"entradaLot\").value = v.novoLot", script, StringComparison.Ordinal);
        Assert.Contains("el(\"entradaForm\").hidden = false", script, StringComparison.Ordinal);
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
