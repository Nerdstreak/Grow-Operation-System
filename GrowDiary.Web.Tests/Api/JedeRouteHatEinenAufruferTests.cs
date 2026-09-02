using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Jeder Endpunkt wird von jemandem gerufen — oder hat einen ausgeschriebenen Grund.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Beim Durchgehen der ungeprüften Klassen
/// kamen zwei Controller heraus, die <b>erreichbar</b> sind und die
/// <b>niemand</b> ruft:</para>
///
/// <list type="bullet">
///   <item><c>GET /api/system/network</c> gibt die privaten LAN-Adressen der
///   Maschine heraus — nachgeprüft an der laufenden App, HTTP 200 mit der
///   echten Adresse darin. Sein Nachfolger <c>GET /api/system/mobile-access</c>
///   wird von <c>MobilePage.tsx</c> benutzt; dieser hier von nichts.</item>
///   <item><c>GET /api/live/home</c> rechnet in einer Schleife über alle Zelte
///   und fragt dabei <b>Home Assistant an der echten Anlage</b> ab. Die
///   Live-Seite holt in Wahrheit je Zelt einzeln.</item>
/// </list>
///
/// <para><b>Warum eine Zählung und nicht zwei Löschungen.</b> Beide sind nicht
/// aus Nachlässigkeit entstanden, sondern beim Umbau von MVC auf React liegen
/// geblieben — und beim nächsten Umbau bleibt wieder etwas liegen. Ein
/// Endpunkt ohne Aufrufer ist nicht harmlos: er ist Angriffsfläche, er hält
/// eine zweite Wahrheit am Leben, und er kostet beim Lesen Zeit.</para>
///
/// <para><b>Als Aufrufer zählt</b>, wer die Route wirklich anspricht: die
/// React-Oberfläche, die Playwright-Mappe, das MCP-Add-on und
/// <c>GrowOsAccess</c>. Ein Vorkommen in einem <i>Kommentar</i> oder in der
/// Doku zählt nicht — eine Erwähnung ist keine Verwendung.</para>
/// </remarks>
public sealed class JedeRouteHatEinenAufruferTests
{
    /// <summary>
    /// Endpunkte ohne Aufrufer — jeder mit ausgeschriebenem Grund.
    /// </summary>
    /// <remarks>
    /// Der Schlüssel ist <c>METHODE /pfad/mit/{platzhaltern}</c>, genau wie ihn
    /// die Meldung unten ausgibt.
    /// </remarks>
    /// <remarks>
    /// <para>Heute steht hier <b>nichts</b> — und das ist die Aussage. Am
    /// 02.09.2026 fand diese Zählung elf Endpunkte ohne Aufrufer; alle elf sind
    /// gelöscht, keiner brauchte eine Ausnahme.</para>
    ///
    /// <para>Der Fehlerbehandler <c>/api/error</c> steht nicht hier, weil er
    /// einen echten Aufrufer hat: <c>app.UseExceptionHandler("/api/error")</c>
    /// in <c>Program.cs</c>. Genau dafür zählt <c>GrowDiary.Web</c> selbst als
    /// Aufrufer-Ort.</para>
    /// </remarks>
    private static readonly Dictionary<string, string> OHNE_AUFRUFER = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Wo ein Aufruf stehen darf.</summary>
    /// <remarks>
    /// <para><b>Warum <c>GrowDiary.Web</c> mit dabei ist.</b> Manche Adressen
    /// baut das Backend selbst und reicht sie als Link weiter — die Sicherung
    /// etwa: <c>$"/api/system/backup/{Uri.EscapeDataString(fileName)}"</c> geht
    /// als <c>safetyBackupDownloadUrl</c> an <c>ReleasePage.tsx</c>, die daraus
    /// ein <c>&lt;a href&gt;</c> macht. Ohne diese Quelle meldete die Zählung
    /// den Download als tot, obwohl der Knopf da ist.</para>
    ///
    /// <para><b>Aber die Route belegt sich nicht selbst.</b> Attribut-Zeilen
    /// (<c>[HttpGet("…")]</c>, <c>[Route("…")]</c>) werden vorher entfernt.
    /// Genau diese Falle ist <c>routes-reachable</c> schon einmal
    /// zugeschnappt: eine erfundene Route belegte sich durch ihre eigene
    /// Deklaration.</para>
    /// </remarks>
    private static readonly string[] AUFRUFER_ORTE =
    [
        Path.Combine("GrowDiary.React", "src"),
        Path.Combine("GrowDiary.React", "e2e"),
        "GrowOsAccess",
        "GrowMcp",
        "GrowDiary.Web",
    ];

