using System.Text.Json;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services.Ai;

/// <summary>The wire form of one request, kept separate from sending it so it can be asserted.</summary>
public sealed record AiRequestShape(Uri Uri, string Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Builds the provider-specific request. The two dialects differ in more than a URL — auth
/// header, where the system prompt lives, and the shape of the reply — so each is written
/// out rather than squeezed through one shared shape.
/// </summary>
public static class AiRequestFactory
{
    private const int MaxTokens = 1500;

    public static AiRequestShape Build(AiSettings settings, string systemMessage, string userMessage) =>
        settings.Provider == AiProvider.Anthropic
            ? BuildAnthropic(settings, systemMessage, userMessage)
            : BuildOpenAi(settings, systemMessage, userMessage);

    private static AiRequestShape BuildOpenAi(AiSettings settings, string systemMessage, string userMessage)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = settings.Model,
            temperature = 0.2,
            max_tokens = MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = systemMessage },
                new { role = "user", content = userMessage },
            },
        });

        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            headers["Authorization"] = $"Bearer {settings.ApiKey}";
        }

        return new AiRequestShape(OpenAiUri(settings.BaseUrl!), body, headers);
    }

    private static AiRequestShape BuildAnthropic(AiSettings settings, string systemMessage, string userMessage)
    {
        // The system prompt is a top-level field here, not a message with role "system".
        var body = JsonSerializer.Serialize(new
        {
            model = settings.Model,
            max_tokens = MaxTokens,
            temperature = 0.2,
            system = systemMessage,
            messages = new object[] { new { role = "user", content = userMessage } },
        });

        var headers = new Dictionary<string, string> { ["anthropic-version"] = "2023-06-01" };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            headers["x-api-key"] = settings.ApiKey!;
        }

        return new AiRequestShape(AnthropicUri(settings.BaseUrl), body, headers);
    }

    /// <summary>Tolerates a base URL with or without <c>/v1</c> and with or without the path.</summary>
    public static Uri OpenAiUri(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed);
        }

        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/v1";
        }

        return new Uri($"{trimmed}/chat/completions");
    }

    public static Uri AnthropicUri(string? baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.anthropic.com/v1"
            : baseUrl.TrimEnd('/');

        if (trimmed.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed);
        }

        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += "/v1";
        }

        return new Uri($"{trimmed}/messages");
    }

    /// <summary>Pulls the answer text out of whichever reply shape came back.</summary>
    public static string? ReadContent(AiProvider provider, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (provider == AiProvider.Anthropic)
            {
                // content is a list of blocks; the text ones are what we want.
                var blocks = root.GetProperty("content");
                foreach (var block in blocks.EnumerateArray())
                {
                    if (block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        return text.GetString();
                    }
                }

                return null;
            }

            return root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return null;
        }
    }
}
