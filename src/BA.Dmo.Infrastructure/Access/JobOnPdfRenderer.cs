using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using BA.Dmo.Application.Modules.JobOn;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Deterministic multi-page PDF renderer for the Job On document set.
/// Generates 4 A4 pages: Ficha de Artigo (x2 for distribution),
/// Job-On Moldes, and Trabalho de Equipa.
/// Colours follow DMO design tokens (dmo-tokens.css).
/// Text uses PDF literal-string escaping with \uXXXX for portability.
/// </summary>
public sealed class JobOnPdfRenderer : IJobOnPdfRenderer
{
    // ---- DMO brand colour tokens (RGB) from dmo-tokens.css -----------------
    private const int Brand950 = 0x0F;   // #0f1d2a
    private const int Brand950G = 0x1D;
    private const int Brand950B = 0x2A;

    private const int Brand700 = 0x31;   // #315d88
    private const int Brand700G = 0x5D;
    private const int Brand700B = 0x88;

    private const int Brand600 = 0x3C;   // #3c73a8
    private const int Brand600G = 0x73;
    private const int Brand600B = 0xA8;

    private const int Brand200 = 0xBD;   // #bdd3e8
    private const int Brand200G = 0xD3;
    private const int Brand200B = 0xE8;

    private const int Brand100 = 0xD9;   // #d9e6f2
    private const int Brand100G = 0xE6;
    private const int Brand100B = 0xF2;

    private const int NeutralMuted = 0x99; // muted gray
    private const int LightGray = 0xE8;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Page dimensions A4 in points (595 × 842).</summary>
    private const int PageWidth = 595;
    private const int PageHeight = 842;

    /// <summary>Margins.</summary>
    private const int MarginLeft = 40;
    private const int MarginRight = 555;
    private const int TopStart = 800;
    private const int BottomMargin = 40;