    [Fact]
    public void JedeRouteWirdVonJemandemGerufen()
    {
        var routen = AlleRouten().ToList();

        // Mengenwaechter: ohne Routen prueft der Rest nichts.
        Assert.True(routen.Count >= 100,
            $"Nur {routen.Count} Routen gefunden — die Zaehlung sieht ihre Grundmenge nicht "
            + "und waere auch bei jedem toten Endpunkt gruen.");

        var quelltext = AufruferQuelltext();
        Assert.True(quelltext.Length > 100_000,
            $"Nur {quelltext.Length} Zeichen Aufrufer-Quelltext gelesen — dann findet die "
            + "Zaehlung nichts und meldet ALLES als tot.");

        var verwaist = new List<string>();
        foreach (var (schluessel, muster) in routen)
        {
            if (OHNE_AUFRUFER.ContainsKey(schluessel)) continue;
            if (muster.IsMatch(quelltext)) continue;
            verwaist.Add(schluessel);
        }

        Assert.True(verwaist.Count == 0,
            "Diese Endpunkte ruft niemand:\n  " + string.Join("\n  ", verwaist.Order())
            + "\n\nEin Endpunkt ohne Aufrufer ist nicht harmlos: er ist Angriffsflaeche, er "
            + "haelt eine zweite Wahrheit am Leben, und er kostet beim Lesen Zeit. Entweder "
            + "loeschen oder mit ausgeschriebenem Grund in OHNE_AUFRUFER eintragen.");
    }

    /// <summary>Jeder eingetragene Grund gilt einer Route, die es wirklich gibt.</summary>
    /// <remarks>
    /// Ein Tippfehler im Schlüssel machte die Ausnahme wirkungslos — und die
    /// Zählung meldete den Endpunkt weiter, bis jemand die Ausnahme „repariert",
    /// indem er den Endpunkt einträgt, den er gerade sieht.
    /// </remarks>
    [Fact]
    public void JedeAusnahmeGiltEinerEchtenRoute()
    {
        var vorhanden = AlleRouten().Select(r => r.Schluessel).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var erfunden = OHNE_AUFRUFER.Keys.Where(k => !vorhanden.Contains(k)).ToList();

        Assert.True(erfunden.Count == 0,
            "Diese Ausnahmen nennen eine Route, die es nicht gibt: " + string.Join(", ", erfunden)
            + ". Eine Ausnahme mit Tippfehler ist wirkungslos.");
    }

    /// <summary>Der Selbsttest: findet das Muster einen echten Aufruf?</summary>
    /// <remarks>
    /// Eine Zählung mit kaputtem Muster meldet <i>alles</i> als tot oder
    /// <i>nichts</i> — beides unbrauchbar. Hier steht ausgeschrieben, welche
    /// Schreibweisen der Oberfläche getroffen werden müssen.
    /// </remarks>
    [Theory]
    // So schreibt die Oberflaeche wirklich (woertlich aus dem Quelltext).
    [InlineData("api/grows/{growId:int}/tasks", "apiFetch(`/api/grows/${grow.id}/tasks`)", true)]
    [InlineData("api/hardware-items", "apiFetch<HardwareItem[]>('/api/hardware-items')", true)]
    [InlineData("api/hardware-items/{id:int}", "api.delete(`/api/hardware-items/${id}`)", true)]
    [InlineData("api/system/mobile-access", "apiFetch('/api/system/mobile-access')", true)]
    // Und was NICHT zaehlen darf: ein anderer Pfad, der zufaellig so anfaengt.
    [InlineData("api/live/home", "apiFetch('/api/live/homepage-alt')", false)]
    [InlineData("api/system/network", "apiFetch('/api/system/network-status')", false)]
    public void DasMusterTrifftEchteAufrufe(string vorlage, string quelle, bool erwartet)
    {
        var muster = MusterFuer(vorlage);

        Assert.True(muster.IsMatch(quelle) == erwartet,
            $"Das Muster fuer „{vorlage}\" sagt zu <{quelle}> das Gegenteil von dem, was es "
            + "soll. Eine Zaehlung mit kaputtem Muster meldet alles als tot oder nichts.");
    }

