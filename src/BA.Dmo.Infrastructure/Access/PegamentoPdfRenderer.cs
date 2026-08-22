using System.Text;
using System.Globalization;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Deterministic PDF renderer for the Pegamentos control sheet (GLM-PEG-14,
/// 06_DATA §16). Generates a valid single-page PDF from the frozen Pegamento
/// snapshot ONLY — never from current Job On / tool / nominal / settings state.
/// Historical integrity: a later Job On revision must NOT alter an already
/// generated Pegamentos document.
/// </summary>
public sealed class PegamentoPdfRenderer : IPegamentoPdfRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public byte[] RenderPegamento(PegamentoPdfData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("%PDF-1.4");
        sb.AppendLine("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj");
        sb.AppendLine("2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj");
        sb.AppendLine("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]"
            + "/Resources<</Font<</F1 4 0 R>> >>/Contents 5 0 R>>endobj");
        sb.AppendLine("4 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj");

        const int top = 820;
        var text = new StringBuilder();
        int y = top;

        void Line(string s, int dy = 16, int size = 10)
        {
            text.AppendLine($"BT /F1 {size} Tf 40 {y} Td ({Escape(s)}) Tj ET");
            y -= dy;
        }

        void Pair(string label, string value, int dy = 14)
        {
            Line($"{label}: {value}", dy);
        }

        // ===== HEADER =====
        Line($"Pegamentos — {data.Reference}", 20, 14);
        Line($"Produção {data.ProductionCode} · Máquina {data.MachineCode} · Revisão {data.JobOnRevisionId.ToString().Substring(0, 8)}", 16, 10);

        // ===== COMPONENT SUMMARY BLOCKS =====
        if (y < 700) { }
        y -= 8;

        // CM
        Line("CM — Contra-molde", 16, 11);
        Pair("Referência", data.CmReference);
        Pair("Lote", data.CmLot ?? "—");
        Pair("Nominal", Nom(data.CmNominal));
        Pair("Média medida", AvgFor(data, "CM"));
        Pair("Corredor", Corridor(data.CmNominal, data.Tolerance));
        y -= 6;

        // BQ
        Line("BQ — Boquilha", 16, 11);
        Pair("Referência", data.BqReference);
        Pair("Lote", data.BqLot ?? "—");
        Pair("Nominal", Nom(data.BqNominal));
        Pair("Média medida", AvgFor(data, "BQ"));
        Pair("Corredor", Corridor(data.BqNominal, data.Tolerance));
        y -= 6;

        // MF
        Line("MF — Molde final", 16, 11);
        Pair("Referência", data.MfReference);
        Pair("Lote", data.MfLot ?? "—");
        Pair("Nominal", Nom(data.MfNominal));
        Pair("Média medida", AvgFor(data, "MF"));
        Pair("Corredor", Corridor(data.MfNominal, data.Tolerance));
        y -= 8;

        // ===== STATUS MESSAGE =====
        Line(StatusMessage(data), 16, 10);
        y -= 4;

        // ===== PER-COMPONENT MEASUREMENT TABLES =====
        foreach (var component in new[] { "CM", "BQ", "MF" })
        {
            if (y < 120)
            {
                y = top;
            }

            y -= 8;
            var nominal = component switch
            {
                "CM" => data.CmNominal,
                "BQ" => data.BqNominal,
                "MF" => data.MfNominal,
                _ => null
            };
            var reference = component switch
            {
                "CM" => data.CmReference,
                "BQ" => data.BqReference,
                "MF" => data.MfReference,
                _ => ""
            };
            var lot = component switch
            {
                "CM" => data.CmLot,
                "BQ" => data.BqLot,
                "MF" => data.MfLot,
                _ => null
            };

            Line($"{component} — {reference} · Lote {lot ?? "—"} · Nominal {Nom(nominal)} · Corredor {Corridor(nominal, data.Tolerance)}", 16, 11);
            Line("N.º | COSTURA | CONTRA COSTURA | OVALIZAÇÃO | MÉDIA", 14, 9);

            var rows = data.Measurements.Where(m => m.ComponentKey == component).ToList();
            if (rows.Count == 0)
            {
                Line("Sem medições registadas.", 14, 9);
            }
            else
            {
                foreach (var m in rows)
                {
                    Line($"{m.ToolNumber?.ToString() ?? "—"} | {Fmt(m.Costura)} | {Fmt(m.ContraCostura)} | {Fmt(m.Ovalizacao)} | {Fmt(m.Media)}", 13, 9);
                }
                Line($"AVG | — | — | — | {AvgFor(data, component)}", 13, 10);
            }
        }

        // ===== FOOTER =====
        y -= 10;
        Line($"Gerado em {data.GeneratedAtUtc:yyyy-MM-dd HH:mm}", 14, 9);

        var content = text.ToString();
        sb.AppendLine($"5 0 obj<</Length {content.Length}>>stream");
        sb.AppendLine(content);
        sb.AppendLine("endstream endobj");

        var xrefOffset = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.AppendLine("xref");
        sb.AppendLine("0 6");
        sb.AppendLine("0000000000 65535 f ");
        sb.AppendLine("0000000009 00000 n ");
        sb.AppendLine("0000000058 00000 n ");
        sb.AppendLine("0000000115 00000 n ");
        sb.AppendLine("0000000310 00000 n ");
        sb.AppendLine($"{(6 + xrefOffset):D10} 00000 n ");
        sb.AppendLine("trailer<</Size 6/Root 1 0 R>>");
        sb.AppendLine("startxref");
        sb.AppendLine(xrefOffset.ToString());
        sb.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string AvgFor(PegamentoPdfData data, string component)
    {
        var rows = data.Measurements.Where(m => m.ComponentKey == component && m.Media.HasValue).ToList();
        if (rows.Count == 0) return "—";
        return Fmt(rows.Average(r => r.Media!.Value));
    }

    private static string StatusMessage(PegamentoPdfData data)
    {
        bool AllWithinCorridor()
        {
            foreach (var m in data.Measurements)
            {
                var nominal = m.ComponentKey switch
                {
                    "CM" => data.CmNominal,
                    "BQ" => data.BqNominal,
                    "MF" => data.MfNominal,
                    _ => null
                };
                if (!nominal.HasValue || !m.Media.HasValue) return false;
                if (PegamentoMeasurementCalculator.CheckTolerance(m.Media.Value, nominal.Value, data.Tolerance)
                    != PegamentoToleranceStatus.Ok)
                {
                    return false;
                }
            }
            return true;
        }

        return AllWithinCorridor()
            ? "✓ Todas as medições permanecem dentro do corredor do respetivo componente."
            : "Atenção: existem medições fora do corredor do respetivo componente.";
    }

    private static string Corridor(decimal? nominal, decimal tolerance)
        => nominal.HasValue ? $"{Fmt(nominal.Value - tolerance)}–{Fmt(nominal.Value + tolerance)}" : "—";

    private static string Nom(decimal? v) => v.HasValue ? Fmt(v.Value) + " mm" : "—";

    private static string Fmt(decimal? v) => v.HasValue ? v.Value.ToString("0.##", Inv) : "—";

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '(' : sb.Append("\\("); break;
                case ')' : sb.Append("\\)"); break;
                case '\r': break;
                case '\n': sb.Append(' '); break;
                case 'Ç': case 'ç': sb.Append('C'); break;
                case '—': case '–': case 'ã': case 'â': case 'à': case 'á': case 'ä': sb.Append('a'); break;
                case 'ó': case 'ô': case 'õ': case 'ò': case 'ö': sb.Append('o'); break;
                case 'ê': case 'é': case 'è': case 'ë': case 'Ê': case 'É': sb.Append('e'); break;
                case 'í': case 'ì': case 'î': sb.Append('i'); break;
                case 'ú': case 'ù': case 'û': sb.Append('u'); break;
                case '\u2019': case '\u2018': sb.Append('\''); break;
                default:
                    if (c <= 127) sb.Append(c);
                    else sb.Append(StringNormalizeFallback(c));
                    break;
            }
        }
        return sb.ToString();
    }

    private static char StringNormalizeFallback(char c)
    {
        // Fallback: strip combining marks from accented letters; else '?'.
        var s = c.ToString().Normalize(System.Text.NormalizationForm.FormD);
        if (s.Length > 0 && char.IsLetter(s[0]))
            return char.ToLowerInvariant(s[0]);
        return '?';
    }
}