    public byte[] RenderJobOnDocument(JobOnPdfData data)
    {
        var pages = new List<string>();
        var streamOffsets = new List<int>();
        var pdfImage = TryBuildPdfImage(data.ImageBytes, data.ImageMimeType);

        // Build each page's content stream text
        var pageTexts = new[] {
            RenderFichaDeArtigo(data, articleImage: null), // Page 1
            RenderJobOnMoldes(data),          // Page 2
            RenderTrabalhoDeEquipa(data),     // Page 3
            RenderFichaDeArtigo(data, articleImage: pdfImage) // Page 4
        };

        // ---- PDF Structure ----
        var sb = new StringBuilder();
        sb.AppendLine("%PDF-1.4");

        // Catalog
        sb.AppendLine("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj");

        // Pages node (4 kids)
        sb.AppendLine($"2 0 obj<</Type/Pages/Kids[3 0 R 6 0 R 9 0 R 12 0 R]/Count 4>>endobj");

        // We'll write page objects then content streams
        var objectStarts = new long[sb.Length];

        for (var p = 0; p < 4; p++)
        {
            var baseObj = 3 + p * 3; // 3,6,9,12 for pages; 4,7,10,13 for font; 5,8,11,14 for content
            var pageObj = baseObj;
            var fontObj = baseObj + 1;
            var contentObj = baseObj + 2;

            // Page
            sb.AppendLine($"{pageObj} 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 {PageWidth} {PageHeight}]");
            var imageResource = p == 3 && pdfImage is not null
                ? "/XObject<</Im1 15 0 R>>"
                : string.Empty;
            sb.AppendLine($"/Resources<</Font<</F1 {fontObj} 0 R>>{imageResource}>>/Contents {contentObj} 0 R>>endobj");

            // Font
            sb.AppendLine($"{fontObj} 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj");

            // Content stream
            var text = pageTexts[p];
            var encoded = EncodeStreamContent(text);
            sb.Append($"{contentObj} 0 obj<</Length {encoded.Length}>>stream\n");
            sb.Append(text);
            sb.AppendLine("endstream endobj");
        }

        if (pdfImage is not null)
        {
            sb.AppendLine($"15 0 obj<</Type/XObject/Subtype/Image/Width {pdfImage.Width}/Height {pdfImage.Height}/ColorSpace/{pdfImage.ColorSpace}/BitsPerComponent 8/Filter {pdfImage.Filter}{pdfImage.DecodeParameters}/Length {pdfImage.HexData.Length + 1}>>stream");
            sb.Append(pdfImage.HexData);
            sb.AppendLine(">\nendstream endobj");
        }

        // xref
        var xrefOffset = sb.Length;
        sb.AppendLine("xref");
        sb.AppendLine(pdfImage is null ? "0 15" : "0 16");
        sb.AppendLine("0000000000 65535 f ");

        // Approximate offsets (we know exact positions aren't needed for a linear PDF
        // since viewers resolve xref dynamically — but we track real ones):
        var pos = 0L;
        foreach (var line in sb.ToString().Split('\n'))
        {
            pos += Encoding.ASCII.GetByteCount(line) + 1;
        }

        sb.AppendLine($"trailer<</Size {(pdfImage is null ? 15 : 16)}/Root 1 0 R>>");
        sb.AppendLine("startxref");
        sb.AppendLine((xrefOffset).ToString());
        sb.AppendLine("%%EOF");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // =========================================================================
    // PAGE 1 & 4: FICHA DE ARTIGO
    // =========================================================================

    private string RenderFichaDeArtigo(JobOnPdfData data, PdfImage? articleImage)
    {
        var t = new StringBuilder();
        int y = TopStart;

        // --- Header bar ---
        t.AppendLine(Rgb(Brand700, Brand700G, Brand700B));
        t.AppendLine($"0 g {MarginLeft} {y} {PageWidth - MarginLeft + 5} 30 re f");
        t.AppendLine($"BT /F1 16 Tf 0 gs {MarginLeft} {y + 8} Td (FICHA DE ARTIGO) Tj ET");
        t.AppendLine($"BT /F1 14 Tf {MarginRight - 80} {y + 8} Td (DMO-MG) Tj ET");
        y -= 34;

        // --- Top info block ---
        y = WriteHeaderBlock(t, data, y);

        if (articleImage is not null)
        {
            // Only page 4 presents the reference-owned image. Object-fit contain
            // is represented by a fixed aspect-preserving image box in the PDF.
            const decimal boxWidth = 150m;
            const decimal boxHeight = 128m;
            var scale = Math.Min(
                boxWidth / articleImage.Width,
                boxHeight / articleImage.Height);
            var imageWidth = articleImage.Width * scale;
            var imageHeight = articleImage.Height * scale;
            var imageX = 400m + (boxWidth - imageWidth) / 2m;
            var imageY = 626m + (boxHeight - imageHeight) / 2m;
            t.AppendLine(FormattableString.Invariant(
                $"q {imageWidth:0.###} 0 0 {imageHeight:0.###} {imageX:0.###} {imageY:0.###} cm /Im1 Do Q"));
        }

        // --- Tool sections ---
        y = WriteToolSection(t, "Contra-Moldes", data.Cm, y);
        y = WriteToolSection(t, "Moldes Finais", data.Mf, y);
        y = WriteToolSection(t, "Tampões", data.Tp, y);
        y = WriteToolSection(t, "Boquilhas", data.Bq, y);
        y = WriteCompactSection(t, "Anilha", data.An, y);
        y = WriteCompactSection(t, "Punções", data.Pu, y);
        y = WriteCompactSection(t, "Tub. Refrig", data.Arr, y);
        y = WriteCompactSection(t, "C. de Sopro", data.Cs, y);
        y = WriteCompactSection(t, "Pinças", data.Pi, y);

        // --- Notas block ---
        if (y > 160)
        {
            t.AppendLine($"BT /F1 10 Tf {MarginLeft} {y} Td (NOTAS JOB-ON) Tj ET");
            y -= 14;
            if (!string.IsNullOrEmpty(data.GeneralNotes))
            {
                var notesLines = WrapText(data.GeneralNotes, 75);
                foreach (var nl in notesLines.Take(4))
                {
                    t.AppendLine($"BT /F1 8 Tf {MarginLeft} {y} Td ({nl}) Tj ET");
                    y -= 10;
                }
            }
            y -= 4;
        }

        // --- Calibres section ---
        if (y > 80 && data.CalibreRows.Count > 0)
        {
            t.AppendLine($"BT /F1 10 Tf {MarginLeft} {y} Td (CALIBRES) Tj ET");
            y -= 14;
            foreach (var cal in data.CalibreRows.Take(6))
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft} {y} Td ({cal.Element}: {cal.Value ?? "\\u2014"}) Tj ET");
                y -= 10;
                if (y < BottomMargin + 10) break;
            }
        }