    /// <summary>
    /// Ein Vorkommen im <b>Kommentar</b> zählt nicht als Aufruf.
    /// </summary>
    /// <remarks>
    /// „Eine Erwähnung ist keine Verwendung" (<c>CLAUDE.md</c>). Ein
    /// auskommentierter Aufruf oder ein Hinweis „früher lag das unter …" hielte
    /// den toten Endpunkt sonst am Leben — und die Zählung wäre grün, während
    /// niemand den Endpunkt ruft.
    /// </remarks>
    [Theory]
    [InlineData("// frueher: apiFetch('/api/system/network')", "")]
    [InlineData("const a = 1 // siehe /api/live/home", "const a = 1 ")]
    [InlineData("/* apiFetch('/api/live/home') */", " ")]
    [InlineData("apiFetch('/api/live/home') // echt", "apiFetch('/api/live/home') ")]
    public void EinKommentarZaehltNichtAlsAufruf(string quelle, string erwartet)
    {
        Assert.True(OhneKommentare(quelle).TrimEnd() == erwartet.TrimEnd(),
            $"Aus <{quelle}> wurde <{OhneKommentare(quelle)}>, erwartet war <{erwartet}>. "
            + "Ein auskommentierter Aufruf haelt sonst einen toten Endpunkt am Leben.");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>
    /// Ein Muster, das die Route in der Schreibweise der Aufrufer trifft.
    /// </summary>
    /// <remarks>
    /// <para><b>Zwei Wege, denn die Oberfläche schreibt auf zwei Arten.</b></para>
    ///
    /// <para>Meistens steht der Pfad am Stück da:
    /// <c>apiFetch(`/api/grows/${id}/tasks`)</c>. Platzhalter werden zu
    /// „alles ausser Trennzeichen".</para>
    ///
    /// <para>Manchmal wird er aber <b>zur Laufzeit zusammengesetzt</b>:
    /// <c>const route = … : 'confirm-finish'</c> und erst danach
    /// <c>`/api/grows/${growId}/actions/${route}`</c>. Ein reiner
    /// Pfadvergleich meldete diese fünf Endpunkte als tot, obwohl der Knopf
    /// dafür auf jeder Grow-Seite steht. Deshalb zählt zusätzlich der
    /// <b>kennzeichnende Namensteil</b> — das letzte feste Stück der Route,
    /// wenn es unverwechselbar genug ist (mit Bindestrich oder länger als
    /// sieben Zeichen). „id" oder „new" zählen nicht.</para>
    /// </remarks>
    private static Regex MusterFuer(string vorlage)
    {
        var teile = vorlage.Trim('/').Split('/');

        // Ein Platzhalter steht fuer alles ausser Trennzeichen: die Oberflaeche
        // schreibt dort `${grow.id}` — kein Schraegstrich, keine Anfuehrung.
        const string platzhalter = @"[^/'""`\s]+";

        var pfad = string.Join("/", teile.Select(teil =>
            teil.StartsWith('{') ? platzhalter : Regex.Escape(teil)));

        var muster = "/" + pfad + @"(?![\w-])";

        var letztesFeste = teile.LastOrDefault(teil => !teil.StartsWith('{'));
        if (letztesFeste is not null
            && (letztesFeste.Contains('-') || letztesFeste.Length > 7))
        {
            muster += "|" + Regex.Escape(letztesFeste) + @"(?![\w-])";
        }

        return new Regex(muster, RegexOptions.Compiled);
    }

    private static IEnumerable<(string Schluessel, Regex Muster)> AlleRouten()
    {
        var assembly = typeof(GrowDiary.Web.Services.Bauzeit).Assembly;

        foreach (var typ in assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var amTyp = typ.GetCustomAttribute<RouteAttribute>()?.Template?.Trim('/') ?? string.Empty;

            foreach (var methode in typ.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var attribut in methode.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var eigene = attribut.Template?.Trim('/') ?? string.Empty;

                    /* Eine Vorlage mit "/" oder "~/" am Anfang ersetzt die Route
                       am Typ. Ohne den Tilde-Fall stand in der ersten Fassung
                       "GET /api/companion/~/api/grows/{growId:int}/due-sops" in
                       der Meldung — eine Route, die es so nie gab. */
                    var roh = attribut.Template ?? string.Empty;
                    var ersetztTyp = roh.StartsWith('/') || roh.StartsWith("~/");
                    var vorlage = ersetztTyp
                        ? roh.TrimStart('~').Trim('/')
                        : string.Join("/", new[] { amTyp, eigene }.Where(t => t.Length > 0));

                    if (vorlage.Length == 0) continue;

                    var verb = attribut.HttpMethods.FirstOrDefault() ?? "GET";
                    yield return ($"{verb} /{vorlage}", MusterFuer(vorlage));
                }
            }
        }
    }

