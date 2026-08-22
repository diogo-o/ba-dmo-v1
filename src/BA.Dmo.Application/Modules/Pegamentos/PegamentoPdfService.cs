using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Render a Pegamentos control sheet to PDF bytes from the frozen historical
/// snapshot. Deterministic output for deterministic input.
/// This is a Pegamentos-owned port — the concrete PDF library is an
/// infrastructure decision (mirrors GLM-PESO-09's IPdfRenderer rule).
/// </summary>
public interface IPegamentoPdfRenderer
{
    byte[] RenderPegamento(PegamentoPdfData data);
}

/// <summary>
/// Structured data of a Pegamentos document derived from the APPROVED /
/// persisted Pegamento snapshot. Never from live Job On state (CRITICAL RULE).
/// </summary>
public sealed record PegamentoPdfData
{
    public string Reference { get; init; } = string.Empty;
    public string ProductionCode { get; init; } = string.Empty;
    public string MachineCode { get; init; } = string.Empty;
    public Guid JobOnRevisionId { get; init; }

    // Component snapshot data
    public string CmReference { get; init; } = string.Empty;
    public string? CmLot { get; init; }
    public decimal? CmNominal { get; init; }
    public string BqReference { get; init; } = string.Empty;
    public string? BqLot { get; init; }
    public decimal? BqNominal { get; init; }
    public string MfReference { get; init; } = string.Empty;
    public string? MfLot { get; init; }
    public decimal? MfNominal { get; init; }

    public decimal Tolerance { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notas { get; init; }

    public IReadOnlyList<PegamentoPdfMeasurementRow> Measurements { get; init; } = Array.Empty<PegamentoPdfMeasurementRow>();

    public DateTimeOffset GeneratedAtUtc { get; init; }
}

/// <summary>One measurement row for the Pegamentos PDF rendering.</summary>
public sealed record PegamentoPdfMeasurementRow
{
    public string ComponentKey { get; init; } = string.Empty;
    public int? ToolNumber { get; init; }
    public decimal? Costura { get; init; }
    public decimal? ContraCostura { get; init; }
    public decimal? Ovalizacao { get; init; }
    public decimal? Media { get; init; }
}

/// <summary>Result of generating a Pegamentos document from the frozen snapshot.</summary>
public sealed record GeneratedDocument(byte[] PdfBytes, string FileName);

/// <summary>
/// Application-layer Pegamentos PDF generation service.
/// Generates PDF bytes + canonical filename from the persisted control snapshot.
/// Does NOT persist pegamento_documentos — that happens only after browser
/// confirmation (see PegamentoService.ConfirmDocumentSavedAsync).
/// </summary>
public sealed class PegamentoPdfService
{
    private readonly IPegamentoRepository _repository;
    private readonly PegamentoAuthorizationGate _gate;

    public PegamentoPdfService(IPegamentoRepository repository, PegamentoAuthorizationGate gate)
    {
        _repository = repository;
        _gate = gate;
    }

    /// <summary>
    /// Generates PDF bytes + filename for a control from its frozen historical
    /// snapshot. Returns the document artifact for the browser to save; does NOT
    /// touch pegamento_documentos.
    /// </summary>
    public async Task<Result<GeneratedDocument, DomainError>> GenerateAsync(
        IPegamentoPdfRenderer renderer,
        Guid controloId,
        CancellationToken ct = default)
    {
        var actorId = _gate.ResolveActorId();
        if (actorId is null)
            return Result<GeneratedDocument, DomainError>.Failure(DomainError.Forbidden(
                "PEGAMENTO_UNAUTHORIZED", "Acesso não autorizado ao módulo Pegamentos."));

        var control = await _repository.GetByIdAsync(controloId, ct);
        if (control is null)
            return Result<GeneratedDocument, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        var data = BuildPdfData(control);
        var pdfBytes = renderer.RenderPegamento(data);
        var fileName = PegamentoPdfFilename.Compute(control);

        return Result<GeneratedDocument, DomainError>.Success(new GeneratedDocument(pdfBytes, fileName));
    }

    private static PegamentoPdfData BuildPdfData(PegamentoControlo control)
    {
        return new PegamentoPdfData
        {
            Reference = control.ReferenceSnapshot,
            ProductionCode = control.ProductionCode,
            MachineCode = control.MachineCode,
            JobOnRevisionId = control.JobOnRevisionId,
            CmReference = control.CmSnapshot?.ReferenceSnapshot ?? string.Empty,
            CmLot = control.CmSnapshot?.LotSnapshot,
            CmNominal = control.CmNominal,
            BqReference = control.BqSnapshot?.ReferenceSnapshot ?? string.Empty,
            BqLot = control.BqSnapshot?.LotSnapshot,
            BqNominal = control.BqNominal,
            MfReference = control.MfSnapshot?.ReferenceSnapshot ?? string.Empty,
            MfLot = control.MfSnapshot?.LotSnapshot,
            MfNominal = control.MfNominal,
            Tolerance = control.Tolerance,
            Status = control.Status.ToString(),
            Notas = control.Notas,
            Measurements = control.Measurements
                .Select(m => new PegamentoPdfMeasurementRow
                {
                    ComponentKey = m.ComponentKey.ToString(),
                    ToolNumber = m.ToolNumber,
                    Costura = m.Costura,
                    ContraCostura = m.ContraCostura,
                    Ovalizacao = m.Ovalizacao,
                    Media = m.Media
                })
                .ToList()
                .AsReadOnly(),
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }
}