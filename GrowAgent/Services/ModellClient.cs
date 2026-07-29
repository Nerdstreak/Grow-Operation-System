using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace GrowAgent.Services;

/// <summary>Eine Zeile im Gespräch.</summary>
public sealed record Zug(string Rolle, string Text);

/// <summary>
/// Der Anschluss an das Sprachmodell.
/// </summary>
/// <remarks>
/// <para>Drei Anbieter, weil die Entscheidung dem Betreiber gehört: Claude,
/// ein OpenAI-kompatibler Dienst, oder ein Modell auf dem eigenen Rechner über
/// Ollama. Der Schlüssel wohnt in der Konfiguration dieses Add-ons — Grow OS
/// bleibt schlüsselfrei, so war es abgesprochen.</para>
///
/// <para>Für Claude das offizielle SDK, für die beiden anderen HTTP. Gemischt
/// wird innerhalb eines Anbieters nie.</para>
/// </remarks>
public sealed class ModellClient
{
    /// <summary>
    /// Reichlich Platz für die Antwort.
    /// </summary>
    /// <remarks>
    /// Bei Claude deckt diese Grenze Denken UND Antwort ab. Zu knapp bemessen
    /// bricht die Antwort mitten im Satz ab, ohne dass ein Fehler kommt.
    /// </remarks>
    private const int MaxTokens = 16000;

    private readonly AgentEinstellungen _einstellungen;
    private readonly HttpClient _http;
    private readonly ILogger<ModellClient> _logger;

    public ModellClient(AgentEinstellungen einstellungen, HttpClient http, ILogger<ModellClient> logger)
    {
        _einstellungen = einstellungen;
        _http = http;
        _logger = logger;
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>Die Antwort des Beraters, oder ein Klartext-Hinweis, warum keine kam.</summary>
    public Task<string> AntwortAsync(string anweisung, IReadOnlyList<Zug> verlauf, CancellationToken cancellationToken)
        => _einstellungen.Anbieter.ToLowerInvariant() switch
        {
            "anthropic" => ClaudeAsync(anweisung, verlauf, cancellationToken),
            "openai" => OpenAiAsync(anweisung, verlauf, cancellationToken),
            "ollama" => OllamaAsync(anweisung, verlauf, cancellationToken),
            var anderer => Task.FromResult($"Unbekannter Anbieter „{anderer}“. Erlaubt sind: anthropic, openai, ollama."),
        };

    private async Task<string> ClaudeAsync(string anweisung, IReadOnlyList<Zug> verlauf, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_einstellungen.Schluessel))
        {
            return "Es ist kein Schlüssel hinterlegt. Trag ihn in den Einstellungen dieses Add-ons ein.";
        }

        AnthropicClient client = new() { ApiKey = _einstellungen.Schluessel };

        var antwort = await client.Messages.Create(new MessageCreateParams
        {
            Model = _einstellungen.Modell,
            MaxTokens = MaxTokens,
            System = anweisung,
            Messages = verlauf
                .Select(zug => new MessageParam
                {
                    Role = zug.Rolle == "assistant" ? Role.Assistant : Role.User,
                    Content = zug.Text,
                })
                .ToList(),
        }, cancellationToken);

        // Erst den Grund prüfen, dann den Inhalt lesen: bei einer Ablehnung ist
        // die Liste leer, und ein blinder Zugriff auf das erste Element wäre
        // ein Absturz statt einer Erklärung.
        if (antwort.StopReason == "refusal")
        {
            _logger.LogInformation("Das Modell hat die Anfrage abgelehnt.");
            return "Das Modell hat diese Anfrage abgelehnt. Formulier sie anders oder frag etwas Konkretes zur Anlage.";
        }

        var text = string.Join("\n", antwort.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text));

        return string.IsNullOrWhiteSpace(text) ? "Das Modell hat nichts zurückgegeben." : text;
    }

    private async Task<string> OpenAiAsync(string anweisung, IReadOnlyList<Zug> verlauf, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_einstellungen.Schluessel))
        {
            return "Es ist kein Schlüssel hinterlegt. Trag ihn in den Einstellungen dieses Add-ons ein.";
        }

        var basis = string.IsNullOrWhiteSpace(_einstellungen.Adresse)
            ? "https://api.openai.com"
            : _einstellungen.Adresse.TrimEnd('/');

        using var anfrage = new HttpRequestMessage(HttpMethod.Post, $"{basis}/v1/chat/completions")
        {
            Content = Json(new
            {
                model = _einstellungen.Modell,
                messages = Nachrichten(anweisung, verlauf),
                max_completion_tokens = MaxTokens,
            }),
        };
        anfrage.Headers.Add("Authorization", $"Bearer {_einstellungen.Schluessel}");

        return await TextAusAntwortAsync(anfrage, wurzel =>
            wurzel.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString(),
            cancellationToken);
    }

    private async Task<string> OllamaAsync(string anweisung, IReadOnlyList<Zug> verlauf, CancellationToken cancellationToken)
    {
        var basis = string.IsNullOrWhiteSpace(_einstellungen.Adresse)
            ? "http://homeassistant.local:11434"
            : _einstellungen.Adresse.TrimEnd('/');

        using var anfrage = new HttpRequestMessage(HttpMethod.Post, $"{basis}/api/chat")
        {
            Content = Json(new
            {
                model = _einstellungen.Modell,
                messages = Nachrichten(anweisung, verlauf),
                stream = false,
            }),
        };

        return await TextAusAntwortAsync(anfrage, wurzel =>
            wurzel.GetProperty("message").GetProperty("content").GetString(),
            cancellationToken);
    }

    private static object[] Nachrichten(string anweisung, IReadOnlyList<Zug> verlauf)
        => new object[] { new { role = "system", content = anweisung } }
            .Concat(verlauf.Select(zug => (object)new { role = zug.Rolle, content = zug.Text }))
            .ToArray();

    private static StringContent Json(object inhalt)
        => new(JsonSerializer.Serialize(inhalt), Encoding.UTF8, "application/json");

    /// <summary>
    /// Anfrage schicken und den Text herausholen — mit lesbarem Fehler statt Ausnahme.
    /// </summary>
    /// <remarks>
    /// Was hier schiefgeht, steht auf dem Bildschirm eines Menschen, der gerade
    /// eine Frage zu seinem Zelt gestellt hat. Ein Kartenstapel aus
    /// Ausnahmetexten hilft ihm nicht.
    /// </remarks>
    private async Task<string> TextAusAntwortAsync(
        HttpRequestMessage anfrage, Func<JsonElement, string?> herausholen, CancellationToken cancellationToken)
    {
        try
        {
            using var antwort = await _http.SendAsync(anfrage, cancellationToken);
            var inhalt = await antwort.Content.ReadAsStringAsync(cancellationToken);

            if (!antwort.IsSuccessStatusCode)
            {
                _logger.LogWarning("Das Modell antwortete mit {Code}: {Inhalt}", (int)antwort.StatusCode, inhalt);
                return $"Das Modell antwortete mit Fehler {(int)antwort.StatusCode}. Stimmen Schlüssel, Adresse und Modellname?";
            }

            using var json = JsonDocument.Parse(inhalt);
            return herausholen(json.RootElement) ?? "Das Modell hat nichts zurückgegeben.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Das Modell war nicht erreichbar.");
            return "Das Modell war nicht erreichbar. Läuft es, und stimmt die Adresse?";
        }
    }
}
