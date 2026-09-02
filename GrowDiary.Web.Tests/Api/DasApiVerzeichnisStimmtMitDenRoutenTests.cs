using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Das API-Verzeichnis nennt nur Endpunkte, die es wirklich gibt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Nach dem Löschen der drei
/// Legacy-Kamera-Routen warb <c>GET /api/system/api-manifest</c> weiter für
/// sie. Wer das Verzeichnis liest — und dafür ist es da —, bekam drei
/// Endpunkte genannt, die 404 antworten.</para>
///
/// <para><b>Warum das nicht auffällt.</b> Dieselbe Datei ist aus der Zählung
/// „jede Route hat einen Aufrufer" <i>ausgeschlossen</i>: ein Katalog ist kein
/// Aufrufer. Ausgeschlossen, aber nicht mitgezogen — genau die Lücke, die
/// entsteht, wenn man etwas aus einer Prüfung herausnimmt, ohne ihm eine
/// eigene zu geben.</para>
///
/// <para>Das Verzeichnis ist Dokumentation, und Dokumentation driftet. Diese
/// Zählung bindet sie an die Routentabelle.</para>
/// </remarks>
public sealed class DasApiVerzeichnisStimmtMitDenRoutenTests
{
    /// <summary>Ein Eintrag im Verzeichnis, so wie er im Quelltext steht.</summary>
    private static readonly Regex EINTRAG = new(
        @"Endpoint\(\s*""(?<verb>[A-Z]+)""\s*,\s*""(?<pfad>[^""]+)""",
        RegexOptions.Compiled);

    [Fact]
    public void JederEintragNenntEineEchteRoute()
    {
        var eintraege = VerzeichnisEintraege();
        var routen = AlleRouten();

        // Zwei Mengenwaechter: ohne beide Seiten prueft der Vergleich nichts.
        Assert.True(eintraege.Count >= 40,
            $"Nur {eintraege.Count} Verzeichnis-Eintraege gefunden — heisst der Aufruf nicht "
            + "mehr `Endpoint(\"GET\", \"/…\")`? Dann prueft diese Zaehlung nichts.");
        Assert.True(routen.Count >= 100,
            $"Nur {routen.Count} Routen gefunden — die Zaehlung sieht ihre Grundmenge nicht.");

        var erfunden = eintraege.Where(e => !routen.Contains(e)).ToList();

        Assert.True(erfunden.Count == 0,
            "Das API-Verzeichnis nennt Endpunkte, die es nicht gibt:\n  "
            + string.Join("\n  ", erfunden.Order())
            + "\n\nWer das Verzeichnis liest — und dafuer ist es da —, bekommt eine 404-Adresse "
            + "genannt. Entweder den Eintrag streichen oder die Route wieder anlegen.");
    }

    /// <summary>Der Selbsttest: trifft das Muster die belegte Form?</summary>
    [Theory]
    [InlineData("Endpoint(\"GET\", \"/tents/{id}/camera.jpg\", \"Legacy.\", true),", "GET /tents/{id}/camera.jpg")]
    [InlineData("  Endpoint( \"POST\" , \"/api/x/y\" , \"Text\" ),", "POST /api/x/y")]
    [InlineData("var x = Endpoint(kind, path);", null)]
    public void DasMusterTrifftDieBelegteForm(string zeile, string? erwartet)
    {
        var treffer = EINTRAG.Match(zeile);

        if (erwartet is null)
        {
            Assert.False(treffer.Success, $"<{zeile}> sollte nicht treffen.");
            return;
        }

        Assert.True(treffer.Success, $"<{zeile}> wurde nicht getroffen.");
        Assert.Equal(erwartet, $"{treffer.Groups["verb"].Value} {treffer.Groups["pfad"].Value}");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>
    /// Was das Verzeichnis nennt, auf die Schreibweise der Routentabelle gebracht.
    /// </summary>
    /// <remarks>
    /// Das Verzeichnis schreibt <c>{id}</c>, die Routentabelle
    /// <c>{id:int}</c> — verglichen wird ohne die Typangabe, denn sie ist eine
    /// Frage der Route und nicht der Adresse.
    /// </remarks>
    private static HashSet<string> VerzeichnisEintraege()
    {
        var datei = Path.Combine(ProjektWurzel(), "GrowDiary.Web", "Api", "Controllers",
            "SystemApiController.StatusEndpoints.cs");
        var raus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var zeile in File.ReadAllLines(datei))
        {
            // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
            var treffer = EINTRAG.Match(zeile.Split("//")[0]);
            if (treffer.Success)
            {
                raus.Add($"{treffer.Groups["verb"].Value} {OhneTyp(treffer.Groups["pfad"].Value)}");
            }
        }

        return raus;
    }

    private static HashSet<string> AlleRouten()
    {
        var raus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(GrowDiary.Web.Services.Bauzeit).Assembly;

        foreach (var typ in assembly.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var amTyp = typ.GetCustomAttribute<RouteAttribute>()?.Template?.Trim('/') ?? string.Empty;

            foreach (var methode in typ.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var attribut in methode.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var roh = attribut.Template ?? string.Empty;
                    var vorlage = roh.StartsWith('/') || roh.StartsWith("~/")
                        ? roh.TrimStart('~').Trim('/')
                        : string.Join("/", new[] { amTyp, roh.Trim('/') }.Where(t => t.Length > 0));

                    if (vorlage.Length == 0) continue;

                    var verb = attribut.HttpMethods.FirstOrDefault() ?? "GET";
                    raus.Add($"{verb} /{OhneTyp(vorlage)}");
                }
            }
        }

        return raus;
    }

    /// <summary>
    /// „{id:int}" wird „{id}", und ein Abfrageteil fällt weg.
    /// </summary>
    /// <remarks>
    /// Das Verzeichnis schreibt manche Einträge mit Beispiel-Abfrage
    /// (<c>/api/hydro-setups?tentId={id}</c>), weil das dem Leser hilft. Zur
    /// <i>Route</i> gehört der Abfrageteil nicht — sonst meldete diese Zählung
    /// einen lebenden Endpunkt als erfunden.
    /// </remarks>
    private static string OhneTyp(string pfad)
    {
        var ohneAbfrage = pfad.Split('?')[0];
        return Regex.Replace(ohneAbfrage, @"\{(\w+):[^}]+\}", "{$1}");
    }

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
