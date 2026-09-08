using System.Text.Json;
using BA.Dmo.Application.Shared;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>Result of generating a Job On document.</summary>
public sealed record GeneratedJobOnDocument(byte[] PdfBytes, string FileName);

/// <summary>
/// Application-layer Job On PDF generation service.
/// Reads the current revision snapshot from the repository and builds the
/// 4-page document set for printing/distribution to production sections.
/// </summary>
public sealed class JobOnPdfService
{
    private readonly IJobOnRepository _repository;
    private readonly JobOnAuthorizationGate _gate;
    private readonly IJobOnImageProvider? _imageProvider;

    public JobOnPdfService(
        IJobOnRepository repository,
        JobOnAuthorizationGate gate,
        IJobOnImageProvider? imageProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _imageProvider = imageProvider;
    }

    /// <summary>
    /// Generates the full 4-page Job On document set as PDF bytes + canonical filename.
    /// Requires jobon.view capability. Deterministic output from the current revision snapshot.
    /// </summary>
    public async Task<Result<GeneratedJobOnDocument, DomainError>> GenerateAsync(
        IJobOnPdfRenderer renderer,
        Guid jobOnId,
        CancellationToken ct = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonViewCapabilityId);
        if (gate.IsFailure)
            return Result<GeneratedJobOnDocument, DomainError>.Failure(gate.Error);

