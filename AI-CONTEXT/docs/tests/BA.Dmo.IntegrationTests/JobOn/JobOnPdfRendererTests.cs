using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Infrastructure.Access;

namespace BA.Dmo.IntegrationTests.JobOnPdfRendering;

public class JobOnPdfRendererTests
{
    [Fact]
    public void Renderer_DrawsReferenceImage_ExactlyOnce_OnRequiredPage()
    {
        var jpeg = new byte[]
        {
            0xFF, 0xD8,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x02, 0x00, 0x03, 0x03,
            0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
            0xFF, 0xD9
        };
        var renderer = new JobOnPdfRenderer();

        var pdf = renderer.RenderJobOnDocument(new JobOnPdfData
        {
            Reference = "5447T173",
            ProductionCode = "202601",
            MachineCode = "B1",
            ImageBytes = jpeg,
            ImageMimeType = "image/jpeg"
        });
        var text = System.Text.Encoding.UTF8.GetString(pdf);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "/Im1 Do"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "/XObject<</Im1 15 0 R>>"));
        Assert.Contains("15 0 obj<</Type/XObject/Subtype/Image", text, StringComparison.Ordinal);
        Assert.Contains("/Filter [/ASCIIHexDecode /DCTDecode]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_WithoutReferenceImage_DoesNotCreateImageObject()
    {
        var pdf = new JobOnPdfRenderer().RenderJobOnDocument(new JobOnPdfData());
        var text = System.Text.Encoding.UTF8.GetString(pdf);

        Assert.DoesNotContain("/Im1 Do", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Subtype/Image", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_EmbedsPngReferenceImage_WithPdfCompatibleFilter()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var renderer = new JobOnPdfRenderer();

        var pdf = renderer.RenderJobOnDocument(new JobOnPdfData
        {
            ImageBytes = png,
            ImageMimeType = "image/png"
        });
        var text = System.Text.Encoding.UTF8.GetString(pdf);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "/Im1 Do"));
        Assert.Contains("/Filter [/ASCIIHexDecode /FlateDecode]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_ProjectsPersistedHeaderToolsCalibresAndNotesAcrossFourPages()
    {
        static JobOnPdfComponent Tool(string reference, string lot) => new()
        {
            Reference = reference,
            Lot = lot,
            Stock = 12m,
            MachineQuantity = 4m,
            Usage = 0.75m,
            Notes = $"NOTA-{reference}",
            Fields = new Dictionary<string, string> { ["fundo_final"] = "FF-77" }
        };

        var data = new JobOnPdfData
        {
            Reference = "REF-LIVE",
            ProductionCode = "PROD-77",
            MachineCode = "C3",
            Sections = 24,
            DropCount = 2m,
            Weight = 123.45m,
            TypeSnapshot = "TIPO-LIVE",
            StopSnapshot = "STOP-LIVE",
            ProcessSnapshot = "PROCESS-LIVE",
            GeneralNotes = "OBS-LIVE",
            PlannedStartAt = new DateTimeOffset(2026, 9, 8, 8, 0, 0, TimeSpan.Zero),
            PlannedEndAt = new DateTimeOffset(2026, 9, 9, 8, 0, 0, TimeSpan.Zero),
            Cm = Tool("CM-1", "LCM"),
            Mf = Tool("MF-1", "LMF"),
            Tp = Tool("TP-1", "LTP"),
            Bq = Tool("BQ-1", "LBQ"),
            An = Tool("AN-1", "LAN"),
            Pu = Tool("PU-1", "LPU"),
            Arr = Tool("ARR-1", "LARR"),
            Pi = Tool("PI-1", "LPI"),
            Cs = Tool("CS-1", "LCS"),
            Fo = Tool("FO-1", "LFO"),
            CalibreRows = new[] { new JobOnPdfCalibreRow("CAL-LIVE", "31.45", 3m) }
        };

        var text = System.Text.Encoding.UTF8.GetString(new JobOnPdfRenderer().RenderJobOnDocument(data));

        Assert.Contains("/Count 4", text, StringComparison.Ordinal);
        Assert.Contains("FICHA DE ARTIGO", text, StringComparison.Ordinal);
        Assert.Contains("Job-ON Moldes", text, StringComparison.Ordinal);
        Assert.Contains("TRABALHO DE EQUIPA", text, StringComparison.Ordinal);
        foreach (var value in new[]
                 {
                     "REF-LIVE", "PROD-77", "TIPO-LIVE", "STOP-LIVE", "PROCESS-LIVE", "OBS-LIVE",
                     "CM-1", "MF-1", "TP-1", "BQ-1", "AN-1", "PU-1", "ARR-1", "PI-1", "CS-1", "FO-1",
                     "CAL-LIVE", "31.45"
                 })
            Assert.Contains(value, text, StringComparison.Ordinal);
        Assert.Contains("Stock: 12", text, StringComparison.Ordinal);
        Assert.Contains("Qtd. m", text, StringComparison.Ordinal);
        Assert.Contains("Qtd: 3", text, StringComparison.Ordinal);
    }
}
