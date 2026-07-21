using System.Net;
using System.Text;

namespace GrowDiary.Web.Tests.TestFakes;

/// <summary>
/// Hand-rolled HTTP fake for Home Assistant: routes each request through a responder
/// function and records every request (method, URI, body) so tests can assert on the
/// exact traffic — URLs, payloads, and how often (or whether) HA was called at all.
/// </summary>
public sealed class RecordingHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    public RecordingHttpHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<(HttpMethod Method, Uri Uri, string? Body)> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, body));
        return _responder(request, body);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Fake HA entity-state JSON as returned by <c>GET /api/states/&lt;entity&gt;</c>.</summary>
    public static string EntityStateJson(string entityId, string state, string? unit = null, string? friendlyName = null)
    {
        var attributes = new List<string>();
        if (friendlyName is not null) attributes.Add($"\"friendly_name\":\"{friendlyName}\"");
        if (unit is not null) attributes.Add($"\"unit_of_measurement\":\"{unit}\"");
        return $"{{\"entity_id\":\"{entityId}\",\"state\":\"{state}\",\"last_changed\":\"2026-07-21T10:00:00+00:00\",\"attributes\":{{{string.Join(',', attributes)}}}}}";
    }
}

public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