        var jobOn = await _repository.GetByIdAsync(jobOnId, ct);
        if (jobOn is null)
            return Result<GeneratedJobOnDocument, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        return await GenerateRevisionCoreAsync(renderer, jobOn, jobOn.CurrentRevision, ct);
    }

    /// <summary>Generates the document for one exact immutable revision.</summary>
    public async Task<Result<GeneratedJobOnDocument, DomainError>> GenerateRevisionAsync(
        IJobOnPdfRenderer renderer,
        Guid jobOnId,
        Guid revisionId,
        CancellationToken ct = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonViewCapabilityId);
        if (gate.IsFailure)
            return Result<GeneratedJobOnDocument, DomainError>.Failure(gate.Error);

        var jobOn = await _repository.GetByIdAsync(jobOnId, ct);
        if (jobOn is null)
            return Result<GeneratedJobOnDocument, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var revision = jobOn.Revisions.SingleOrDefault(r => r.JobOnRevisionId == revisionId);
        if (revision is null)
            return Result<GeneratedJobOnDocument, DomainError>.Failure(DomainError.NotFound(
                "JOBON_REVISION_NOT_FOUND", "Revisão Job On não encontrada."));

        return await GenerateRevisionCoreAsync(renderer, jobOn, revision, ct);
    }

    private async Task<Result<GeneratedJobOnDocument, DomainError>> GenerateRevisionCoreAsync(
        IJobOnPdfRenderer renderer,
        JobOnEntity jobOn,
        JobOnRevision? revision,
        CancellationToken ct)
    {
        var data = BuildPdfData(jobOn, revision);
        if (_imageProvider is not null)
        {
            // A historical revision does not own an image snapshot. Printing
            // consumes the current master association for its Article/Reference.
            var image = await _imageProvider.ResolveAsync(jobOn.Id, ct);
            if (image is not null)
            {
                data = data with
                {
                    ImageBytes = image.Bytes,
                    ImageMimeType = image.MimeType
                };
            }
        }
        var pdfBytes = renderer.RenderJobOnDocument(data);
        var fileName = BuildFileName(jobOn, revision);

        return Result<GeneratedJobOnDocument, DomainError>.Success(new GeneratedJobOnDocument(pdfBytes, fileName));
    }

    private static JobOnPdfData BuildPdfData(JobOnEntity jobOn, JobOnRevision? revision)
    {
        if (revision is null)
        {
            // Return minimal data — no revision means no tool data at all.
            return new JobOnPdfData
            {
                Reference = string.Empty,
                ProductionCode = jobOn.ProductionCode,
                MachineCode = jobOn.MachineCode,
                PlannedStartAt = jobOn.PlannedStartAt,
                PlannedEndAt = jobOn.PlannedEndAt
            };
        }

        var components = revision.Components ?? Array.Empty<Domain.Modules.JobOn.JobOnComponent>();
        var byFamily = components.ToDictionary(c => c.Family, c => c);

        var verifications = FlattenVerifications(components);

        return new JobOnPdfData
        {
            Reference = ArticleReferenceImageRules.ExtractReferenceCode(
                revision.ReferenceSnapshot),
            ProductionCode = ExtractSnapshotString(revision.ProductionSnapshot, "production_code") ?? jobOn.ProductionCode,
            MachineCode = ExtractSnapshotString(revision.MachineSnapshot, "machine_code") ?? jobOn.MachineCode,
            Sections = ParseSections(revision.Sections),
            DropCount = revision.DropCount,
            Weight = revision.WeightSnapshot,
            TypeSnapshot = revision.TypeSnapshot,
            StopSnapshot = revision.StopSnapshot,
            ProcessSnapshot = revision.ProcessSnapshot,
            PlannedStartAt = ExtractSnapshotDate(revision.DatesSnapshot, "start_at") ?? jobOn.PlannedStartAt,
            PlannedEndAt = ExtractSnapshotDate(revision.DatesSnapshot, "end_at") ?? jobOn.PlannedEndAt,
            GeneralNotes = revision.GeneralNotes,
            RevisionNumber = revision.RevisionNumber,

            Cm = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.MP_CM)),
            Mf = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.MF)),
            Tp = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.TP)),
            Bq = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.BQ)),
            An = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.AN)),
            Pu = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.PU)),
            Arr = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.ARR)),
            Pi = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.PI)),
            Cs = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.CS)),
            Fo = ToPdfComponent(byFamily.GetValueOrDefault(ComponentFamily.FO)),

            CalibreRows = byFamily.GetValueOrDefault(ComponentFamily.CAL)?.Rows
                ?.Select(r => new JobOnPdfCalibreRow(r.ElementLabel, r.ValueText ?? r.ValueDecimal?.ToString(), r.MachineQuantity))
                .ToList()
                .AsReadOnly()
                ?? (IReadOnlyList<JobOnPdfCalibreRow>)Array.Empty<JobOnPdfCalibreRow>(),

            Verifications = verifications
        };
    }

    private static IReadOnlyList<JobOnPdfVerification> FlattenVerifications(
        IReadOnlyList<Domain.Modules.JobOn.JobOnComponent> components)
    {
        var all = new List<JobOnPdfVerification>();
        foreach (var comp in components)
        {
            if (comp.Verifications is not null)
            {
                foreach (var v in comp.Verifications)
                {
                    all.Add(new JobOnPdfVerification(
                        RuleText: v.RuleTextSnapshot ?? "—",
                        IsChecked: v.Status == "confirmada",
                        StatusText: v.Status switch
                        {
                            "pendente" => "Pendente",
                            "confirmada" => "Confirmada",
                            "reposta" => "Reposta",
                            "desativada" => "Desativada",
                            _ => v.Status
                        }));
                }
            }
        }
        return all.AsReadOnly();
    }

    private static JobOnPdfComponent? ToPdfComponent(Domain.Modules.JobOn.JobOnComponent? comp)
    {
        if (comp is null) return null;

        var fields = new Dictionary<string, string>();
        if (comp.Fields is not null)
        {
            foreach (var f in comp.Fields)
            {
                var value = f.ValueText
                    ?? f.ValueInteger?.ToString()
                    ?? f.ValueDecimal?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    ?? (f.ValueBoolean == true ? "Sim" : f.ValueBoolean == false ? "Não" : "")
                    ?? f.ValueDate?.ToString("dd/MM/yyyy")
                    ?? "";
                fields[f.FieldKey] = value;
            }
        }

        return new JobOnPdfComponent
        {
            Reference = comp.ReferenceSnapshot ?? "",
            Lot = comp.LotSnapshot,
            TechnicalName = comp.TechnicalNameSnapshot,
            Usage = comp.UsageSnapshot,
            Notes = comp.Notes,
            Stock = comp.StockSnapshot,
            MachineQuantity = comp.PlannedQuantity,
            Fields = fields
        };
    }

    private static string? ExtractSnapshotString(string? json, string property)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(property, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ExtractSnapshotDate(string? json, string property)
    {
        var value = ExtractSnapshotString(json, property);
        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static int ParseSections(string? sectionsJson)
    {
        if (string.IsNullOrWhiteSpace(sectionsJson)) return 0;
        try
        {
            return int.Parse(sectionsJson);
        }
        catch
        {
            return 0;
        }
    }

    internal static string BuildFileName(JobOnEntity jobOn, JobOnRevision? revision = null)
    {
        var reference = ArticleReferenceImageRules.ExtractReferenceCode(
            (revision ?? jobOn.CurrentRevision)?.ReferenceSnapshot);
        var revisionSuffix = revision is not null && revision.JobOnRevisionId != jobOn.CurrentRevisionId
            ? $"_R{revision.RevisionNumber}"
            : string.Empty;
        return $"JobOn_{jobOn.ProductionCode}_{reference}_{jobOn.MachineCode}{revisionSuffix}.pdf";
    }
}
