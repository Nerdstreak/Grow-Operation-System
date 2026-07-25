using System.Text.Json;

namespace GrowDiary.Web.Services.Ai;

/// <summary>
/// Reads the model's reply and checks its citations against what was actually sent.
///
/// This is where "the AI should use our knowledge" stops being a hope. Every claim names an
/// id; an id we never handed over cannot have been read anywhere, so the claim is marked
/// ungrounded and shown as the model's own opinion. Nothing has to be trusted.
/// </summary>
public static class AiAnswerParser
{
    public static AiAnswer Parse(string raw, AiContext context)
    {
        var json = ExtractJson(raw);
        if (json is null)
        {
            // Not JSON at all — keep the text rather than lose the answer, but it carries
            // no citations, so nothing about it is presented as sourced.
            return new AiAnswer { Summary = raw.Trim() };
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var claims = new List<AiClaim>();
            if (root.TryGetProperty("aussagen", out var statements) && statements.ValueKind == JsonValueKind.Array)
            {
                foreach (var statement in statements.EnumerateArray())
                {
                    var text = Text(statement, "text");
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    var sourceId = Text(statement, "quelle");
                    var item = sourceId is null
                        ? null
                        : context.Knowledge.FirstOrDefault(entry =>
                            string.Equals(entry.Id, sourceId, StringComparison.OrdinalIgnoreCase));

                    claims.Add(new AiClaim(
                        Text: text!,
                        SourceId: sourceId,
                        Grounded: item is not null,
                        SourceTitle: item?.Title,
                        SourceUrl: item?.SourceUrl));
                }
            }

            var unanswered = Text(root, "offen");
            return new AiAnswer
            {
                Summary = Text(root, "antwort") ?? string.Empty,
                Claims = claims,
                Unanswered = string.IsNullOrWhiteSpace(unanswered) ? null : unanswered,
            };
        }
        catch (JsonException)
        {
            return new AiAnswer { Summary = raw.Trim() };
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    /// <summary>
    /// Models like to wrap JSON in prose or a ```json fence even when told not to, so the
    /// object is cut out rather than demanded.
    /// </summary>
    private static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }
}
