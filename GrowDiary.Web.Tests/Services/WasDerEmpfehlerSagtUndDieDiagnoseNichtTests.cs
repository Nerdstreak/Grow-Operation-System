using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Was der <b>unsichtbare</b> Empfehler sagt — und die sichtbare Diagnose nicht.
/// </summary>
/// <remarks>
/// <para><b>Warum es diese Prüfung gibt (02.09.2026).</b>
/// <c>RecommendationEngine</c> hat 852 Zeilen und über fünfzig
/// Empfehlungstexte. Seine Texte werden gerechnet und weggeworfen; seine
/// Schwere landet in <c>stateTone</c>, das keine Seite liest
/// (<see cref="ZweiAmpelnFuerDasselbeZeltTests"/>).</para>
///
/// <para>Bevor man über 852 Zeilen entscheidet, gehört die Frage beantwortet:
/// <b>sagt er etwas, das die sichtbare Diagnose nicht sagt?</b> Die Diagnose
/// (<c>DeviationAnalyzerService</c>, auf <c>/grows/{id}</c> sichtbar) deckt
/// dieselben Messgrössen ab — pH, EC, Wassertemperatur, Sauerstoff.</para>
///
/// <para>Diese Prüfung stellt beide vor denselben Fall und hält fest, was nur
/// der Empfehler findet. <b>Sie urteilt nicht</b>: sie macht die Antwort
/// sichtbar, damit die Entscheidung auf Zahlen steht und nicht auf einem
/// Gefühl.</para>
/// </remarks>
public sealed class WasDerEmpfehlerSagtUndDieDiagnoseNichtTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;

    public WasDerEmpfehlerSagtUndDieDiagnoseNichtTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Empfehler_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        KopiereWissen();
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>
    /// Der Empfehler findet bei einem gesunden Becken nichts Zusätzliches.
    /// </summary>
    /// <remarks>
    /// Der Mengenwächter für den Fall darunter: wären beide immer leer, sagte
    /// der Vergleich nichts.
    /// </remarks>
    [Fact]
    public void BeiEinemGesundenBecken_SagtKeinerEtwasSchweres()
    {
        var (karten, _) = Beurteilen(ph: 5.9, ec: 1.1, wasser: 20.5, sauerstoff: 8.2);

        var schwer = karten.Where(k => k.Severity is Kartenschwere.Gefahr or Kartenschwere.Warnung).ToList();

        Assert.True(schwer.Count == 0,
            "Bei einem Becken in allen Zielbaendern meldet der Empfehler trotzdem etwas "
            + "Schweres: " + string.Join(" | ", schwer.Select(k => k.Title)));
    }

    /// <summary>
    /// Bei einem kranken Becken sagen beide etwas — und zwar zu denselben Messgrössen.
    /// </summary>
    /// <remarks>
    /// <para>Das ist die eigentliche Auskunft. Findet der Empfehler eine
    /// Messgrösse, die die Diagnose übergeht, ist sein Verschwinden ein
    /// Verlust. Deckt er nur ab, was ohnehin sichtbar ist, dann nicht.</para>
    ///
    /// <para>Verglichen werden <b>Messgrössen</b>, nicht Wortlaute: dass zwei
    /// Systeme denselben Sachverhalt verschieden formulieren, ist zu erwarten
    /// und sagt nichts.</para>
    /// </remarks>
    [Fact]
    public void BeiEinemKrankenBecken_DecktDieDiagnoseDieselbenMessgroessenAb()
    {
        // pH weit oben, EC weit unten, Wasser zu warm, Sauerstoff zu niedrig.
        var (karten, abweichungen) = Beurteilen(ph: 7.4, ec: 0.3, wasser: 27.5, sauerstoff: 4.0);

        // Mengenwaechter: beide muessen ueberhaupt etwas finden.
        Assert.True(karten.Count > 0, "Der Empfehler findet an einem kranken Becken nichts.");
        Assert.True(abweichungen.Count > 0, "Die Diagnose findet an einem kranken Becken nichts.");

        var vonDerDiagnose = abweichungen.Select(a => Messgroesse(a.StableKey)).ToHashSet(StringComparer.Ordinal);
        var nurVomEmpfehler = karten
            .Where(k => k.Severity is Kartenschwere.Gefahr or Kartenschwere.Warnung)
            .Select(k => Messgroesse(k.Title))
            .Where(m => m.Length > 0 && !vonDerDiagnose.Contains(m))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(nurVomEmpfehler.Count == 0,
            "Der Empfehler meldet Messgroessen, zu denen die sichtbare Diagnose schweigt: "
            + string.Join(", ", nurVomEmpfehler)
            + ".\n\nDas heisst: seine Texte werden gerechnet und weggeworfen, obwohl sie etwas "
            + "sagen, das der Nutzer nirgends sieht. Bevor jemand die 852 Zeilen loescht, "
            + "gehoert das hierher — und bevor jemand sie behaelt, gehoert entschieden, wo sie "
            + "erscheinen.\n\nGefunden hat die Diagnose: " + string.Join(", ", vonDerDiagnose.Order()));
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>Die Messgrösse hinter einer Kennung oder einer Überschrift.</summary>
    /// <remarks>
    /// Grob mit Absicht: verglichen wird, <i>worüber</i> gesprochen wird, nicht
    /// wie. Was sich keiner Messgrösse zuordnen lässt, bleibt draussen.
    /// </remarks>
    private static string Messgroesse(string text)
    {
        var klein = text.ToLowerInvariant();

        /* Sauerstoff ZUERST. Die Diagnose nennt ihn "hydro.do", der Empfehler
           "Geloester Sauerstoff zu niedrig". Im ersten Anlauf pruefte ich auf
           "do " mit Leerzeichen — "hydro.do" fiel durch, und die Pruefung
           meldete, die Diagnose schweige zum Sauerstoff. Sie tut es nicht
           (CheckDissolvedOxygen, Zeile 491). Der Fehler war meine Zuordnung,
           nicht die App. */
        if (klein.Contains("sauerstoff") || klein.Contains(".do") || klein.Contains("oxygen"))
            return "sauerstoff";

        if (klein.Contains("orp")) return "orp";
        if (klein.Contains("temp") || klein.Contains("wärm") || klein.Contains("warm")) return "wassertemperatur";
        if (klein.Contains("ph")) return "ph";
        if (klein.Contains("ec") || klein.Contains("leitwert") || klein.Contains("dünger")) return "ec";
        return string.Empty;
    }

    private (IReadOnlyList<RecommendationCard> Karten, IReadOnlyList<GrowDeviation> Abweichungen) Beurteilen(
        double ph, double ec, double wasser, double sauerstoff)
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Vergleich",
            TentId = zelt.Id,
            HydroStyle = HydroStyle.RDWC,
            SeedType = SeedType.Feminized,
            Status = GrowStatus.Running,
            StartDate = DateTime.Today.AddDays(-60),
            FlipDate = DateTime.Today.AddDays(-30),
        });
        var grow = _grows.GetGrow(growId)!;

        for (var tag = 3; tag >= 0; tag -= 1)
        {
            _grows.CreateMeasurement(new Measurement
            {
                GrowId = growId,
                TakenAt = DateTime.Today.AddDays(-tag).AddHours(9),
                Stage = GrowStage.Flower,
                ReservoirPh = ph,
                ReservoirEc = ec,
                ReservoirWaterTempC = wasser,
                DissolvedOxygenMgL = sauerstoff,
                Source = ValueOrigin.Manual,
            });
        }

        var wissen = new KnowledgeBaseLoader(_pfade, NullLogger<KnowledgeBaseLoader>.Instance);
        wissen.Initialize();
        var ziele = new TargetValueService(wissen);
        var messungen = _grows.GetMeasurementsForGrow(growId);
        var letzte = messungen.OrderByDescending(m => m.TakenAt).First();

        var diagnose = new DeviationAnalyzerService(ziele, alertRules: null, wissen);
        var empfehler = new RecommendationEngine(
            new CultivationKnowledgeService(wissen), new MeasurementSanityService());

        return (
            empfehler.Evaluate(grow, letzte, null, null),
            diagnose.Analyze(grow, messungen));
    }

    private void KopiereWissen()
    {
        var quelle = Path.Combine(ProjektWurzel(), "GrowDiary.Web", "wwwroot", "knowledge-defaults");
        var ziel = Path.Combine(_wurzel, "wwwroot", "knowledge-defaults");
        foreach (var datei in Directory.EnumerateFiles(quelle, "*.json", SearchOption.AllDirectories))
        {
            var pfad = Path.Combine(ziel, Path.GetRelativePath(quelle, datei));
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.Copy(datei, pfad);
        }
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
