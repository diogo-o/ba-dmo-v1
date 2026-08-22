using System.Globalization;
using System.Text;
using BA.Dmo.Application.Modules.Peso;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Deterministic PDF renderer for the Peso folha de produção (GLM-PESO-09, 06_DATA §16).
/// Generates a valid single-page A4 PDF from the APPROVED snapshot only.
/// Historical integrity: later NominalWeight changes must NOT alter an already generated document.
/// Colours follow DMO design tokens (dmo-tokens.css). Text escapes non-ASCII via \uXXXX for Helvetica.
/// </summary>
public sealed class PesoSingleFilePdfRenderer : IPdfRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---- A4 geometry (72 DPI points) ---------------------------------------
    private const int PageW = 595, PageH = 842;
    private const int MgnL = 38, MgnR = 560;
    private const int PageTop = 780, PageBot = 30;

    // ---- DMO colour tokens (RGB from dmo-tokens.css) -----------------------
    private static readonly (int R, int G, int B) Blue = (49, 93, 136);    // --dmo-brand-700
    private static readonly (int R, int G, int B) Dark = (15, 29, 42);      // --dmo-brand-950
    private static readonly (int R, int G, int B) Light = (189, 211, 232);  // --dmo-brand-200
    private static readonly (int R, int G, int B) Paler = (232, 239, 247);  // --dmo-brand-050
    private static readonly (int R, int G, int B) Green = (82, 124, 114);   // success/approved
    private static readonly (int R, int G, int B) Muted = (100, 119, 138);  // muted text

    /// <summary>Escapes string for PDF Tj operator + Unicode \uXXXX for non-ASCII.</summary>
    private static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in s)
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(':  sb.Append("\\("); break;
                case ')':  sb.Append("\\)"); break;
                default:
                    if (c > 127) sb.Append($"\\u{(ushort)c:X4}");
                    else sb.Append(c);
                    break;
            }
        return sb.ToString();
    }

    public byte[] RenderPesoFolha(PesoFolhaPdf d)
    {
        var st = new StringBuilder(20000);
        var y = PageTop;

        // ====================================================================
        // HEADER BAR — blue background + white title
        // ====================================================================
        Rect(st, Blue.R, Blue.G, Blue.B, MgnL, y - 16, MgnR - MgnL, 18);
        Txt(st, 14, true, 255, 255, 255, "Controlo de Peso e Volume", MgnL + 4, y - 4);
        y -= 26;

        // Subtitle
        Txt(st, 8, false, Dark.R, Dark.G, Dark.B,
            $"Documento final para Produ\u00E7\u00E3o \u00B7 Refer\u00EAncia {d.MoldNumber}{d.NeckringNumber} \u00B7 Linha {d.Line} \u00B7 Lote L{d.Lote}",
            MgnL + 4, y);
        y -= 12;

        // Approval badge (top-right green pill)
        var bx = MgnR - 115;
        var by = PageTop - 16;
        Rect(st, Green.R, Green.G, Green.B, bx, by, 110, 16);
        Txt(st, 9, true, 255, 255, 255, "APROVADO", bx + 6, by + 3);
        if (d.ApprovedAtUtc is { } dt)
            Txt(st, 7, false, 255, 255, 255, dt.ToString("yyyy-MM-dd"), bx + 40, by + 3);
        y -= 16;

        HLine(st, y--);

        // ====================================================================
        // INFO CHIPS ROW — Reference | CM | Boquilha | Linha | Lote
        // ====================================================================
        var chipW = (MgnR - MgnL) / 5;
        var chips = new[] {
            ("Refer\u00EAncia", $"{d.MoldNumber}{d.NeckringNumber}"),
            ("Contra molde", d.MoldNumber),
            ("Boquiha / Neckring", d.NeckringNumber),
            ("Linha", d.Line),
            ("Lote", $"L{d.Lote}")
        };
        var cx = MgnL + 6;
        foreach (var (lb, val) in chips)
        {
            Rect(st, Paler.R, Paler.G, Paler.B, cx - 4, y - 12, chipW - 8, 14);
            HLine(st, y, Blue.R, Blue.G, Blue.B);
            Txt(st, 7, false, Muted.R, Muted.G, Muted.B, lb, cx, y - 1);
            Txt(st, 9, true, Dark.R, Dark.G, Dark.B, val, cx, y - 9);
            cx += chipW;
        }
        y -= 18;

        // ====================================================================
        // SECTION: IDENTIFICAÇÃO DA PRODUÇÃO
        // ====================================================================
        SecHeader(st, ref y, "IDENTIFICAÇÃO DA PRODUÇÃO");

        // Two-column table
        IdRow(st, ref y, "Produ\u00E7\u00E3o", d.ProductionCode, "Linha", d.Line, "Lote", $"L{d.Lote}", false);
        IdRow(st, ref y, "Estado do molde", d.EstadoMolde ?? "\u2014", "Tipo", d.Processo ?? "\u2014", "Data",
             d.ApprovedAtUtc?.ToString("dd/MM/yyyy") ?? "\u2014", true);
        y -= 6;

        // ====================================================================
        // SECTION: COMPARAÇÃO COM A ÚLTIMA PRODUÇÃO
        // ====================================================================
        SecHeader(st, ref y, "COMPARAÇÃO COM A ÚLTIMA PRODUÇÃO");

        CompHeaderRow(st, ref y);
        CompDataRow(st, ref y, "Peso calculado", Fmt(d.PesoMedio), Fmt(d.PreviousPesoMedio), FmtDelta(d.DeltaPeso), FmtPct(d.DeltaPesoPct), false);
        CompDataRow(st, ref y, "Capacidade m\u00E9dia", Fmt(d.CapacidadeMedia), Fmt(d.PreviousCapacidadeMedia), FmtDelta(d.DeltaCapacidade), FmtPct(d.DeltaCapacidadePct), true);

        y -= 4;
        if (!string.IsNullOrEmpty(d.PreviousProductionCode))
        {
            RowSep(st, ref y);
            MutedTxt(st, 8, "\u00DAltima produ\u00E7\u00E3o usada:", MgnL + 4, y);
            y -= 12;
            Txt(st, 9, false, Dark.R, Dark.G, Dark.B, d.PreviousProductionCode, MgnL + 4, y);
            y -= 16;
        }
        y -= 6;

        // ====================================================================
        // SECTION: COMPARAÇÃO POR CONTRA MOLDE (SINGLE COMBINED TABLE)
        // ====================================================================
        SecHeader(st, ref y, "COMPARAÇÃO POR CONTRA MOLDE");

        CmTableHeader(st, ref y);

        if (d.CmRows?.Count > 0)
        {
            for (var i = 0; i < d.CmRows.Count; i++)
            {
                var r = d.CmRows[i];
                CmTableRow(st, ref y, r.CmNumber,
                    Fmt(r.PesoAtual), Fmt(r.PesoAnterior), FmtDelta(r.DeltaPeso),
                    Fmt(r.CapacidadeAtual), Fmt(r.CapacidadeAnterior), FmtDelta(r.DeltaCapacidade),
                    i % 2 == 1);
            }
            // Capacity summary row
            y += 2;
            RowSep(st, ref y);
            y += 12;
            Txt(st, 8, true, Dark.R, Dark.G, Dark.B, "Capacidade média", MgnL + 4, y);
            Txt(st, 8, true, Dark.R, Dark.G, Dark.B, Fmt(d.CapacidadeMedia), 320, y);
            Txt(st, 8, true, Dark.R, Dark.G, Dark.B, Fmt(d.PreviousCapacidadeMedia), 400, y);
            Txt(st, 8, true, Dark.R, Dark.G, Dark.B, FmtDelta(d.DeltaCapacidade), 470, y);
            y += 16;
        }
        else
        {
            MutedTxt(st, 9, "\u2014 Sem leituras por CM registadas", MgnL + 4, y);
            y -= 14;
        }
        y -= 6;

        // ====================================================================
        // SECTION: REFERÊNCIAS
        // ====================================================================
        SecHeader(st, ref y, "REFERÊNCIAS");

        RefRow(st, ref y, "Peso nominal do desenho", Fmt(d.PesoNominal), "Diferença para novo", FmtDelta(d.DeltaNominal), "Variação", FmtPct(d.DeltaNominalPct));
        RefRow(st, ref y, "Peso médio SAP produção anterior", Fmt(d.SapPesoMedio), "Período SAP", d.SapPeriodo ?? "\u2014", "", "", alt: true);
        RefRow(st, ref y, "Temperatura", TempC(d.TemperaturaC), "Densidade", Density(d.Densidade), "", "");
        y -= 6;

        // ====================================================================
        // SECTION: RASTREABILIDADE
        // ====================================================================
        SecHeader(st, ref y, "RASTREABILIDADE");

        TraceRow(st, ref y, "Verificado por", d.ApprovedBy ?? "\u2014", "Aprovado por", d.ApprovedBy ?? "\u2014");
        TraceRow(st, ref y, "Data da aprovação", d.ApprovedAtUtc?.ToString("dd/MM/yyyy, HH:mm") ?? "\u2014", "Revisão", d.Revision.ToString(), alt: true);
        y -= 8;

        // ====================================================================
        // FOOTER
        // ====================================================================
        var fy = Math.Max(PageBot, y - 20);
        MutedTxt(st, 7, $"Gerado em {DateTime.UtcNow:dd/MM/yyyy, HH:mm:ss}", MgnL + 4, fy);

        // ====================================================================
        // ASSEMBLE PDF BYTES
        // ====================================================================
        return AssemblePdf(st.ToString());
    }

    /* ======================================================================
       PDF GRAPHICS HELPERS
       ====================================================================== */

    private static void Rect(StringBuilder s, int r, int g, int b, int x, int yy, int w, int h) =>
        s.AppendLine($"{r:N3} {g:N3} {b:N3} rg {x} {yy} {w} {h} re f");

    private static void HLine(StringBuilder s, int yy, int r = 190, int g = 200, int b = 210) =>
        s.AppendLine($"{r:N3} {g:N3} {b:N3} RG 0.5 w {MgnL} {yy} m {MgnR} {yy} l S");

    private static void VLine(StringBuilder s, int xx, int yy, int h, int r = 190, int g = 200, int b = 210) =>
        s.AppendLine($"{r:N3} {g:N3} {b:N3} RG 0.5 w {xx} {yy} m {xx} {yy - h} l S");

    private static void Txt(StringBuilder s, int sz, bool bold, int r, int g, int b, string text, int xx, int yy)
    {
        s.Append($"{r:N3} {g:N3} {b:N3} rg ");
        s.AppendLine($"BT /F1 {sz} Tf {(bold ? "7" : "0")} Tf {xx} {yy} Td ({Esc(text)}) Tj ET");
    }

    /* ======================================================================
       LAYOUT HELPERS
       ====================================================================== */

    private static void SecHeader(StringBuilder s, ref int y, string title)
    {
        y -= 4;
        Rect(s, Blue.R, Blue.G, Blue.B, MgnL, y - 14, MgnR - MgnL, 16);
        Txt(s, 9, true, 255, 255, 255, title, MgnL + 4, y - 3);
        y -= 18;
        HLine(s, y--);
    }

    private static void RowSep(StringBuilder s, ref int y)
    {
        HLine(s, y--);
    }

    private static void MutedTxt(StringBuilder s, int sz, string text, int xx, int yy) =>
        Txt(s, sz, false, Muted.R, Muted.G, Muted.B, text, xx, yy);

    /* ======================================================================
       TABLE ROW HELPERS
       ====================================================================== */

    /// <summary>Identification section row: 3 label/value pairs.</summary>
    private static void IdRow(StringBuilder s, ref int y, string a, string vA, string b, string vB, string c, string vC, bool alt)
    {
        if (alt) Rect(s, Paler.R, Paler.G, Paler.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, a, MgnL + 4, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vA, MgnL + 120, y - 1);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, b, MgnL + 220, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vB, MgnL + 310, y - 1);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, c, MgnL + 400, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vC, MgnL + 460, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /// <summary>Comparison section header row.</summary>
    private static void CompHeaderRow(StringBuilder s, ref int y)
    {
        Rect(s, Blue.R, Blue.G, Blue.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 8, true, 255, 255, 255, "Parâmetro", MgnL + 4, y - 1);
        Txt(s, 8, true, 255, 255, 255, "Produção atual", MgnL + 150, y - 1);
        Txt(s, 8, true, 255, 255, 255, "\u00DAltima produção", MgnL + 270, y - 1);
        Txt(s, 8, true, 255, 255, 255, "Diferença", MgnL + 390, y - 1);
        Txt(s, 8, true, 255, 255, 255, "Variação", MgnL + 470, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /// <summary>Comparison data row.</summary>
    private static void CompDataRow(StringBuilder s, ref int y, string label, string cur, string prev, string diff, string pct, bool alt)
    {
        if (alt) Rect(s, Paler.R, Paler.G, Paler.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, label, MgnL + 4, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, cur, MgnL + 150, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, prev, MgnL + 270, y - 1);
        Txt(s, 9, true, Dark.R, Dark.G, Dark.B, diff, MgnL + 390, y - 1);
        Txt(s, 9, true, Dark.R, Dark.G, Dark.B, pct, MgnL + 470, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /// <summary>CM comparison combined table header.</summary>
    private static void CmTableHeader(StringBuilder s, ref int y)
    {
        Rect(s, Blue.R, Blue.G, Blue.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 7, true, 255, 255, 255, "Contra Molde", MgnL + 4, y - 1);
        Txt(s, 7, true, 255, 255, 255, "Peso atual (g)", MgnL + 130, y - 1);
        Txt(s, 7, true, 255, 255, 255, "Peso ant. (g)", MgnL + 210, y - 1);
        Txt(s, 7, true, 255, 255, 255, "Δ (g)", MgnL + 285, y - 1);
        Txt(s, 7, true, 255, 255, 255, "Cap. atual (cm³)", MgnL + 330, y - 1);
        Txt(s, 7, true, 255, 255, 255, "Cap. ant. (cm³)", MgnL + 410, y - 1);
        Txt(s, 7, true, 255, 255, 255, "Δ (cm³)", MgnL + 480, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /// <summary>One CM data row.</summary>
    private static void CmTableRow(StringBuilder s, ref int y, string cm, string pCur, string pPrev, string pDiff,
        string cCur, string cPrev, string cDiff, bool alt)
    {
        if (alt) Rect(s, Paler.R, Paler.G, Paler.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 8, false, Dark.R, Dark.G, Dark.B, cm, MgnL + 4, y - 1);
        Txt(s, 8, false, Dark.R, Dark.G, Dark.B, pCur, MgnL + 130, y - 1);
        Txt(s, 8, false, Dark.R, Dark.G, Dark.B, pPrev, MgnL + 210, y - 1);
        Txt(s, 8, true, Dark.R, Dark.G, Dark.B, pDiff, MgnL + 285, y - 1);
        Txt(s, 8, false, Dark.R, Dark.G, Dark.B, cCur, MgnL + 330, y - 1);
        Txt(s, 8, false, Dark.R, Dark.G, Dark.B, cPrev, MgnL + 410, y - 1);
        Txt(s, 8, true, Dark.R, Dark.G, Dark.B, cDiff, MgnL + 480, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /// <summary>References section row (6 columns).</summary>
    private static void RefRow(StringBuilder s, ref int y, string a, string vA, string b, string vB, string c, string vC, bool alt = false)
    {
        if (alt) Rect(s, Paler.R, Paler.G, Paler.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, a, MgnL + 4, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vA, MgnL + 160, y - 1);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, b, MgnL + 280, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vB, MgnL + 390, y - 1);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, c, MgnL + 470, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vC, MgnL + 520, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /// <summary>Traceability section row (4 columns).</summary>
    private static void TraceRow(StringBuilder s, ref int y, string a, string vA, string b, string vB, bool alt = false)
    {
        if (alt) Rect(s, Paler.R, Paler.G, Paler.B, MgnL, y - 12, MgnR - MgnL, 14);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, a, MgnL + 4, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vA, MgnL + 140, y - 1);
        Txt(s, 8, false, Muted.R, Muted.G, Muted.B, b, MgnL + 290, y - 1);
        Txt(s, 9, false, Dark.R, Dark.G, Dark.B, vB, MgnL + 400, y - 1);
        y -= 14;
        RowSep(s, ref y);
    }

    /* ======================================================================
       FORMATTING
       ====================================================================== */

    private static string Fmt(decimal? v) => v.HasValue ? v.Value.ToString("0.##", Inv) : "\u2014";
    private static string FmtDelta(decimal? v)
    {
        if (!v.HasValue) return "\u2014";
        return (v.Value >= 0 ? "+" : "") + Math.Abs(v.Value).ToString("0.##", Inv);
    }
    private static string FmtPct(decimal? v)
    {
        if (!v.HasValue) return "\u2014";
        return (v.Value >= 0 ? "+" : "") + Math.Abs(v.Value).ToString("0.##", Inv) + "%";
    }
    private static string TempC(decimal? v) => v.HasValue ? $"{v.Value:0}\u00B0C" : "\u2014";
    private static string Density(decimal? v) => v.HasValue ? $"{v.Value:0.#####} g/cm\u00B3" : "\u2014";

    /* ======================================================================
       PDF ASSEMBLY
       ====================================================================== */

    private static byte[] AssemblePdf(string streamContent)
    {
        var sb = new StringBuilder();
        var bytes = Encoding.UTF8.GetBytes(streamContent);

        sb.AppendLine("%PDF-1.4");
        sb.AppendLine("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj");
        sb.AppendLine("2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj");
        sb.AppendLine("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]" +
                      "/Resources<</Font<</F1 4 0 R>>>>>>Contents 5 0 R>>endobj");
        sb.AppendLine("4 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj");
        sb.AppendLine($"5 0 obj<</Length {bytes.Length}>>stream\n");
        sb.Append(streamContent);
        sb.AppendLine("\nendstream endobj");

        var xrefOff = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.AppendLine("xref");
        sb.AppendLine("0 6");
        sb.AppendLine("0000000000 65535 f ");
        sb.AppendLine("0000000009 00000 n ");
        sb.AppendLine("0000000058 00000 n ");
        sb.AppendLine("0000000115 00000 n ");
        sb.AppendLine("0000000310 00000 n ");
        sb.AppendLine($"{(6 + xrefOff):D10} 00000 n ");
        sb.AppendLine("trailer<</Size 6/Root 1 0 R>>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefOff.ToString());
        sb.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
