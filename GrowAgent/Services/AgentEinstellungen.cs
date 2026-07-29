using System.Text.Json;

namespace GrowAgent.Services;

/// <summary>
/// Was der Betreiber in den Add-on-Einstellungen eingetragen hat.
/// </summary>
/// <remarks>
/// Home Assistant legt die Optionen als <c>/data/options.json</c> ab. Beim
/// Entwickeln gibt es die Datei nicht — dann greifen Umgebungsvariablen, damit
/// sich das Ganze auch ohne Home Assistant starten lässt.
/// </remarks>
public sealed class AgentEinstellungen
{
    /// <summary>anthropic, openai oder ollama.</summary>
    public string Anbieter { get; init; } = "anthropic";

    /// <summary>Der Modellname beim gewählten Anbieter.</summary>
    public string Modell { get; init; } = "claude-opus-5";

    /// <summary>Der Schlüssel. Bei Ollama leer.</summary>
    public string Schluessel { get; init; } = string.Empty;

    /// <summary>Abweichende Adresse — für Ollama oder einen eigenen Dienst.</summary>
    public string Adresse { get; init; } = string.Empty;

    /// <summary>
    /// Grow OS von Hand eintragen, falls der Supervisor es nicht herausrückt.
    /// </summary>
    /// <remarks>
    /// Normalerweise fragt der Berater den Supervisor nach dem Hostnamen — der
    /// enthält einen Hash und lässt sich nicht raten. Welche Berechtigungsrolle
    /// diese Auskunft verlangt, ist nicht dokumentiert; wird sie verweigert,
    /// trägt der Betreiber hier ein, was in der Add-on-Übersicht steht (etwa
    /// <c>http://a1b2c3d4-grow-os:5076</c>).
    /// </remarks>
    public string GrowOsAdresse { get; init; } = string.Empty;

    /// <summary>Aus der Add-on-Konfiguration lesen, sonst aus der Umgebung.</summary>
    public static AgentEinstellungen Laden(ILogger logger)
    {
        const string pfad = "/data/options.json";
        if (File.Exists(pfad))
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(pfad));
                var wurzel = json.RootElement;
                return new AgentEinstellungen
                {
                    Anbieter = Text(wurzel, "anbieter", "anthropic"),
                    Modell = Text(wurzel, "modell", "claude-opus-5"),
                    Schluessel = Text(wurzel, "schluessel", string.Empty),
                    Adresse = Text(wurzel, "adresse", string.Empty),
                    GrowOsAdresse = Text(wurzel, "grow_os_adresse", string.Empty),
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Die Add-on-Einstellungen liessen sich nicht lesen — es gelten die Vorgaben.");
            }
        }

        return new AgentEinstellungen
        {
            Anbieter = Umgebung("GROW_AGENT_ANBIETER", "anthropic"),
            Modell = Umgebung("GROW_AGENT_MODELL", "claude-opus-5"),
            Schluessel = Umgebung("GROW_AGENT_SCHLUESSEL", string.Empty),
            Adresse = Umgebung("GROW_AGENT_ADRESSE", string.Empty),
            GrowOsAdresse = Umgebung("GROW_AGENT_GROW_OS", string.Empty),
        };
    }

    private static string Text(JsonElement element, string name, string vorgabe)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String
            ? wert.GetString() ?? vorgabe
            : vorgabe;

    private static string Umgebung(string name, string vorgabe)
    {
        var wert = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(wert) ? vorgabe : wert;
    }
}
