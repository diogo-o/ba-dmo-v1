using System.Net;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Auth;

namespace BA.Dmo.IntegrationTests.Identity;

/// <summary>
/// U-05 privileged provisioning adapter tests (Plan-V3 PV-07, 06_DATA §14–15):
/// service_role is used exclusively here, stays on the server-side wire, and
/// never appears in messages; the adapter is idempotent.
/// </summary>
public class SupabaseAdminProvisioningAdapterTests
{
    private const string SupabaseUrl = "https://project.supabase.example";
    private const string ServiceRoleKey = "service-role-secret-value";

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

    private static string UserJson(string id, string email) =>
        $"{{\"id\":\"{id}\",\"email\":\"{email}\"}}";

    private static string ListingJson(params string[] userObjects) =>
        "{\"users\":[" + string.Join(",", userObjects) + "]}";

    private static string Repeat(Func<int, string> item, int count) =>
        string.Join(",", Enumerable.Range(0, count).Select(item));

    // ---- GetUserEmailsAsync pagination --------------------------------------

    [Fact]
    public async Task GetUserEmails_UserOnPageTwo_IsResolved()
    {
        // Page 1 is a full (100) page of unrelated users; the requested user only
        // appears on page 2. Without pagination the email would be silently missed.
        var targetId = "aaaaaaaa-0000-0000-0000-000000000001";
        var requestedId = Guid.Parse(targetId);
        var handler = new FakeHttpMessageHandler();
        // Page 1: 100 unrelated users (no match), full page -> continue.
        handler.Responders.Enqueue(_ => Json(ListingJson(
            Repeat(i => UserJson(Guid.NewGuid().ToString(), $"u{i}@ba-dmo.example"), 100))));
        // Page 2: short final page containing the requested user.
        handler.Responders.Enqueue(_ => Json(ListingJson(UserJson(targetId, "target@ba-dmo.example"))));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var emails = await adapter.GetUserEmailsAsync(new[] { requestedId });

        Assert.Equal("target@ba-dmo.example", emails[requestedId]);
        Assert.Contains("page=1&per_page=100", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("page=2&per_page=100", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetUserEmails_StopsWhenAllRequestedIdsFound()
    {
        // Both requested IDs are found on the first page -> exactly one request.
        var id1 = "bbbbbbbb-0000-0000-0000-000000000001";
        var id2 = "bbbbbbbb-0000-0000-0000-000000000002";
        var requested = new[] { Guid.Parse(id1), Guid.Parse(id2) };
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(ListingJson(
            UserJson(id1, "a@ba-dmo.example"), UserJson(id2, "b@ba-dmo.example"))));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var emails = await adapter.GetUserEmailsAsync(requested);

        Assert.Equal(2, emails.Count);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetUserEmails_StopsOnShortFinalPage()
    {
        // Page 1 has fewer than 100 users and none of the requested IDs is
        // present -> the loop must stop (a short page means no further pages).
        var requestedId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(ListingJson(
            UserJson(Guid.NewGuid().ToString(), "x@ba-dmo.example"))));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var emails = await adapter.GetUserEmailsAsync(new[] { requestedId });

        Assert.Empty(emails);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetUserEmails_DoesNotIssueOneRequestPerUser()
    {
        // 250 requested users must be resolved in far fewer than 250 HTTP calls:
        // users are scattered across 3 full/short pages (2 full pages of 100 + a
        // short page). One request per page, never one per user.
        var page1Ids = Enumerable.Range(0, 100)
            .Select(i => $"d1111111-0000-0000-0000-{(i):D12}").ToList();
        var page2Ids = Enumerable.Range(0, 100)
            .Select(i => $"d2222222-0000-0000-0000-{(i):D12}").ToList();
        var page3Ids = Enumerable.Range(0, 50)
            .Select(i => $"d3333333-0000-0000-0000-{(i):D12}").ToList();

        var requested = page1Ids.Concat(page2Ids).Concat(page3Ids)
            .Select(Guid.Parse).ToList();
        Assert.Equal(250, requested.Count);

        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(ListingJson(
            Repeat(i => UserJson(page1Ids[i], $"p1-{i}@ba-dmo.example"), 100))));
        handler.Responders.Enqueue(_ => Json(ListingJson(
            Repeat(i => UserJson(page2Ids[i], $"p2-{i}@ba-dmo.example"), 100))));
        handler.Responders.Enqueue(_ => Json(ListingJson(
            Repeat(i => UserJson(page3Ids[i], $"p3-{i}@ba-dmo.example"), 50))));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var emails = await adapter.GetUserEmailsAsync(requested);

        Assert.Equal(250, emails.Count);
        // One request per page (3), not one per user (250).
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetUserEmails_LookupFailure_ReturnsEmpty_WithoutThrowing()
    {
        var requestedId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
        var handler = new FakeHttpMessageHandler { Throw = new HttpRequestException("down") };
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var emails = await adapter.GetUserEmailsAsync(new[] { requestedId });

        Assert.Empty(emails);
        // The service-role value never leaks into any surfaced material.
    }

    [Fact]
    public async Task GetUserEmails_MissingConfiguration_ReturnsEmpty_WithoutHttpCalls()
    {
        var requestedId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000002");
        var handler = new FakeHttpMessageHandler();
        var adapter = new SupabaseAdminProvisioningAdapter(new HttpClient(handler), null, null);

        var emails = await adapter.GetUserEmailsAsync(new[] { requestedId });

        Assert.Empty(emails);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateUser_SendsServiceRoleOnlyServerSide_AndReturnsTheUserId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\",\"email\":\"admin@ba-dmo.example\"}"));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var result = await adapter.EnsureAuthUserAsync("admin@ba-dmo.example", "password");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            result.Value.AuthUserId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"{SupabaseUrl}/auth/v1/admin/users", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(ServiceRoleKey, request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ExistingAccount_IsResolvedIdempotently_ViaAdminLookup()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"msg\":\"User already registered\"}", HttpStatusCode.UnprocessableEntity));
        handler.Responders.Enqueue(_ => Json(
            "{\"users\":[{\"id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\",\"email\":\"admin@ba-dmo.example\"}]}"));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var result = await adapter.EnsureAuthUserAsync("admin@ba-dmo.example", "password");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            result.Value.AuthUserId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/auth/v1/admin/users?email=", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task MissingConfiguration_FailsClearly_WithoutHttpCalls()
    {
        var handler = new FakeHttpMessageHandler();
        var adapter = new SupabaseAdminProvisioningAdapter(new HttpClient(handler), null, null);

        var result = await adapter.EnsureAuthUserAsync("admin@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal("PROVISIONING_CONFIGURATION_MISSING", result.Error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task HardFailure_FailsClosed_AndNeverLeaksTheServiceRole()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json("{}", HttpStatusCode.InternalServerError));
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var result = await adapter.EnsureAuthUserAsync("admin@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
        Assert.DoesNotContain(ServiceRoleKey, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkFailure_FailsClosed_AndNeverLeaksTheServiceRole()
    {
        var handler = new FakeHttpMessageHandler { Throw = new HttpRequestException("down") };
        var adapter = new SupabaseAdminProvisioningAdapter(
            new HttpClient(handler), SupabaseUrl, ServiceRoleKey);

        var result = await adapter.EnsureAuthUserAsync("admin@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.DoesNotContain(ServiceRoleKey, result.Error.Message, StringComparison.Ordinal);
    }
}
