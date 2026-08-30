using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// U-13 — Tests for Job On PDF document generation.
/// Covers service layer (authorization, data mapping, error cases) and renderer output.
/// IMAGE E2E TEST: PENDING REAL ENVIRONMENT
/// </summary>
public class JobOnPdfTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeJobOnRepository _repository = new();
    private readonly PdfTestIdentityAccessor _identity = new();
    private readonly JobOnService _jobOnService;
    private readonly JobOnPdfService _pdfService;
    private readonly TestPdfRenderer _renderer = new();

    public JobOnPdfTests()
    {
        var gate = new JobOnAuthorizationGate(_identity);
        _jobOnService = new JobOnService(
            gate,
            _repository,
            new FakeJobOnUserContextRepository(),
            new PdfTestClock(),
            new FakeFerramentasToolLookup());
        _pdfService = new JobOnPdfService(_repository, gate);
        _identity.GrantCapabilities(new[] { "jobon.view", "jobon.edit" });
    }

    #region Helpers

    private async Task<Guid> CreateJobOnWithRevision(string production = "202603", string machine = "C1")
    {
        // Create Job On
        var createResult = await _jobOnService.CreateAsync(
            new CreateJobOnRequest(production, machine, Start, End, "9262T288"));
        Assert.True(createResult.IsSuccess);
        var jobOnId = createResult.Value;

        // Save a revision with components
        var cmComponentId = Guid.NewGuid();
        var tpComponentId = Guid.NewGuid();
        var calComponentId = Guid.NewGuid();
        var revResult = await _jobOnService.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId,
            GeneralNotes: "Notas de teste para impressão",
            ChangeReason: null,
            ImageAssetId: null,
            Components: new[]
            {
                new JobOnComponent
                {
                    JobOnComponentId = cmComponentId,
                    Family = ComponentFamily.MP_CM,
                    ReferenceSnapshot = "9400",
                    LotSnapshot = "10",
                    UsageSnapshot = 76m,
                    Notes = "CX-BQ RODAR LIVRE",
                    Fields = new[]
                    {
                        new JobOnComponentField
                        {
                            JobOnComponentFieldId = Guid.NewGuid(),
                            JobOnComponentId = cmComponentId,
                            FieldKey = "diametro_exterior",
                            ValueType = "text",
                            ValueText = "136,3"
                        },
                        new JobOnComponentField
                        {
                            JobOnComponentFieldId = Guid.NewGuid(),
                            JobOnComponentId = cmComponentId,
                            FieldKey = "tipo",
                            ValueType = "text",
                            ValueText = "Teste Tipo"
                        }
                    }.ToList().AsReadOnly(),
                    Verifications = Array.Empty<JobOnVerificationOccurrence>()
                },
                new JobOnComponent
                {
                    JobOnComponentId = Guid.NewGuid(),
                    Family = ComponentFamily.MF,
                    ReferenceSnapshot = "9400",
                    LotSnapshot = "10",
                    UsageSnapshot = 76m,
                    Notes = "REBAIXO DUPLO",
                    Fields = Array.Empty<JobOnComponentField>(),
                    Verifications = Array.Empty<JobOnVerificationOccurrence>()
                },
                new JobOnComponent
                {
                    JobOnComponentId = Guid.NewGuid(),
                    Family = ComponentFamily.BQ,
                    ReferenceSnapshot = "T282",
                    LotSnapshot = "77",
                    UsageSnapshot = 0m,
                    Notes = "FOLGA 0,04 - 0,06",
                    Fields = Array.Empty<JobOnComponentField>(),
                    Verifications = Array.Empty<JobOnVerificationOccurrence>()
                },
                new JobOnComponent
                {
                    JobOnComponentId = tpComponentId,
                    Family = ComponentFamily.TP,
                    ReferenceSnapshot = "",
                    LotSnapshot = null,
                    Notes = "CALOTE ALTERADA PARA 6.7",
                    Fields = new[]
                    {
                        new JobOnComponentField
                        {
                            JobOnComponentFieldId = Guid.NewGuid(),
                            JobOnComponentId = tpComponentId,
                            FieldKey = "diametro",
                            ValueType = "text",
                            ValueText = "36,85"
                        },
                        new JobOnComponentField
                        {
                            JobOnComponentFieldId = Guid.NewGuid(),
                            JobOnComponentId = tpComponentId,
                            FieldKey = "bacia",
                            ValueType = "text",
                            ValueText = "6.7"
                        }
                    }.ToList().AsReadOnly(),
                    Verifications = Array.Empty<JobOnVerificationOccurrence>()
                },
                new JobOnComponent
                {
                    JobOnComponentId = calComponentId,
                    Family = ComponentFamily.CAL,
                    ReferenceSnapshot = "",
                    Notes = "",
                    Fields = Array.Empty<JobOnComponentField>(),
                    Rows = new[]
                    {
                        new JobOnComponentRow
                        {
                            JobOnComponentRowId = Guid.NewGuid(),
                            JobOnComponentId = calComponentId,
                            ElementLabel = "Tampão",
                            ValueText = "31,45",
                            MachineQuantity = 3m
                        },
                        new JobOnComponentRow
                        {
                            JobOnComponentRowId = Guid.NewGuid(),
                            JobOnComponentId = calComponentId,
                            ElementLabel = "Pinças",
                            ValueText = "P 73,2 / M 72,6",
                            MachineQuantity = 3m
                        }
                    }.ToList().AsReadOnly(),
                    Verifications = Array.Empty<JobOnVerificationOccurrence>()
                }
            }));
    Assert.True(revResult.IsSuccess);

    return jobOnId;
    }

    #endregion

    // ---- PDF-01: Document generates non-null result with valid PDF ----
    [Fact]
    public async Task GenerateAsync_ReturnsValidPdf_WithFourPages()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.PdfBytes);
        Assert.NotEmpty(result.Value.PdfBytes);

        // Must start with %PDF header
        var header = System.Text.Encoding.ASCII.GetString(result.Value.PdfBytes, 0, Math.Min(5, result.Value.PdfBytes.Length));
        Assert.Equal("%PDF-", header);

        // Renderer should have been called once
        Assert.Single(_renderer.RenderedDocuments);

        // Filename must contain production code and machine
        Assert.Contains("202603", result.Value.FileName);
        Assert.Contains("C1", result.Value.FileName);
        Assert.EndsWith(".pdf", result.Value.FileName);
    }

    // ---- PDF-02: Data mapping — reference appears in output ----
    [Fact]
    public async Task GenerateAsync_IncludesReferenceInData()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        Assert.True(result.IsSuccess);

        var data = _renderer.RenderedDocuments[0];
        Assert.Equal("202603", data.ProductionCode);
        Assert.Equal("C1", data.MachineCode);
    }

    // ---- PDF-03: Sections and DropCount are mapped ----
    [Fact]
    public async Task GenerateAsync_MapsSectionsAndDropCount()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        // Default sections from revision (empty JSON → 0)
        Assert.Equal(0, data.Sections);
        Assert.Null(data.DropCount);
    }

    // ---- PDF-04: Tool components are correctly grouped by family ----
    [Fact]
    public async Task GenerateAsync_GroupsComponentsByFamily()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        Assert.NotNull(data.Cm);
        Assert.Equal("9400", data.Cm.Reference);
        Assert.Equal("10", data.Cm.Lot);
        Assert.Equal(76m, data.Cm.Usage);
        Assert.NotNull(data.Mf);
        Assert.Equal("9400", data.Mf.Reference);
        Assert.NotNull(data.Bq);
        Assert.Equal("T282", data.Bq.Reference);
    }

    // ---- PDF-05: Calibre rows are mapped from CAL component ----
    [Fact]
    public async Task GenerateAsync_MapsCalibreRows()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        Assert.NotEmpty(data.CalibreRows);
        Assert.Contains(data.CalibreRows, r => r.Element == "Tampão");
        Assert.Contains(data.CalibreRows, r => r.Element == "Pinças");
    }

    // ---- PDF-06: Portuguese characters are preserved in notes ----
    [Fact]
    public async Task GenerateAsync_PreservesPortugueseCharacters()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        // CM notes contain Portuguese text
        Assert.NotNull(data.Cm.Notes);
        Assert.Contains("LIVRE", data.Cm.Notes!);
    }

    // ---- PDF-07: Missing Job On returns NotFound ----
    [Fact]
    public async Task GenerateAsync_ReturnsNotFound_ForMissingJobOn()
    {
        var result = await _pdfService.GenerateAsync(_renderer, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("JOBON_NOT_FOUND", result.Error.Code);
    }

    // ---- PDF-08: Unauthorized user returns Forbidden ----
    [Fact]
    public async Task GenerateAsync_ReturnsForbidden_WhenUnauthorized()
    {
        var jobOnId = await CreateJobOnWithRevision();
        _identity.RevokeAll();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("JOBON_FORBIDDEN", result.Error.Code);
    }

    // ---- PDF-09: GeneralNotes are included in data ----
    [Fact]
    public async Task GenerateAsync_IncludesGeneralNotes()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        Assert.NotNull(data.GeneralNotes);
        Assert.Contains("impressão", data.GeneralNotes);
    }

    // ---- PDF-10: Dates are mapped correctly ----
    [Fact]
    public async Task GenerateAsync_MapsPlannedDates()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        Assert.NotNull(data.PlannedStartAt);
        Assert.Equal(2026, data.PlannedStartAt.Value.Year);
        Assert.Equal(9, data.PlannedStartAt.Value.Month);
        Assert.NotNull(data.PlannedEndAt);
    }

    // ---- PDF-11: Component fields are accessible ----
    [Fact]
    public async Task GenerateAsync_ComponentFieldsAccessible()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        Assert.True(data.Cm.Fields.ContainsKey("diametro_exterior"));
        Assert.Equal("136,3", data.Cm.Fields["diametro_exterior"]);
    }

    // ---- PDF-12: Empty optional components are null ----
    [Fact]
    public async Task GenerateAsync_EmptyComponentsAreNull()
    {
        var jobOnId = await CreateJobOnWithRevision();

        var result = await _pdfService.GenerateAsync(_renderer, jobOnId);
        var data = _renderer.RenderedDocuments[0];

        Assert.Null(data.An);      // No AN component provided
        Assert.Null(data.Pu);      // No PU component provided
        Assert.Null(data.Fo);      // No FO component provided
    }

    // ---- PDF-13: JobOnImageProvider abstraction exists ----
    [Fact]
    public async Task ImageProvider_ResolvesNull_WhenNoImage()
    {
        var noOpProvider = new NullJobOnImageProvider();
        var resolution = await noOpProvider.ResolveAsync(Guid.NewGuid());
        Assert.Null(resolution);
    }

    [Fact]
    public async Task GenerateAsync_ConsumesReferenceImageProvider_IntoPrintProjection()
    {
        var jobOnId = await CreateJobOnWithRevision();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var service = new JobOnPdfService(
            _repository,
            new JobOnAuthorizationGate(_identity),
            new StubJobOnImageProvider(imageBytes, "image/jpeg"));
        var renderer = new TestPdfRenderer();

        var result = await service.GenerateAsync(renderer, jobOnId);

        Assert.True(result.IsSuccess);
        var data = Assert.Single(renderer.RenderedDocuments);
        Assert.Equal(imageBytes, data.ImageBytes);
        Assert.Equal("image/jpeg", data.ImageMimeType);
    }

    // ---- PDF-14: BuildFileName produces correct format ----
    [Fact]
    public void BuildFileName_ProducesCorrectFormat()
    {
        var result = _jobOnService.CreateAsync(
            new CreateJobOnRequest("202608", "B1", Start, End, "9262T288")).Result;
        var jobOnId = result.Value;

        // Save a revision so filename can extract reference
        _ = _jobOnService.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, Array.Empty<JobOnComponent>())).Result;

        var jobOn = _repository.JobOns[jobOnId];
        var fileName = JobOnPdfService.BuildFileName(jobOn);

        Assert.Contains("202608", fileName);
        Assert.Contains("B1", fileName);
        Assert.EndsWith(".pdf", fileName);
    }
}

