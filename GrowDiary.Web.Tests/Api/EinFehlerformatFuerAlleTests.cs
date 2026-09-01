using System.Reflection;
using GrowDiary.Web.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Jeder API-Endpunkt antwortet im Fehlerfall im selben Format.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026, vom Prüfer gefunden).</b> Die neue
/// Ablehnung vertauschter Grenzwerte antwortete mit
/// <c>ValidationProblem(ModelState)</c> — dem ASP.NET-Standardformat:</para>
///
/// <code>
/// {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1",
///  "title":"One or more validation errors occurred.","status":400,"errors":{…}}
/// </code>
///
/// <para>Kein <c>code</c>, kein <c>message</c>, kein <c>fieldErrors</c>, kein
/// <c>schemaVersion</c> — während jeder andere Endpunkt
/// <c>grow-os.api-error.v1</c> liefert. Die Oberfläche liest in
/// <c>api.ts</c> ausschliesslich <c>payload.message</c>; sie bekam
/// <c>undefined</c> und zeigte den englischen Rückfalltext
/// <b>„API request failed with status 400"</b>. Genau die Falle, die in
/// diesem Projekt schon einmal zugeschnappt ist.</para>
///
/// <para><b>Warum eine Zählung und nicht ein Fix.</b> Es gab bereits eine
/// Prüfung für das Fehlerformat — <c>ApiErrorContractTests</c>. Sie prüft
/// <see cref="ApiControllerBase"/> selbst, also die Basis, und hat deshalb
/// nicht gesehen, dass ein Controller sie gar nicht benutzt. Diese hier geht
/// über die <b>Grundmenge</b>: alle Api-Controller der Anwendung.</para>
/// </remarks>
public sealed class EinFehlerformatFuerAlleTests
{
    /// <summary>
    /// Die ASP.NET-Standardantworten sind für Api-Controller tabu.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> und <c>Problem</c> erzeugen beide ein
    /// <c>ProblemDetails</c> ohne die Felder, die die Oberfläche liest. Wer eine
    /// Ausnahme braucht, schreibt sie hier mit Grund hin — nicht still im
    /// Controller.
    /// </remarks>
    [Fact]
    public void KeinApiController_AntwortetImAspNetStandardformat()
    {
        var controller = ApiControllerTypen();

        // Mengenwaechter: ohne Grundmenge laeuft die Schleife null Mal und ist gruen.
        Assert.True(controller.Count >= 30,
            $"Nur {controller.Count} Api-Controller gefunden — die Grundmenge stimmt nicht, "
            + "und diese Zaehlung prueft dann nichts.");

        var verzeichnis = ControllerVerzeichnis();
        var treffer = new List<string>();

        foreach (var datei in Directory.EnumerateFiles(verzeichnis, "*.cs", SearchOption.AllDirectories))
        {
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                var zeile = zeilen[i].Trim();

                // Eine Erwaehnung ist keine Verwendung: Kommentare und
                // XML-Doku nennen die Namen, ohne sie aufzurufen.
                if (zeile.StartsWith("//", StringComparison.Ordinal)
                    || zeile.StartsWith("*", StringComparison.Ordinal)
                    || zeile.StartsWith("/*", StringComparison.Ordinal)
                    || zeile.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (zeile.Contains("return ValidationProblem(", StringComparison.Ordinal)
                    || zeile.Contains("return Problem(", StringComparison.Ordinal))
                {
                    treffer.Add($"{Path.GetFileName(datei)}:{i + 1}  {zeile}");
                }
            }
        }

        Assert.True(treffer.Count == 0,
            "Diese Stellen antworten im ASP.NET-Standardformat statt in "
            + "grow-os.api-error.v1:\n  " + string.Join("\n  ", treffer)
            + "\n\nDie Oberflaeche liest nur payload.message und zeigt sonst "
            + "„API request failed with status 400\". Richtig sind ValidationError(), "
            + "BadRequestError(), NotFoundError() aus ApiControllerBase — oder "
            + "ApiErrorFactory direkt.");
    }

    /// <summary>
    /// Wer einen Fehler beantwortet, erbt von <see cref="ApiControllerBase"/>.
    /// </summary>
    /// <remarks>
    /// <para>Zwei Ausnahmen, beide mit Grund:</para>
    /// <list type="bullet">
    ///   <item><c>AcTestApiController</c> ruft <c>ApiErrorFactory</c> direkt auf
    ///   und liefert damit dasselbe Format.</item>
    ///   <item>Controller ganz ohne Fehlerantwort brauchen die Basis nicht.</item>
    /// </list>
    /// </remarks>
    [Fact]
    public void WerFehlerBeantwortet_ErbtVonDerBasis()
    {
        var verzeichnis = ControllerVerzeichnis();
        var dateien = Directory.EnumerateFiles(verzeichnis, "*.cs", SearchOption.AllDirectories).ToList();

        Assert.True(dateien.Count >= 30,
            $"Nur {dateien.Count} Controller-Dateien gefunden — die Grundmenge stimmt nicht.");

        var offen = new List<string>();
        foreach (var datei in dateien)
        {
            var inhalt = File.ReadAllText(datei);
            if (inhalt.Contains(": ApiControllerBase", StringComparison.Ordinal)) continue;

            // Direkt ueber die Fabrik ist dasselbe Format — ausdruecklich erlaubt.
            if (inhalt.Contains("ApiErrorFactory.", StringComparison.Ordinal)) continue;

            // Ohne Fehlerantwort braucht niemand die Basis.
            var beantwortetFehler =
                inhalt.Contains("return BadRequest(", StringComparison.Ordinal)
                || inhalt.Contains("return NotFound(", StringComparison.Ordinal)
                || inhalt.Contains("return Conflict(", StringComparison.Ordinal)
                || inhalt.Contains("return ValidationProblem(", StringComparison.Ordinal);

            if (beantwortetFehler) offen.Add(Path.GetFileName(datei));
        }

        Assert.True(offen.Count == 0,
            "Diese Controller beantworten Fehler, erben aber weder von ApiControllerBase "
            + "noch rufen sie ApiErrorFactory: " + string.Join(", ", offen)
            + ". Ihre Antworten tragen dann kein code/message/schemaVersion, und die "
            + "Oberflaeche zeigt eine englische Ersatzmeldung.");
    }

    private static List<Type> ApiControllerTypen()
        => typeof(ApiControllerBase).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, Namespace: "GrowDiary.Web.Api.Controllers" })
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

    private static string ControllerVerzeichnis()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var kandidat = Path.Combine(dir, "GrowDiary.Web", "Api", "Controllers");
            if (Directory.Exists(kandidat)) return kandidat;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Controller-Verzeichnis nicht gefunden.");
    }
}
