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
}
