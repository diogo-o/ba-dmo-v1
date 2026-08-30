using System.Globalization;
using BA.Dmo.Application.Modules.JobOn;

namespace BA.Dmo.Web.Pages.JobOn;

/// <summary>
/// R011 / JOB ON PLANNING ISOLATION (Phase 4) — the ONE planning projection
/// shared by the Job On landing (<c>IndexModel.OnGetAsync</c>) and the
/// planning-only read endpoint (<c>GET /api/jobon/planning</c>).
///
/// Semantics are preserved exactly as the landing originally built them:
/// the whole displayed month is read through
/// <c>IJobOnRepository.GetHistoricalProductionsAsync</c> (the existing
/// planning source — no current-production / rail reader), the calendar
/// markers are the stable planned_start_at dates with the deterministic
/// <see cref="JobOnLineColor"/> keys (distinct lines on the same day are all
/// represented, never hidden), and the list is the selected day filtered
/// from the SAME month projection (calendar and list never diverge).
///
/// PLANNING ISOLATION RULE: this type is planning-only. It must never depend
/// on ICurrentProductionContextLookup, the production rail projection or any
/// current-production reader — planning date state and current production
/// context remain separate.
/// </summary>
public static class JobOnPlanningProjection
{
    private static readonly CultureInfo PtPt = new("pt-PT");

    /// <summary>
    /// Everything the planning area needs for a (selected date, marker month)
    /// pair: the date display, the month the calendar renders, the calendar
    /// marker data (record dates + line-color markers) and the day list items.
    /// </summary>
    public sealed record Result(
        string SelectedDateValue,
        string SelectedDateDisplay,
        string Month,
        string RecordDatesCsv,
        IReadOnlyDictionary<string, IReadOnlyList<string>> RecordLines,
        IReadOnlyList<PlaneamentoItem> Items);

    /// <summary>
    /// Selected-date resolution — the exact landing semantics: strict
    /// <c>yyyy-MM-dd</c>; missing/invalid falls back to today.
    /// </summary>
    public static DateTime ResolveSelectedDate(string? date) =>
        DateTime.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : DateTime.Today;

    /// <summary>
    /// Marker-month resolution (calendar navigation): strict <c>yyyy-MM</c>;
    /// missing/invalid falls back to the selected date's month (same
    /// fail-safe style as the page, no new error surface).
    /// </summary>
    public static DateTime ResolveMarkerMonth(string? month, DateTime selectedDate)
    {
        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return new DateTime(selectedDate.Year, selectedDate.Month, 1);
    }

    /// <summary>
    /// The month read range passed to the planning reader (month start →
    /// first day of the next month — the existing OnGet boundary, unchanged).
    /// </summary>
    public static (DateTime From, DateTime To) MonthRange(DateTime date)
    {
        var monthStart = new DateTime(date.Year, date.Month, 1);
        return (monthStart, monthStart.AddMonths(1));
    }

    /// <summary>Canonical <c>YYYY-MM</c> for a date (calendar month token).</summary>
    public static string FormatMonth(DateTime date) =>
        date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Selected-day heading, e.g. "30 de agosto" (landing display, unchanged).</summary>
    public static string FormatDateDisplay(DateTime date) =>
        $"{date.Day} de {date.ToString("MMMM", PtPt).ToLower(PtPt)}";

    /// <summary>
    /// Stable record dates for the canonical calendar (planned_start_at only),
    /// sorted + distinct — the generic marker source for the whole month.
    /// </summary>
    public static string BuildRecordDatesCsv(IReadOnlyList<HistoricalProductionSummary> monthSummaries) =>
        string.Join(",",
            monthSummaries
                .Select(s => s.PlannedStartAt?.ToString("yyyy-MM-dd"))
                .Where(d => d is not null)
                .OrderBy(d => d)
                .Distinct());

