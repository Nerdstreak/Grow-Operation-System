using System.Text.Json;
using System.Text.RegularExpressions;
using GrowDiary.Web.Api.Contracts;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Die Feldnamen, die der MCP-Server aus einem Grow liest, gibt es wirklich.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Das Werkzeug <c>sorte</c> gab einer KI
/// genau EINE Sorte je Grow zurück, obwohl ein Grow N Sorten führen kann. Der
/// Fix liest dafür das neue Feld <c>pflanzenSorten</c> aus dem Grow-DTO.</para>
///
/// <para><b>Warum das eine eigene Prüfung braucht.</b> Der MCP-Server liest
/// Feldnamen als <b>Zeichenketten</b> aus JSON — kein Compiler verbindet sie
/// mit dem DTO. Wird ein Feld umbenannt, findet <c>Texte(grow, "…")</c> nichts,
/// gibt eine leere Liste zurück, und das Werkzeug fällt <b>still</b> auf sein
/// altes Verhalten zurück: eine Sorte. Nichts bricht, niemand merkt es, und die
/// KI berät wieder über ein Becken, dessen halber Inhalt ihr unbekannt ist.</para>
///
/// <para>Eine Halbwahrheit ist hier schlimmer als ein Fehler — sie sieht aus
/// wie eine Antwort.</para>
/// </remarks>
public sealed class McpFeldnamenTests
{
    /// <summary>
    /// Wie ASP.NET das DTO ausliefert — camelCase, wie die laufende App es tut.
    /// </summary>
    private static readonly JsonSerializerOptions WieDieApi =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Findet <c>Text(grow, "x")</c>, <c>Zahl(grow, "x")</c> und
    /// <c>Texte(grow, "x")</c> — die drei Leser des MCP-Servers.
    /// </summary>
    private static readonly Regex Feldzugriff =
        new(@"\b(?:Text|Zahl|Texte)\s*\(\s*grow\s*,\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

    private static readonly Regex Blockkommentar = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Zeilenkommentar = new(@"//.*?$", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void JedesFeld_DasMcpAusEinemGrowLiest_GibtEsImDto()
    {
        var quelle = McpQuelle();
        var code = Zeilenkommentar.Replace(Blockkommentar.Replace(quelle, string.Empty), string.Empty);

        var gelesen = Feldzugriff.Matches(code)
            .Select(treffer => treffer.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Mengenwächter: findet der Suchausdruck überhaupt Zugriffe? Ohne ihn
        // wäre eine kaputte Regex nicht von „alles sauber" zu unterscheiden.
        Assert.True(gelesen.Count >= 3,
            $"Nur {gelesen.Count} Feldzugriffe in GrowTools.cs gefunden — der Suchausdruck "
            + "greift nicht mehr. Er ist damit blind, nicht zufrieden.");

        var vorhanden = FelderDesGrowDetails();

        var fehlend = gelesen.Where(name => !vorhanden.Contains(name)).ToList();

        Assert.True(fehlend.Count == 0,
            "Der MCP-Server liest Felder aus einem Grow, die das DTO nicht liefert:\n  "
            + string.Join("\n  ", fehlend)
            + "\n\nEr bekommt dafür null bzw. eine leere Liste — und fällt STILL auf sein "
            + "altes Verhalten zurück. Vorhanden sind:\n  "
            + string.Join(", ", vorhanden.OrderBy(n => n, StringComparer.Ordinal)));
    }

    /// <summary>
    /// Und das Feld für die Sorten ist wirklich dabei — sonst prüfte der Fall
    /// darüber nur, dass niemand etwas Falsches liest, nicht dass jemand das
    /// Richtige liest.
    /// </summary>
    [Fact]
    public void DasSortenFeld_WirdWirklichGelesen()
    {
        var code = McpQuelle();

        Assert.Contains("pflanzenSorten", code, StringComparison.Ordinal);
        Assert.Contains("pflanzenSorten", FelderDesGrowDetails());
    }

    /// <summary>Die Feldnamen, die <c>GET /api/grows/{id}</c> wirklich ausliefert.</summary>
    private static HashSet<string> FelderDesGrowDetails()
    {
        // Aus einer echten Instanz serialisieren statt die Namen abzuschreiben:
        // die Umwandlung nach camelCase macht der Serializer, nicht ich.
        var leer = (GrowDetailDto)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(GrowDetailDto));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(leer, WieDieApi));
        return json.RootElement.EnumerateObject()
            .Select(feld => feld.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string McpQuelle()
    {
        var pfad = Path.Combine(ProjektWurzel(), "GrowMcp", "Tools", "GrowTools.cs");
        Assert.True(File.Exists(pfad), $"GrowTools.cs nicht gefunden unter {pfad}.");
        return File.ReadAllText(pfad);
    }

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowMcp"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