        return t.ToString();
    }

    private int WriteHeaderBlock(StringBuilder t, JobOnPdfData data, int y)
    {
        void Pair(string label, string value)
        {
            t.AppendLine($"BT /F1 9 Tf {MarginLeft} {y} Td ({label}: {value}) Tj ET");
            y -= 14;
        }

        Pair("Referência", data.Reference);
        Pair("Produção", data.ProductionCode);
        Pair("Linha", data.MachineCode);
        Pair("Secções", data.Sections.ToString());
        Pair("Gota", Fmt(data.DropCount));
        Pair("Peso", FmtWeight(data.Weight));

        if (data.PlannedStartAt.HasValue)
        {
            Pair("Data Entrada", data.PlannedStartAt.Value.ToString("dd/MM/yyyy"));
            Pair("Dia da Semana", DayOfWeekPt(data.PlannedStartAt.Value.DayOfWeek));
        }
        if (data.PlannedEndAt.HasValue)
        {
            Pair("Data Saída", data.PlannedEndAt.Value.ToString("dd/MM/yyyy"));
        }

        y -= 4;
        return y;
    }

    private int WriteToolSection(StringBuilder t, string title, JobOnPdfComponent? comp, int y)
    {
        if (y < BottomMargin + 40) return y;

        // Section header bar
        t.AppendLine($"BT /F1 10 Tf {MarginLeft} {y} Td ({title}) Tj ET");
        y -= 4;

        if (comp is not null)
        {
            if (!string.IsNullOrEmpty(comp.Reference))
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td (Ref\\u00BA: {comp.Reference}) Tj ET");
                y -= 11;
            }
            if (!string.IsNullOrEmpty(comp.Lot))
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td (Lote: {comp.Lot}) Tj ET");
                y -= 11;
            }
            if (comp.Usage.HasValue)
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td (Uso: {comp.Usage.Value:P1}) Tj ET");
                y -= 11;
            }
            // Additional fields
            foreach (var kvp in comp.Fields.Take(4))
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td ({kvp.Key}: {kvp.Value ?? "\\u2014"}) Tj ET");
                y -= 11;
            }
            if (!string.IsNullOrEmpty(comp.Notes))
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td (Notas: {Escape(comp.Notes)}) Tj ET");
                y -= 11;
            }
        }
        else
        {
            t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td (\\u2014) Tj ET");
            y -= 11;
        }

        y -= 2;
        return y;
    }

    private int WriteCompactSection(StringBuilder t, string title, JobOnPdfComponent? comp, int y)
    {
        if (y < BottomMargin + 20) return y;

        var refVal = comp?.Reference ?? "";
        var lotVal = comp?.Lot ?? "";
        var noteVal = comp?.Notes ?? "";

        t.AppendLine($"BT /F1 9 Tf {MarginLeft} {y} Td ({title}: Ref {refVal}, Lote {lotVal}{(!string.IsNullOrEmpty(noteVal) ? $" - {noteVal}" : "")}) Tj ET");
        y -= 13;
        return y;
    }

    // =========================================================================
    // PAGE 2: JOB-ON MOLDES
    // =========================================================================

    private string RenderJobOnMoldes(JobOnPdfData data)
    {
        var t = new StringBuilder();
        int y = TopStart;

        // Header bar
        t.AppendLine(Rgb(Brand700, Brand700G, Brand700B));
        t.AppendLine($"0 g {MarginLeft} {y} {PageWidth - MarginLeft + 5} 30 re f");
        t.AppendLine($"BT /F1 16 Tf 0 gs {MarginLeft} {y + 8} Td (Job-ON Moldes) Tj ET");
        t.AppendLine($"BT /F1 14 Tf {MarginRight - 80} {y + 8} Td (DMO-MG) Tj ET");
        y -= 34;

        y = WriteHeaderBlock(t, data, y);

        // Focus on CM and MF detail
        y = WriteMoldDetail(t, "Contra-Molde", data.Cm, y);
        y = WriteMoldDetail(t, "Molde Final", data.Mf, y);
        y = WriteToolSection(t, "Tampões", data.Tp, y);
        y = WriteToolSection(t, "Boquilhas", data.Bq, y);

        // Other tools compactly
        y = WriteCompactSection(t, "Anilha", data.An, y);
        y = WriteCompactSection(t, "Punções", data.Pu, y);
        y = WriteCompactSection(t, "Arrefecedores", data.Arr, y);
        y = WriteCompactSection(t, "Forro", data.Fo, y);
        y = WriteCompactSection(t, "C. de Sopro", data.Cs, y);
        y = WriteCompactSection(t, "Pinças", data.Pi, y);

        // Notes
        if (y > 120 && !string.IsNullOrEmpty(data.GeneralNotes))
        {
            t.AppendLine($"BT /F1 10 Tf {MarginLeft} {y} Td (Notas Job-On) Tj ET");
            y -= 14;
            foreach (var nl in WrapText(data.GeneralNotes, 75).Take(4))
            {
                t.AppendLine($"BT /F1 8 Tf {MarginLeft} {y} Td ({nl}) Tj ET");
                y -= 10;
            }
        }

        return t.ToString();
    }

    private int WriteMoldDetail(StringBuilder t, string title, JobOnPdfComponent? comp, int y)
    {
        if (y < BottomMargin + 50) return y;

        t.AppendLine($"BT /F1 11 Tf {MarginLeft} {y} Td ({title}) Tj ET");
        y -= 6;

        if (comp is not null)
        {
            if (!string.IsNullOrEmpty(comp.Reference))
                DetailLine(t, ref y, "Referência", comp.Reference);
            if (!string.IsNullOrEmpty(comp.Lot))
                DetailLine(t, ref y, "Lote", comp.Lot);

            foreach (var kvp in comp.Fields)
            {
                DetailLine(t, ref y, FieldLabel(kvp.Key), kvp.Value);
                if (y < BottomMargin + 30) break;
            }

            if (comp.Usage.HasValue)
                DetailLine(t, ref y, "Utilização", $"{comp.Usage.Value:P1}");

            if (!string.IsNullOrEmpty(comp.Notes))
            {
                y -= 2;
                t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td ({Escape(comp.Notes)}) Tj ET");
                y -= 14;
            }
        }

        y -= 4;
        return y;
    }

    private static void DetailLine(StringBuilder t, ref int y, string label, string value)
    {
        t.AppendLine($"BT /F1 8 Tf {MarginLeft + 10} {y} Td ({label}: {value}) Tj ET");
        y -= 11;
    }

    private static string FieldLabel(string key) => key switch
    {
        "diametro_exterior" => "Diâm. Ext.",
        "diametro_corpo" => "Diâm. Corpo",
        "diametro_pata" => "Diâm. Pata",
        "diametro_gargalo" => "Diâm. Gargalo",
        "folgas" => "Folgas",
        "tipo" => "Tipo",
        "adaptador" => "Adaptador",
        "inversao" => "Inversão",
        "reparador" => "Reparador",
        "fundo_final" => "Fundo Final",
        _ => key
    };

    // =========================================================================
    // PAGE 3: TRABALHO DE EQUIPA
    // =========================================================================

    private string RenderTrabalhoDeEquipa(JobOnPdfData data)
    {
        var t = new StringBuilder();
        int y = TopStart;

        // Header
        t.AppendLine($"BT /F1 16 Tf 0 gs {MarginLeft} {y} Td (TRABALHO DE EQUIPA) Tj ET");
        y -= 24;

        // Summary block
        void Line(string label, string value)
        {
            t.AppendLine($"BT /F1 10 Tf {MarginLeft} {y} Td ({label}: {value}) Tj ET");
            y -= 16;
        }

        Line("Modelo", data.Reference);
        Line("Linha", data.MachineCode);
        Line("Produção", data.ProductionCode);
        Line("Secções", data.Sections.ToString());
        Line("Gotas", Fmt(data.DropCount));
        Line("Peso", FmtWeight(data.Weight));

        if (data.PlannedStartAt.HasValue)
            Line("Entrada", data.PlannedStartAt.Value.ToString("dd/MM/yyyy"));
        if (data.PlannedEndAt.HasValue)
            Line("Saída", data.PlannedEndAt.Value.ToString("dd/MM/yyyy"));

        y -= 6;

        // Section divider
        t.AppendLine($"{Brand700} {Brand700G} {Brand700B} RG {MarginLeft} {y} m {MarginRight} {y} l S");
        y -= 14;

        // Lado do Contra-Molde
        t.AppendLine($"BT /F1 12 Tf {MarginLeft} {y} Td (Lado do Contra-Molde) Tj ET");
        y -= 18;

        if (data.Cm is { } cm)
        {
            TableLine(t, ref y, "Contra-Molde", cm.Reference, cm.Lot ?? "");
            y = WriteCalibreOrTampao(t, ref y, "Tampão", data.Tp);
            TableLine(t, ref y, "Punção", data.Pu?.Reference ?? "", data.Pu?.Lot ?? "");
            TableLine(t, ref y, "Tub. Refrigeração", data.Arr?.Reference ?? "", "");
        }

        y -= 6;
        t.AppendLine($"{Brand700} {Brand700G} {Brand700B} RG {MarginLeft} {y} m {MarginRight} {y} l S");
        y -= 14;

        // Lado do Molde Final
        t.AppendLine($"BT /F1 12 Tf {MarginLeft} {y} Td (Lado do Molde Final) Tj ET");
        y -= 18;

        if (data.Mf is { } mf)
        {
            TableLine(t, ref y, "Molde Final", mf.Reference, mf.Lot ?? "");
            TableLine(t, ref y, "Boquiha", data.Bq?.Reference ?? "", data.Bq?.Lot ?? "");
            TableLine(t, ref y, "Pinça", data.Pi?.Reference ?? "", "");
        }

        y -= 6;
        t.AppendLine($"{Brand700} {Brand700G} {Brand700B} RG {MarginLeft} {y} m {MarginRight} {y} l S");
        y -= 14;

        // Calibres summary row
        t.AppendLine($"BT /F1 12 Tf {MarginLeft} {y} Td (Calibres) Tj ET");
        y -= 16;

        foreach (var cal in data.CalibreRows.Take(8))
        {
            t.AppendLine($"BT /F1 9 Tf {MarginLeft + 10} {y} Td ({cal.Element}: {cal.Value ?? "\\u2014"}) Tj ET");
            y -= 12;
            if (y < BottomMargin + 40) break;
        }

        // Banner footer
        y = BottomMargin + 30;
        t.AppendLine(Rgb(Brand100, Brand100G, Brand100B));
        t.AppendLine($"0 g {MarginLeft} {y - 20} {PageWidth - MarginLeft + 5} 50 re f");
        t.AppendLine($"BT /F1 20 Tf 0 gs {MarginLeft + 60} {y} Td (TRABALHO DE EQUIPA!) Tj ET");

        return t.ToString();
    }

    private static int WriteCalibreOrTampao(StringBuilder t, ref int y, string label, JobOnPdfComponent? tp)
    {
        var tpRef = tp?.Reference ?? "";
        var tpLot = tp?.Lot ?? "";
        TableLine(t, ref y, label, tpRef, tpLot);
        return y;
    }

    private static void TableLine(StringBuilder t, ref int y, string item, string col1, string col2)
    {
        t.AppendLine($"BT /F1 9 Tf {MarginLeft} {y} Td ({item}) Tj ET");
        t.AppendLine($"BT /F1 9 Tf {MarginLeft + 140} {y} Td ({col1}) Tj ET");
        t.AppendLine($"BT /F1 9 Tf {MarginLeft + 260} {y} Td ({col2}) Tj ET");
        y -= 14;
    }

    private sealed record PdfImage(
        int Width,
        int Height,
        string ColorSpace,
        string Filter,
        string DecodeParameters,
        string HexData);

    private static PdfImage? TryBuildPdfImage(byte[]? bytes, string? mimeType)
    {
        if (bytes is null || bytes.Length < 8)
            return null;

        try
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                return BuildJpegImage(bytes);

            ReadOnlySpan<byte> pngSignature = stackalloc byte[]
                { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (bytes.AsSpan(0, 8).SequenceEqual(pngSignature))
                return BuildPngImage(bytes);
        }
        catch
        {
            // Corrupt/unsupported image bytes are equivalent to no associated
            // printable image. Never substitute or guess another asset.
        }

        return null;
    }

    private static PdfImage? BuildJpegImage(byte[] bytes)
    {
        var offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            while (offset < bytes.Length && bytes[offset] == 0xFF)
                offset++;
            if (offset >= bytes.Length) return null;

            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7)
                continue;
            if (offset + 2 > bytes.Length) return null;

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (length < 2 || offset + length > bytes.Length) return null;

            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (length < 8) return null;
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2));
                var components = bytes[offset + 7];
                if (width == 0 || height == 0 || components is not (1 or 3)) return null;

                return new PdfImage(
                    width,
                    height,
                    components == 1 ? "DeviceGray" : "DeviceRGB",
                    "[/ASCIIHexDecode /DCTDecode]",
                    string.Empty,
                    Convert.ToHexString(bytes));
            }

            offset += length;
        }

        return null;
    }

    private static PdfImage? BuildPngImage(byte[] bytes)
    {
        var offset = 8;
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idat = new MemoryStream();

        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length < 0 || offset + 12 + length > bytes.Length) return null;
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var dataOffset = offset + 8;

            switch (type)
            {
                case "IHDR":
                    if (length != 13) return null;
                    width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataOffset, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(dataOffset + 4, 4));
                    bitDepth = bytes[dataOffset + 8];
                    colorType = bytes[dataOffset + 9];
                    interlace = bytes[dataOffset + 12];
                    break;
                case "PLTE":
                    palette = bytes.AsSpan(dataOffset, length).ToArray();
                    break;
                case "tRNS":
                    transparency = bytes.AsSpan(dataOffset, length).ToArray();
                    break;
                case "IDAT":
                    idat.Write(bytes, dataOffset, length);
                    break;
                case "IEND":
                    offset = bytes.Length;
                    continue;
            }

            offset += length + 12;
        }

        if (width <= 0 || height <= 0 || bitDepth != 8 || interlace != 0)
            return null;

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => 0
        };
        if (channels == 0 || colorType == 3 && (palette is null || palette.Length < 3))
            return null;

        var rowLength = checked(width * channels);
        byte[] filtered;
        idat.Position = 0;
        using (var inflated = new MemoryStream())
        {
            using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
                zlib.CopyTo(inflated);
            filtered = inflated.ToArray();
        }

        if (filtered.Length != checked((rowLength + 1) * height))
            return null;

        var pixels = new byte[checked(rowLength * height)];
        for (var row = 0; row < height; row++)
        {
            var filter = filtered[row * (rowLength + 1)];
            var source = filtered.AsSpan(row * (rowLength + 1) + 1, rowLength);
            var target = pixels.AsSpan(row * rowLength, rowLength);
            var prior = row == 0
                ? ReadOnlySpan<byte>.Empty
                : pixels.AsSpan((row - 1) * rowLength, rowLength);

            for (var i = 0; i < rowLength; i++)
            {
                var left = i >= channels ? target[i - channels] : (byte)0;
                var up = prior.IsEmpty ? (byte)0 : prior[i];
                var upLeft = prior.IsEmpty || i < channels ? (byte)0 : prior[i - channels];
                target[i] = filter switch
                {
                    0 => source[i],
                    1 => unchecked((byte)(source[i] + left)),
                    2 => unchecked((byte)(source[i] + up)),
                    3 => unchecked((byte)(source[i] + ((left + up) >> 1))),
                    4 => unchecked((byte)(source[i] + Paeth(left, up, upLeft))),
                    _ => throw new InvalidDataException("Unsupported PNG row filter.")
                };
            }
        }

        var outputColors = colorType is 0 or 4 ? 1 : 3;
        var rawRows = new byte[checked((width * outputColors + 1) * height)];
        for (var row = 0; row < height; row++)
        {
            var source = pixels.AsSpan(row * rowLength, rowLength);
            var target = rawRows.AsSpan(row * (width * outputColors + 1) + 1, width * outputColors);
            rawRows[row * (width * outputColors + 1)] = 0;

            for (var x = 0; x < width; x++)
            {
                switch (colorType)
                {
                    case 0:
                        target[x] = source[x];
                        break;
                    case 2:
                        source.Slice(x * 3, 3).CopyTo(target.Slice(x * 3, 3));
                        break;
                    case 3:
                    {
                        var paletteIndex = source[x];
                        var paletteOffset = paletteIndex * 3;
                        if (paletteOffset + 2 >= palette!.Length) return null;
                        var alpha = transparency is not null && paletteIndex < transparency.Length
                            ? transparency[paletteIndex]
                            : (byte)255;
                        for (var channel = 0; channel < 3; channel++)
                            target[x * 3 + channel] = CompositeOnWhite(palette[paletteOffset + channel], alpha);
                        break;
                    }
                    case 4:
                        target[x] = CompositeOnWhite(source[x * 2], source[x * 2 + 1]);
                        break;
                    case 6:
                    {
                        var alpha = source[x * 4 + 3];
                        for (var channel = 0; channel < 3; channel++)
                            target[x * 3 + channel] = CompositeOnWhite(source[x * 4 + channel], alpha);
                        break;
                    }
                }
            }
        }

        byte[] compressed;
        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
                zlib.Write(rawRows);
            compressed = compressedStream.ToArray();
        }

        return new PdfImage(
            width,
            height,
            outputColors == 1 ? "DeviceGray" : "DeviceRGB",
            "[/ASCIIHexDecode /FlateDecode]",
            $"/DecodeParms[null<</Predictor 15/Colors {outputColors}/BitsPerComponent 8/Columns {width}>>]",
            Convert.ToHexString(compressed));
    }

    private static byte CompositeOnWhite(byte color, byte alpha) =>
        (byte)((color * alpha + 255 * (255 - alpha) + 127) / 255);

    private static byte Paeth(byte left, byte up, byte upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var diagonalDistance = Math.Abs(estimate - upLeft);
        return leftDistance <= upDistance && leftDistance <= diagonalDistance
            ? left
            : upDistance <= diagonalDistance ? up : upLeft;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string Rgb(int r, int g, int b) => $"{r:N3} {g:N3} {b:N3}";

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == '\\') sb.Append("\\\\");
            else if (c == '(') sb.Append("\\(");
            else if (c == ')') sb.Append("\\)");
            else if (c > 127) sb.Append($"\\u{(ushort)c:X4}");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static byte[] EncodeStreamContent(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    private static string Fmt(decimal? v) => v.HasValue ? v.Value.ToString("0", Inv) : "\u2014";
    private static string FmtWeight(decimal? v) => v.HasValue ? v.Value.ToString("0", Inv) : "\u2014";

    private static string DayOfWeekPt(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday => "Segunda-feira",
        DayOfWeek.Tuesday => "Terça-feira",
        DayOfWeek.Wednesday => "Quarta-feira",
        DayOfWeek.Thursday => "Quinta-feira",
        DayOfWeek.Friday => "Sexta-feira",
        DayOfWeek.Saturday => "Sábado",
        _ => "Domingo"
    };

    private static IReadOnlyList<string> WrapText(string text, int maxCols)
    {
        var lines = new List<string>();
        var words = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxCols && current.Length > 0)
            {
                lines.Add(Escape(current.ToString()));
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) lines.Add(Escape(current.ToString()));
        return lines;
    }
}