    /// <summary>Der Quelltext aller Stellen, an denen ein Aufruf stehen darf.</summary>
    private static string AufruferQuelltext()
    {
        var wurzel = ProjektWurzel();
        var teile = new List<string>();

        foreach (var ort in AUFRUFER_ORTE)
        {
            var pfad = Path.Combine(wurzel, ort);
            if (!Directory.Exists(pfad)) continue;

            foreach (var datei in Directory.EnumerateFiles(pfad, "*.*", SearchOption.AllDirectories))
            {
                var endung = Path.GetExtension(datei);
                if (endung is not (".ts" or ".tsx" or ".cs" or ".json")) continue;
                if (datei.Contains("node_modules") || datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

                var inhalt = OhneKommentare(File.ReadAllText(datei));
                if (endung == ".cs") inhalt = OhneRoutenAttribute(inhalt);
                teile.Add(inhalt);
            }
        }

        return string.Join("\n", teile);
    }

    /// <summary>Quelltext ohne Kommentare — eine Erwähnung ist keine Verwendung.</summary>
    /// <remarks>
    /// <para>Ein auskommentierter Aufruf oder ein Hinweis „früher lag das unter
    /// …" hielte einen toten Endpunkt sonst am Leben, und die Zählung wäre
    /// grün, während ihn niemand ruft.</para>
    ///
    /// <para><b>Absichtlich grob.</b> Ein <c>//</c> in einer Zeichenkette
    /// (<c>"https://…"</c>) schneidet hier zu viel weg. Das ist die
    /// ungefährliche Richtung: es kann einen Aufruf übersehen und einen
    /// Endpunkt fälschlich als tot melden — dann sieht ein Mensch hin. Die
    /// andere Richtung wäre still.</para>
    /// </remarks>
    private static string OhneKommentare(string quelle)
    {
        var ohneBloecke = Regex.Replace(quelle, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(ohneBloecke, @"//[^\n]*", string.Empty);
    }

    /// <summary>
    /// Quelltext ohne Routen-Attribute — <b>eine Route belegt sich nicht selbst.</b>
    /// </summary>
    /// <remarks>
    /// Genau diese Falle ist <c>routes-reachable</c> schon einmal zugeschnappt:
    /// die Suche las die Datei mit, in der die Routen stehen, und eine
    /// erfundene Route belegte sich dadurch selbst.
    /// </remarks>
    private static string OhneRoutenAttribute(string quelle)
        => Regex.Replace(quelle, @"\[\s*(Http(Get|Post|Put|Delete|Patch|Head)|Route)\s*\([^\]]*\]",
            string.Empty, RegexOptions.Singleline);

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