    /// <summary>
    /// R011 line-color markers: date → distinct line color keys, so multiple
    /// productions/lines on the same day are all represented (never silently
    /// hidden). Deterministic <see cref="JobOnLineColor"/> mapping; keys and
    /// dates ordered for a stable output.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<string>> BuildRecordLines(IReadOnlyList<HistoricalProductionSummary> monthSummaries)
    {
        var markers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var s in monthSummaries)
        {
            if (s.PlannedStartAt is not { } start)
                continue;
            var day = start.ToString("yyyy-MM-dd");
            var key = JobOnLineColor.GetColorKey(s.MachineCode);
            if (key is null)
                continue;
            if (!markers.TryGetValue(day, out var keys))
            {
                keys = new List<string>();
                markers[day] = keys;
            }
            if (!keys.Contains(key))
                keys.Add(key);
        }

        return markers
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// The list items of the SELECTED day, projected from the same month
    /// summaries the calendar markers use (one source, no divergence).
    /// </summary>
    public static IReadOnlyList<PlaneamentoItem> BuildDayItems(
        IReadOnlyList<HistoricalProductionSummary> monthSummaries, DateTime selectedDate) =>
        monthSummaries
            .Where(s => s.PlannedStartAt?.Date == selectedDate.Date)
            .Select(s => new PlaneamentoItem(
                JobOnId: s.JobOnId,
                Date: s.PlannedStartAt?.ToString("dd/MM/yyyy") ?? "—",
                DateIso: s.PlannedStartAt?.ToString("yyyy-MM-dd") ?? "",
                DayLabel: s.PlannedStartAt?.ToString("dd MMM", PtPt).ToUpper(PtPt) ?? "—",
                TimeRange: $"{s.PlannedStartAt?.ToString("HH:mm") ?? "—"}–{s.PlannedEndAt?.ToString("HH:mm") ?? "—"}",
                Reference: s.ReferenceCode ?? "—",
                Production: s.ProductionCode,
                Machine: s.MachineCode,
                RevisionNumber: s.CurrentRevisionNumber,
                LineColorKey: JobOnLineColor.GetColorKey(s.MachineCode),
                LifecycleDisplay: s.LifecycleState switch
                {
                    Domain.Modules.JobOn.JobOnLifecycleState.Rascunho => "Rascunho",
                    Domain.Modules.JobOn.JobOnLifecycleState.Planeado => "Planeado",
                    Domain.Modules.JobOn.JobOnLifecycleState.EmFabrico => "Em fabrico",
                    Domain.Modules.JobOn.JobOnLifecycleState.Fechado => "Fechado",
                    Domain.Modules.JobOn.JobOnLifecycleState.Cancelado => "Cancelado",
                    _ => "—"
                },
                LifecyclePillClass: s.LifecycleState switch
                {
                    Domain.Modules.JobOn.JobOnLifecycleState.EmFabrico => "approved",
                    Domain.Modules.JobOn.JobOnLifecycleState.Fechado => "approved",
                    _ => ""
                },
                PreparationDisplay: "—",
                PreparationPillClass: ""
            ))
            .ToList();

    /// <summary>
    /// Single-month build — exactly what the landing renders: markers and day
    /// list from the selected date's own month.
    /// </summary>
    public static Result Build(DateTime selectedDate, IReadOnlyList<HistoricalProductionSummary> monthSummaries) =>
        Build(selectedDate, selectedDate, monthSummaries, monthSummaries);

    /// <summary>
    /// Full build: calendar markers for <paramref name="markerMonthDate"/>
    /// (calendar navigation can display a month other than the selected
    /// date's — the selected-date rule is preserved: the day list always
    /// comes from the selected date's own month) and the day list for
    /// <paramref name="selectedDate"/>.
    /// </summary>
    public static Result Build(
        DateTime selectedDate,
        DateTime markerMonthDate,
        IReadOnlyList<HistoricalProductionSummary> markerMonthSummaries,
        IReadOnlyList<HistoricalProductionSummary> selectedDayMonthSummaries) =>
        new(
            SelectedDateValue: selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SelectedDateDisplay: FormatDateDisplay(selectedDate),
            Month: FormatMonth(markerMonthDate),
            RecordDatesCsv: BuildRecordDatesCsv(markerMonthSummaries),
            RecordLines: BuildRecordLines(markerMonthSummaries),
            Items: BuildDayItems(selectedDayMonthSummaries, selectedDate));
}
