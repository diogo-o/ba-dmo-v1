namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// Static surface guards for the confirmed auditable location-correction flow.
/// Functional atomicity and history preservation are covered by service tests.
/// </summary>
public class ArmazemCorrectionGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Consultation_ExposesSelectionDrivenCorrectionCard()
    {
        var page = Read("src", "BA.Dmo.Web", "Pages", "Armazem", "Index.cshtml");
        var script = Read("src", "BA.Dmo.Web", "wwwroot", "scripts", "armazem.js");

        Assert.Contains("id=\"correctLocation\" disabled", page, StringComparison.Ordinal);
        Assert.Contains("id=\"correctionForm\"", page, StringComparison.Ordinal);
        Assert.Contains("Posição registada", page, StringComparison.Ordinal);
        Assert.Contains("Posição encontrada (4 dígitos)", page, StringComparison.Ordinal);
        Assert.Contains("Não está fisicamente presente no Armazém", page, StringComparison.Ordinal);
        Assert.Contains("selectedConsultationRow = row", script, StringComparison.Ordinal);
        Assert.Contains("/api/armazem/corrigir-localizacao", script, StringComparison.Ordinal);
        Assert.Contains("correctionNotPresent", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointAndService_UseDedicatedAuditableCorrectionPath()
    {
        var program = Read("src", "BA.Dmo.Web", "Program.cs");
        var service = Read("src", "BA.Dmo.Application", "Modules", "Armazem", "ArmazemService.cs");

        Assert.Contains("MapPost(\"/api/armazem/corrigir-localizacao\"", program, StringComparison.Ordinal);
        Assert.Contains("CorrectLocationAsync", service, StringComparison.Ordinal);
        Assert.Contains("correcao_localizacao", service, StringComparison.Ordinal);
        Assert.Contains("armazem.corrigir_localizacao", service, StringComparison.Ordinal);
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
