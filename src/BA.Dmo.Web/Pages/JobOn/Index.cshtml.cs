using System.Globalization;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.JobOn;

/// <summary>
/// Job On route surface (Plan-V3 05_SHL §5, UD-16, U-13).
/// Loads the current JobOn aggregate for data binding where U-13
/// authoritative data exists. Future-domain dependencies (tool families,
/// inventory, catalog) remain representative/read-only.
/// </summary>
public class IndexModel : PageModel
{
    private static readonly CultureInfo PtPt = new("pt-PT");

    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IJobOnRepository _jobOnRepository;
    private readonly JobOnService? _jobOnService;

    public IndexModel(
        ICurrentUserAccessor currentUserAccessor,
        IJobOnRepository jobOnRepository,
        JobOnService? jobOnService = null)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
        _jobOnRepository = jobOnRepository
            ?? throw new ArgumentNullException(nameof(jobOnRepository));
        // Optional so the landing page still functions when the service is not reachable
        // (test isolation). When present, opening a folha records the user-scoped
        // "current open Job On" context (R011 §14).
        _jobOnService = jobOnService;
    }

    // ---- Authorization ----
    public bool CanEdit { get; private set; }
    public bool CanConfigure { get; private set; }
    public bool CanConfirm { get; private set; }
    public bool CanViewControlo { get; private set; }
    public bool CanViewRepairs { get; private set; }

    // ---- U-13 authoritative JobOn data ----
    public Domain.Modules.JobOn.JobOn? JobOn { get; private set; }
    public Guid? JobOnId => JobOn?.Id;
    public Guid? CurrentRevisionId => JobOn?.CurrentRevision?.JobOnRevisionId;

    // ---- Planeamento ----
    public IReadOnlyList<PlaneamentoItem> PlaneamentoItems { get; private set; } = Array.Empty<PlaneamentoItem>();
    public string SelectedDateDisplay { get; private set; } = "—";
    public string SelectedDateValue { get; private set; } = "—";

    // ---- Verifications (from CurrentRevision) ----
    public IReadOnlyList<VerificationItem> VerificationItems { get; private set; } = Array.Empty<VerificationItem>();
    public int PendingVerificationCount { get; private set; }
    public bool HasPendingVerifications => PendingVerificationCount > 0;

    // ---- Derived display values (design-safe fallbacks per Design-Reference) ----
    public string LifecycleDisplay => JobOn?.LifecycleState switch
    {
        JobOnLifecycleState.Rascunho => "Rascunho",
        JobOnLifecycleState.Planeado => "Planeado",
        JobOnLifecycleState.EmFabrico => "Em fabrico",
        JobOnLifecycleState.Fechado => "Fechado",
        JobOnLifecycleState.Cancelado => "Cancelado",
        _ => "—"
    };

    /// <summary>
    /// Canonical .dmo-pill variant applied on the <see cref="dmo-pill"/> base.
    /// good/acesso = approved, pending = pending, neutral = "" (DMO §16).
    /// </summary>
    public string LifecyclePillClass => JobOn?.LifecycleState switch
    {
        JobOnLifecycleState.EmFabrico => "approved",
        JobOnLifecycleState.Fechado => "approved",
        _ => ""
    };

    public string ReferenceDisplay =>
        ArticleReferenceImageRules.ExtractReferenceCode(JobOn?.CurrentRevision?.ReferenceSnapshot)
        is { Length: > 0 } reference
            ? reference
            : "—";
    public string ProductionDisplay => JobOn?.ProductionCode ?? "—";
    public string MachineDisplay => JobOn?.MachineCode ?? "—";
    public string StartDateDisplay => JobOn?.PlannedStartAt?.ToString("yyyy-MM-dd") ?? "—";
    public string EndDateDisplay => JobOn?.PlannedEndAt?.ToString("yyyy-MM-dd") ?? "—";
    public string SectionsDisplay => JobOn?.CurrentRevision?.Sections ?? "—";
    public string DropCountDisplay => JobOn?.CurrentRevision?.DropCount?.ToString("0") ?? "—";
    public string TypeDisplay => JobOn?.CurrentRevision?.TypeSnapshot ?? "—";
    public string StopDisplay => JobOn?.CurrentRevision?.StopSnapshot ?? "";
    public string WeightDisplay => JobOn?.CurrentRevision?.WeightSnapshot?.ToString("0.00") ?? "—";
    public string ProcessDisplay => JobOn?.CurrentRevision?.ProcessSnapshot ?? "—";
    public string GeneralNotesDisplay => JobOn?.CurrentRevision?.GeneralNotes ?? "";
    public int RevisionCount => JobOn?.RevisionCount ?? 0;
    public int CurrentRevisionNumber => JobOn?.CurrentRevision?.RevisionNumber ?? 0;

    /// <summary>
    /// Stable planned dates carrying activity, bound to the canonical
    /// calendar's <c>data-record-dates</c> (GLM-JOB-05 rule 3 / GLM-JOB-08).
    /// </summary>
    public string RecordDatesCsv { get; private set; } = "";

    /// <summary>
    /// R011 — Date → production-line color keys, bound to the canonical calendar's
    /// <c>data-record-lines</c>. Each day maps to the DISTINCT lines with scheduled
    /// production (e.g. <c>{"2026-08-20":["b1","c2"]}</c>). Built from the SAME planning
    /// projection used for the list (§8), so calendar and list never diverge.
    /// Line tokens use the deterministic <see cref="JobOnLineColor"/> mapping (§4/§5).
    /// </summary>
    public string RecordLinesJson { get; private set; } = "{}";

    public async Task OnGetAsync(Guid? id = null, string? date = null)
    {
        var user = _currentUserAccessor.Current;
        CanEdit = user?.HasCapability(CanonicalModuleCatalog.JobonEditCapabilityId) == true;
        CanConfigure = user?.HasCapability(CanonicalModuleCatalog.JobonConfigureCapabilityId) == true;
        CanConfirm = user?.HasCapability(CanonicalModuleCatalog.JobonConfirmarCapabilityId) == true;
        CanViewControlo = user?.HasModule(CanonicalModuleCatalog.ControloAreaId) == true;
        CanViewRepairs = user?.HasModule(CanonicalModuleCatalog.ReparacaoInternaModuleId) == true;

        if (id.HasValue)
        {
            JobOn = await _jobOnRepository.GetByIdAsync(id.Value);

            // Build verification items from CurrentRevision.Verifications
            var verifications = JobOn?.CurrentRevision?.Verifications ?? Array.Empty<JobOnVerificationOccurrence>();
            PendingVerificationCount = verifications.Count(v => v.Status == "pendente");
            VerificationItems = verifications
                .Select(v => new VerificationItem(
                    OccurrenceId: v.JobOnVerificationOccurrenceId,
                    RuleText: v.RuleTextSnapshot ?? "—",
                    IsChecked: v.Status == "confirmada",
                    IsPending: v.Status == "pendente",
                    StatusDisplay: v.Status switch
                    {
                        "pendente" => "Pendente",
                        "confirmada" => "Confirmada",
                        "reposta" => "Reposta",
                        "desativada" => "Desativada",
                        _ => v.Status
                    },
                    StatusPillClass: v.Status switch
                    {
                        "pendente" => "pending",
                        "confirmada" => "approved",
                        _ => ""
                    },
                    CompletedBy: v.CompletedBy,
                    CompletedAt: v.CompletedAtUtc?.ToString("dd/MM/yyyy HH:mm")
                ))
                .ToList();
        }

        // Resolve selected date (the date shown/filtered; month drives the calendar).
        var selectedDate = DateTime.TryParseExact(date, "yyyy-MM-dd", null, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : DateTime.Today;

        SelectedDateValue = selectedDate.ToString("yyyy-MM-dd");
        SelectedDateDisplay = $"{selectedDate.Day} de {selectedDate.ToString("MMMM", PtPt).ToLower(PtPt)}";

        // R011 — The calendar markers and the list are built from ONE lightweight planning
        // projection (HistoricalProductionSummary: job_on_id, production, reference,
        // machine, planned dates, lifecycle). No full Job On documents are loaded to render
        // the calendar/list (§19). We load the whole displayed month so calendar markers
        // cover it, then filter the list to the selected day (§6/§9).
        var monthStart = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        var monthEndExclusive = monthStart.AddMonths(1);

        var summaries = await _jobOnRepository.GetHistoricalProductionsAsync(
            referenceFilter: null,
            machineFilter: null,
            from: monthStart,
            to: monthEndExclusive,
            cancellationToken: HttpContext.RequestAborted);

        var monthSummaries = summaries.ToList();

        // Stable record dates for the canonical calendar (planned_start_at only) — the
        // generic .has-record marker remains for any module that still consumes it.
        RecordDatesCsv = string.Join(",",
            monthSummaries
                .Select(s => s.PlannedStartAt?.ToString("yyyy-MM-dd"))
                .Where(d => d is not null)
                .OrderBy(d => d)
                .Distinct());

        // R011 line-color markers: date → distinct line color keys, so multiple
        // productions/lines on the same day are all represented (never silently hidden).
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

        var orderedMarkers = markers
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        RecordLinesJson = System.Text.Json.JsonSerializer.Serialize(orderedMarkers);

        // The list shows the productions of the SELECTED day from the same projection (§6/§9).
        var daySummaries = monthSummaries
            .Where(s => s.PlannedStartAt?.Date == selectedDate.Date)
            .ToList();

        PlaneamentoItems = daySummaries
            .Select(s => new PlaneamentoItem(
                JobOnId: s.JobOnId,
                Date: s.PlannedStartAt?.ToString("dd/MM/yyyy") ?? "—",
                DateIso: s.PlannedStartAt?.ToString("yyyy-MM-dd") ?? "",
                Reference: s.ReferenceCode ?? "—",
                Production: s.ProductionCode,
                Machine: s.MachineCode,
                LineColorKey: JobOnLineColor.GetColorKey(s.MachineCode),
                LifecycleDisplay: s.LifecycleState switch
                {
                    JobOnLifecycleState.Rascunho => "Rascunho",
                    JobOnLifecycleState.Planeado => "Planeado",
                    JobOnLifecycleState.EmFabrico => "Em fabrico",
                    JobOnLifecycleState.Fechado => "Fechado",
                    JobOnLifecycleState.Cancelado => "Cancelado",
                    _ => "—"
                },
                LifecyclePillClass: s.LifecycleState switch
                {
                    JobOnLifecycleState.EmFabrico => "approved",
                    JobOnLifecycleState.Fechado => "approved",
                    _ => ""
                },
                PreparationDisplay: "—",
                PreparationPillClass: ""
            ))
            .ToList();

        // R011 — Record the Job On this user explicitly opened when navigated with an id.
        // Preserves exact production identity for later Controlo "Carregar Job On atual".
        // The service resolves the actor + re-checks jobon.view server-side.
        if (id.HasValue && _jobOnService is not null)
        {
            await _jobOnService.SetCurrentOpenAsync(id.Value, HttpContext.RequestAborted);
        }
    }
}

public sealed record PlaneamentoItem(
    Guid JobOnId,
    string Date,
    string DateIso,
    string Reference,
    string Production,
    string Machine,
    string? LineColorKey,
    string LifecycleDisplay,
    string LifecyclePillClass,
    string PreparationDisplay,
    string PreparationPillClass
);

public sealed record VerificationItem(
    Guid OccurrenceId,
    string RuleText,
    bool IsChecked,
    bool IsPending,
    string StatusDisplay,
    string StatusPillClass,
    string? CompletedBy,
    string? CompletedAt
);
