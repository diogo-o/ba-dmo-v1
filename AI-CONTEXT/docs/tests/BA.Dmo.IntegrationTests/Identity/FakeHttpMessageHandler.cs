namespace BA.Dmo.IntegrationTests.Identity;

/// <summary>
/// Scriptable HTTP handler for adapter tests (confined to tests/*).
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    public Queue<Func<HttpRequestMessage, HttpResponseMessage>> Responders { get; } = new();

    public Exception? Throw { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (Throw is not null)
            throw Throw;

        return Responders.Count > 0
            ? Responders.Dequeue()(request)
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK);
    }
}