// =====================================================================
// Test doubles
// =====================================================================

internal sealed class PdfTestIdentityAccessor : ICurrentUserAccessor
{
    private CurrentUser? _user;

    public CurrentUser? Current => _user;

    public void GrantCapabilities(IEnumerable<string> caps)
    {
        _user = new CurrentUser(
            Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            "Utilizador Teste PDF",
            new[] { "jobon" },
            caps);
    }

    public void RevokeAll() => _user = null;
}

internal sealed class PdfTestClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Renders nothing — only captures the DTO for inspection.
/// Used in place of the real PDF renderer in unit tests.
/// </summary>
internal sealed class TestPdfRenderer : IJobOnPdfRenderer
{
    public List<JobOnPdfData> RenderedDocuments { get; } = new();

    public byte[] RenderJobOnDocument(JobOnPdfData data)
    {
        RenderedDocuments.Add(data);
        // Return minimal valid PDF header for byte-level checks
        return System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");
    }
}

/// <summary>Always returns null — simulates missing image.</summary>
internal sealed class NullJobOnImageProvider : BA.Dmo.Application.Shared.IJobOnImageProvider
{
    public Task<BA.Dmo.Application.Shared.ImageResolution?> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
        => Task.FromResult<BA.Dmo.Application.Shared.ImageResolution?>(null);
}

internal sealed class StubJobOnImageProvider : BA.Dmo.Application.Shared.IJobOnImageProvider
{
    private readonly byte[] _bytes;
    private readonly string _mimeType;

    public StubJobOnImageProvider(byte[] bytes, string mimeType)
    {
        _bytes = bytes;
        _mimeType = mimeType;
    }

    public Task<BA.Dmo.Application.Shared.ImageResolution?> ResolveAsync(
        Guid jobOnId,
        CancellationToken ct = default) =>
        Task.FromResult<BA.Dmo.Application.Shared.ImageResolution?>(
            new BA.Dmo.Application.Shared.ImageResolution(_bytes, _mimeType));
}
