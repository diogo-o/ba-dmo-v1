using System.Text;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Infrastructure.Access;

namespace BA.Dmo.IntegrationTests.Pegamentos;

/// <summary>
/// U-11 — Pegamentos PDF renderer produces valid PDF bytes from the frozen
/// snapshot; renders header, measurements and status. No HTML/browser-print
/// artifacts (GLM-PEG-14).
/// </summary>
public class PegamentoPdfRendererTests
{
    private static PegamentoPdfData Sample() => new()
    {
        Reference = "5447T173",
        ProductionCode = "202601",
        MachineCode = "B1",
        JobOnRevisionId = Guid.NewGuid(),
        CmReference = "5447",
        CmLot = "4",
        CmNominal = 52.00m,
        BqReference = "T173",
        BqNominal = 38.50m,
        MfReference = "MF-1",
        MfNominal = 60.00m,
        Tolerance = 0.20m,
        Status = "Aberto",
        Measurements = new[]
        {
            new PegamentoPdfMeasurementRow { ComponentKey = "CM", ToolNumber = 42, Costura = 52.30m, ContraCostura = 52.00m, Ovalizacao = 0.30m, Media = 52.15m }
        },
        GeneratedAtUtc = new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public void Render_ProducesValidPdfHeader()
    {
        var renderer = new PegamentoPdfRenderer();
        var bytes = renderer.RenderPegamento(Sample());
        var text = Encoding.ASCII.GetString(bytes);

        Assert.Contains("%PDF-1.4", text);
        Assert.Contains("%%EOF", text);
        Assert.StartsWith("%PDF", text);
    }

    [Fact]
    public void Render_IncludesProductionIdentityAndComponentData()
    {
        var renderer = new PegamentoPdfRenderer();
        var text = Encoding.ASCII.GetString(renderer.RenderPegamento(Sample()));

        Assert.Contains("5447T173", text);
        Assert.Contains("202601", text);
        Assert.Contains("Contra-molde", text);
        Assert.Contains("Boquilha", text);
        Assert.Contains("Molde final", text);
        Assert.Contains("OVALIZA", text);
        Assert.Contains("COSTURA", text);
    }

    [Fact]
    public void Render_DoesNotEmitHtmlOrBrowserPrintArtifacts()
    {
        var renderer = new PegamentoPdfRenderer();
        var text = Encoding.ASCII.GetString(renderer.RenderPegamento(Sample()));

        Assert.DoesNotContain("file:///", text);
        Assert.DoesNotContain(".html", text);
        Assert.DoesNotContain("1/1", text);
    }
}