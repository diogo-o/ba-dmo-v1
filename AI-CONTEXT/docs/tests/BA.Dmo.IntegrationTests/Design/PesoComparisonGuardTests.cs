namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// Static acceptance guards for the confirmed Peso comparison contract: creation
/// is inside Novo Controlo, pairing is explicit, and comparison values are only
/// per-CM glass weight. Capacity remains a normal control result, never a
/// previous-production comparison dimension.
/// </summary>
public class PesoComparisonGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void OperatorFlow_RequiresConfirmationPairingReviewAndExplicitSubmit()
    {
        var page = Read("src", "BA.Dmo.Web", "Pages", "Peso", "Index.cshtml");
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "peso.js");

        Assert.Contains("comparisonPreviousControl", page, StringComparison.Ordinal);
        Assert.Contains("confirmComparisonPrevious", page, StringComparison.Ordinal);
        Assert.Contains("comparisonPairingTable", page, StringComparison.Ordinal);
        Assert.Contains("createComparison", page, StringComparison.Ordinal);
        Assert.Contains("submitComparison", page, StringComparison.Ordinal);
        Assert.True(page.IndexOf("Resultados", StringComparison.Ordinal) <
                    page.IndexOf("comparisonBuilder", StringComparison.Ordinal));
        Assert.Contains("currentCmNumber", script, StringComparison.Ordinal);
        Assert.Contains("previousCmNumber", script, StringComparison.Ordinal);
        Assert.Contains("invalidateComparisonMapping", script, StringComparison.Ordinal);
        Assert.Contains("Tabela criada; reveja antes de enviar", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparisonSurfaces_RemoveGlobalWaterAndCapacityComparison()
    {
        var operatorPage = Read("src", "BA.Dmo.Web", "Pages", "Peso", "Index.cshtml");
        var responsiblePage = Read("src", "BA.Dmo.Web", "Pages", "Peso", "Responsavel.cshtml");
        var renderer = Read("src", "BA.Dmo.Infrastructure", "Access", "PesoSingleFilePdfRenderer.cs");

        Assert.DoesNotContain("Diferença anterior", operatorPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Comparação global", responsiblePage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Capacidade anterior", responsiblePage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COMPARAÇÃO COM A ÚLTIMA PRODUÇÃO", renderer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cap. ant.", renderer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMPARA\\u00C7\\u00C3O POR CM \\u2014 PESO DO VIDRO", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void LotPrefix_IsReservedForFilename_NotPesoContent()
    {
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "peso.js");
        var responsiblePage = Read("src", "BA.Dmo.Web", "Pages", "Peso", "Responsavel.cshtml");
        var service = Read("src", "BA.Dmo.Application", "Modules", "Peso", "PesoService.cs");

        Assert.DoesNotContain("<span>Lote</span><strong>L", script, StringComparison.Ordinal);
        Assert.DoesNotContain(" · L@c.Lote", responsiblePage, StringComparison.Ordinal);
        Assert.Contains(" · Lote {control.Lote}", service, StringComparison.Ordinal);
        Assert.Contains("__L{lote}.pdf", service, StringComparison.Ordinal);
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
