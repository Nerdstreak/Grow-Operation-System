using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services.Ai;

/// <summary>The outcome of talking to the provider, including the ways it can go wrong.</summary>
public sealed record AiCallResult(bool Ok, string? Raw, string? ErrorCode, string? ErrorMessage)
{
    public static AiCallResult Success(string raw) => new(true, raw, null, null);
    public static AiCallResult Failure(string code, string message) => new(false, null, code, message);
}

/// <summary>
/// Talks to an OpenAI-compatible chat endpoint.
///
/// "OpenAI-compatible" is the whole point: the same handful of fields is spoken by the
/// hosted providers and by Ollama or LM Studio running on the user's own machine, so one
/// implementation covers "send it to a provider" and "nothing leaves the house".
/// </summary>
public sealed class AiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiClient> _logger;

    public AiClient(IHttpClientFactory httpClientFactory, ILogger<AiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AiCallResult> CompleteAsync(
        AiSettings settings,
        string systemMessage,
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (!settings.IsUsable)
        {
            return AiCallResult.Failure("ai_not_configured", "Es ist kein KI-Modell eingerichtet.");
        }

        var payload = new
        {
            model = settings.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = userMessage },
            },
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(settings.BaseUrl!))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Never log the body wholesale: it echoes the prompt, and the prompt holds
                // the user's grow data.
                _logger.LogWarning("KI-Anbieter antwortete mit {Status}", (int)response.StatusCode);
                return AiCallResult.Failure(
                    "ai_provider_error",
                    $"Der KI-Anbieter antwortete mit HTTP {(int)response.StatusCode}.");
            }

            var content = ReadContent(body);
            return content is null
                ? AiCallResult.Failure("ai_bad_response", "Die Antwort des Anbieters war nicht lesbar.")
                : AiCallResult.Success(content);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiCallResult.Failure("ai_timeout", "Das Modell hat nicht rechtzeitig geantwortet.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "KI-Anbieter nicht erreichbar");
            return AiCallResult.Failure("ai_unreachable", "Der KI-Anbieter ist nicht erreichbar.");
        }
    }

    /// <summary>Tolerates a base URL with or without the trailing <c>/v1</c>.</summary>
    private static Uri BuildUri(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/v1";
        }

        return new Uri(trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions");
    }

    private static string? ReadContent(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
