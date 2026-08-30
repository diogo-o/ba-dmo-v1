using System.Net;
using System.Net.Http.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Auth;
using BA.Dmo.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.JobOnImages;

public class JobOnImageWebApiTests : IClassFixture<JobOnImageWebApiTests.ImageFixture>
{
    private readonly ImageFixture _fixture;

    public JobOnImageWebApiTests(ImageFixture fixture)
    {
        _fixture = fixture;
        _fixture.Images.Associations.Clear();
    }

    [Fact]
    public async Task AttachAndRemove_ChangeReferenceAssociation_WithoutAddingRevision()
    {
        var client = _fixture.CreateTestClient();
        var login = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "jobon-images@ba-dmo.example",
            ["password"] = "correct"
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var beforeCount = _fixture.JobOns.RevisionCount;
        var attach = await client.PostAsJsonAsync(
            $"/api/jobon/{_fixture.JobOns.JobOnId}/image/attach",
            new { imageAssetId = "artigo-5447T173.jpg" });

        Assert.Equal(HttpStatusCode.OK, attach.StatusCode);
        Assert.Equal(beforeCount, _fixture.JobOns.RevisionCount);
        var association = Assert.Single(_fixture.Images.Associations.Values);
        Assert.Equal("5447T173", association.ReferenceCode);
        Assert.Equal("artigo-5447T173.jpg", association.ImageAssetId);

        var remove = await client.PostAsync(
            $"/api/jobon/{_fixture.JobOns.JobOnId}/image/remove",
            content: null);

        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.Equal(beforeCount, _fixture.JobOns.RevisionCount);
        Assert.Empty(_fixture.Images.Associations);
    }

    [Fact]
    public async Task UnsafePath_IsRejected_AndWritesNothing()
    {
        var client = _fixture.CreateTestClient();
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "jobon-images@ba-dmo.example",
            ["password"] = "correct"
        }));

        var response = await client.PostAsJsonAsync(
            $"/api/jobon/{_fixture.JobOns.JobOnId}/image/attach",
            new { imageAssetId = "..\\outside.jpg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_fixture.Images.Associations);
    }

    public sealed class ImageFixture : WebApplicationFactory<Program>
    {
        private static readonly Guid AuthUserId =
            Guid.Parse("88888888-2222-3333-4444-555555555555");

        public FakeJobOnRepository JobOns { get; } = new();
        public FakeArticleImageRepository Images { get; } = new();

        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                Replace<ISupabaseAuthAdapter>(services, new FakeAuthAdapter());
                Replace<IInternalUserRepository>(services, new FakeIdentityRepository());
                Replace<IJobOnRepository>(services, JobOns);
                Replace<IArticleReferenceImageRepository>(services, Images);
                services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(
                    options => options.Conventions.ConfigureFilter(
                        new IgnoreAntiforgeryTokenAttribute()));
            });
        }

        public HttpClient CreateTestClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        private static void Replace<TService>(IServiceCollection services, TService implementation)
            where TService : class
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
                services.Remove(descriptor);
            services.AddSingleton(implementation);
        }

        private sealed class FakeAuthAdapter : ISupabaseAuthAdapter
        {
            public Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
                string email,
                string password,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(AuthUserId, email)));
        }

        private sealed class FakeIdentityRepository : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                    "actor-jobon-images",
                    AuthUserId,
                    "Responsável Job On",
                    "Responsável",
                    UserActive: true,
                    TemplateId: "tpl-jobon-images",
                    TemplateName: "Job On",
                    TemplateActive: true,
                    ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[\"jobon.view\",\"jobon.edit\"]}]",
                    FunctionalProfile: "Responsável"));

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }

    public sealed class FakeArticleImageRepository : IArticleReferenceImageRepository
    {
        public Dictionary<string, ArticleReferenceImage> Associations { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<ArticleReferenceImage?> GetAsync(
            string referenceCode,
            CancellationToken cancellationToken = default)
        {
            Associations.TryGetValue(referenceCode, out var association);
            return Task.FromResult(association);
        }

        public Task SetAsync(
            ArticleReferenceImage association,
            Guid jobOnId,
            Guid? jobOnRevisionId,
            string eventType,
            string? beforeImageAssetId,
            string actorId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default)
        {
            Associations[association.ReferenceCode] = association;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string referenceCode,
            Guid jobOnId,
            Guid? jobOnRevisionId,
            string eventType,
            string beforeImageAssetId,
            string actorId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken = default)
        {
            Associations.Remove(referenceCode);
            return Task.CompletedTask;
        }
    }

    public sealed class FakeJobOnRepository : IJobOnRepository
    {
        private readonly Domain.Modules.JobOn.JobOn _jobOn;

        public FakeJobOnRepository()
        {
            JobOnId = Guid.Parse("77777777-2222-3333-4444-555555555555");
            var revision = new JobOnRevision
            {
                JobOnRevisionId = Guid.Parse("66666666-2222-3333-4444-555555555555"),
                JobOnId = JobOnId,
                RevisionNumber = 1,
                ReferenceSnapshot = "{\"article_reference\":\"5447T173\"}",
                SavedBy = "actor-jobon-images",
                SavedAtUtc = DateTime.UtcNow
            };
            _jobOn = new Domain.Modules.JobOn.JobOn(
                "202601", "B1", DateTimeOffset.UtcNow, null, new[] { revision });
            typeof(Domain.Modules.JobOn.JobOn)
                .GetProperty(nameof(Domain.Modules.JobOn.JobOn.Id))!
                .SetValue(_jobOn, JobOnId);
            _jobOn.SaveRevision(revision);
        }

        public Guid JobOnId { get; }
        public int RevisionCount => _jobOn.RevisionCount;

        public Task<Domain.Modules.JobOn.JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Domain.Modules.JobOn.JobOn?>(id == JobOnId ? _jobOn : null);

        public Task<Guid> CreateAsync(Domain.Modules.JobOn.JobOn jobOn, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> CreateAtomicallyAsync(Domain.Modules.JobOn.JobOn jobOn, JobOnRevision initialRevision, string actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Domain.Modules.JobOn.JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Domain.Modules.JobOn.JobOn>>(Array.Empty<Domain.Modules.JobOn.JobOn>());
        public Task<Domain.Modules.JobOn.JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default) => Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);
        public Task TransitionLifecycleAsync(BA.Dmo.Domain.Modules.JobOn.JobOn jobOn, string actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<JobOnRevision>>(_jobOn.Revisions);
        public Task InsertComponentsAsync(IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertFieldsAsync(IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertVerificationsAsync(IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> ConfirmVerificationOccurrenceAsync(Guid occurrenceId, string completedBy, DateTime completedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default) => Task.FromResult(_jobOn.CurrentRevisionId);
        public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task InsertImageMutationAsync(JobOnRevision newRevision, Guid jobOnId, string eventType, string? beforeImageAssetId, string? afterImageAssetId, string actorId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Reference image actions must not create revisions.");
        public Task SaveRevisionGraphAsync(JobOnRevision revision, string eventType, string actorId, string? beforeSnapshot = null, string? afterSnapshot = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AlterDatesAtomicallyAsync(Guid jobOnId, DateTimeOffset? plannedStartAt, DateTimeOffset? plannedEndAt, JobOnRevision newRevision, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> DuplicateAtomicallyAsync(Domain.Modules.JobOn.JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(Array.Empty<HistoricalProductionSummary>());
    }
}
