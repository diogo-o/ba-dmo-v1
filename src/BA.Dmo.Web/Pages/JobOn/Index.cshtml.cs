using System.Globalization;
using System.Text.Json;
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

    // ---- Derived display values ----
    // Empty editable/input fields render BLANK when no real stored value exists.
    // Never render "—", "--", "{}", "[]", "null" or a serialized empty string as
    // a field value. Real values render unchanged.
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
            : "";
    public string ProductionDisplay => JobOn?.ProductionCode ?? "";
    // Production navigator label: "<production> · atual" when a real production is
    // loaded; a clean no-context state otherwise (never a placeholder dash joined
    // to "atual").
    public string ProductionNavDisplay =>
        string.IsNullOrWhiteSpace(JobOn?.ProductionCode)
            ? "Sem produção"
            : $"{JobOn.ProductionCode} · atual";
    public string MachineDisplay => JobOn?.MachineCode ?? "";
    public string StartDateDisplay => JobOn?.PlannedStartAt?.ToString("yyyy-MM-dd") ?? "";
    public string EndDateDisplay => JobOn?.PlannedEndAt?.ToString("yyyy-MM-dd") ?? "";
    public string SectionsDisplay => NormalizeSectionsDisplay(JobOn?.CurrentRevision?.Sections);
    public string DropCountDisplay => JobOn?.CurrentRevision?.DropCount?.ToString("0") ?? "";
    public string TypeDisplay => JobOn?.CurrentRevision?.TypeSnapshot ?? "";
    public string StopDisplay => JobOn?.CurrentRevision?.StopSnapshot ?? "";
    public string WeightDisplay => JobOn?.CurrentRevision?.WeightSnapshot?.ToString("0.00") ?? "";
    public string ProcessDisplay => JobOn?.CurrentRevision?.ProcessSnapshot ?? "";
    public string GeneralNotesDisplay => JobOn?.CurrentRevision?.GeneralNotes ?? "";
    public int RevisionCount => JobOn?.RevisionCount ?? 0;
    public int CurrentRevisionNumber => JobOn?.CurrentRevision?.RevisionNumber ?? 0;
    public IReadOnlyList<JobOnComponent> Components =>
        JobOn?.CurrentRevision?.Components ?? Array.Empty<JobOnComponent>();

    private static string NormalizeSectionsDisplay(string? sections)
    {
        if (string.IsNullOrWhiteSpace(sections))
        {
            return string.Empty;
        }

        var value = sections.Trim();
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.Object when !document.RootElement.EnumerateObject().Any() => string.Empty,
                JsonValueKind.Array when document.RootElement.GetArrayLength() == 0 => string.Empty,
                JsonValueKind.String => document.RootElement.GetString()?.Trim() ?? string.Empty,
                _ => value
            };
        }
        catch (JsonException)
        {
            return value;
        }
    }

    public JobOnComponent? Component(ComponentFamily family) =>
        Components.FirstOrDefault(component => component.Family == family);

    public static string ComponentFieldValue(JobOnComponentField field) => field.ValueType switch
    {
        "integer" => field.ValueInteger?.ToString(PtPt) ?? "",
        "decimal" => field.ValueDecimal?.ToString("0.##", PtPt) ?? "",
        "boolean" => field.ValueBoolean is null ? "" : field.ValueBoolean.Value ? "Sim" : "Não",
        "date" => field.ValueDate?.ToString("dd/MM/yyyy", PtPt) ?? "",
        _ => field.ValueText ?? ""
    };

    public static string ComponentFieldLabel(string fieldKey) => fieldKey switch
    {
        "diametro_exterior" => "Ø exterior",
        "diametro_corpo" => "Ø corpo",
        "diametro_pata" => "Ø pata",
        "diametro_gargalo" => "Ø gargalo",
        "fundo_final" => "Fundo final",
        "folgas" => "Folgas",
        "tipo" => "Tipo",
        "adaptador" => "Adaptador",
        "inversao" => "Inversão",
        "reparador" => "Reparador",
        "nominal" => "Nominal",
        "bacia" => "Bacia",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fieldKey.Replace('_', ' '))
    };

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
        // PHASE 4: the shared JobOnPlanningProjection owns the resolution +
        // projection semantics (the planning-only endpoint reuses the exact
        // same code — one implementation, identical behavior).
        var selectedDate = JobOnPlanningProjection.ResolveSelectedDate(date);
        var monthStart = JobOnPlanningProjection.MonthRange(selectedDate);

        SelectedDateValue = selectedDate.ToString("yyyy-MM-dd");
        SelectedDateDisplay = JobOnPlanningProjection.FormatDateDisplay(selectedDate);

        // R011 — The calendar markers and the list are built from ONE lightweight planning
        // projection (HistoricalProductionSummary: job_on_id, production, reference,
        // machine, planned dates, lifecycle). No full Job On documents are loaded to render
        // the calendar/list (§19). We load the whole displayed month so calendar markers
        // cover it, then filter the list to the selected day (§6/§9).
        var summaries = await _jobOnRepository.GetHistoricalProductionsAsync(
            referenceFilter: null,
            machineFilter: null,
            from: monthStart.From,
            to: monthStart.To,
            cancellationToken: HttpContext.RequestAborted);

        var planning = JobOnPlanningProjection.Build(selectedDate, summaries.ToList());
        RecordDatesCsv = planning.RecordDatesCsv;
        RecordLinesJson = System.Text.Json.JsonSerializer.Serialize(planning.RecordLines);
        PlaneamentoItems = planning.Items;

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
    string DayLabel,
    string TimeRange,
    string Reference,
    string Production,
    string Machine,
    int RevisionNumber,
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

public sealed record JobOnToolCardModel(
    JobOnComponent? Component,
    string Code,
    string Name,
    bool Priority,
    bool CanEdit,
    string? ExtraClass = null
);
