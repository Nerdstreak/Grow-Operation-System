using System.Text.RegularExpressions;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Phase eines Grows kommt aus <b>einer</b> Quelle.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Vier Stellen leiteten „in welcher Phase
/// ist dieser Grow gerade" aus der <b>letzten Messung</b> ab
/// (<c>latest?.Stage ?? GrowStage.Veg</c>), während der Rest der App
/// <see cref="GrowStageResolver"/> fragt.</para>
///
/// <para><b>Warum das auseinanderläuft.</b> Die Phase auf einer Messung ist
/// eine Momentaufnahme: sie beschreibt <i>diese Messung</i>, nicht den Grow von
/// heute. Wer am 1. August misst, am 10. flippt und danach die Sensoren machen
/// lässt, hat oben in der Kopfzeile „Blüte · Tag 14" stehen — und daneben
/// Zielbänder aus der Vegetation. Genau diese Sorte Widerspruch (EC 0,6–0,8 in
/// der Diagnose gegen 0,9–1,1 in der Kachel) steht in <c>CLAUDE.md</c> unter
/// „EINE WAHRHEIT JE ZAHL".</para>
///
/// <para>Schlimmer noch beim Urlaubswächter: der schickt <b>Nachrichten aufs
/// Telefon</b>. Ein Grow, der seit Wochen automatisch misst, bekommt Warnungen
/// gegen die falschen Bänder — und wer im Urlaub ist, kann nicht nachsehen.</para>
///
/// <para>Und die Automessung stempelte die Phase der letzten Messung auf jede
/// neue: einmal falsch, für immer falsch, weil die nächste wieder von dort
/// abschreibt.</para>
/// </remarks>
public sealed class EinePhaseJeGrowTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;

    public EinePhaseJeGrowTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "EinePhase_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>
    /// Der Urlaubswächter urteilt nach der Phase von <b>heute</b>.
    /// </summary>
    /// <remarks>
    /// Der Aufbau: ein Grow, der vor 30 Tagen geflippt hat — also unstrittig in
    /// der Blüte —, dessen Messungen aber alle noch „Veg" tragen, weil sie
    /// automatisch erfasst wurden und die Automessung die Phase der letzten
    /// Messung weiterschrieb.
    ///
    /// Geprüft wird nicht ein bestimmtes Band, sondern die <b>Gleichheit</b>:
    /// derselbe Grow mit denselben Zahlen muss dasselbe Urteil bekommen, egal
    /// welche Beschriftung auf den Messzeilen steht. Damit hängt die Prüfung an
    /// keiner Zahl, die sich morgen ändern darf.
    /// </remarks>
    [Fact]
    public void DieAufschriftDerMessungAendertDasUrteilNicht()
    {
        var mitVegAufschrift = BefundeFuerBluetegrow(aufschrift: GrowStage.Veg);
        var mitBlueteAufschrift = BefundeFuerBluetegrow(aufschrift: GrowStage.Flower);

        // Mengenwaechter: ohne Befunde waere jede Gleichheit darunter wertlos.
        Assert.True(mitBlueteAufschrift.Count > 0,
            "Der Aufbau erzeugt gar keinen Befund — dann prueft der Vergleich darunter nichts. "
            + "Die Messreihe muss so weit aus dem Band laufen, dass der Waechter anschlaegt.");

        Assert.True(
            mitVegAufschrift.SequenceEqual(mitBlueteAufschrift),
            "Derselbe Grow, dieselben Zahlen — aber ein anderes Urteil, je nachdem was auf den "
            + "Messzeilen steht:\n"
            + $"  Aufschrift „Veg\":    {string.Join(", ", mitVegAufschrift)}\n"
            + $"  Aufschrift „Bluete\": {string.Join(", ", mitBlueteAufschrift)}\n"
            + "Der Grow ist vor 30 Tagen geflippt; die Aufschrift ist eine Momentaufnahme von "
            + "damals. Der Waechter schickt Nachrichten aufs Telefon — hier gegen die falschen "
            + "Baender.");
    }

    /// <summary>
    /// Eine automatisch erfasste Messung trägt die Phase von <b>heute</b>.
    /// </summary>
    /// <remarks>
    /// Der Fehler nährte sich selbst: die Automessung schrieb die Phase der
    /// <i>letzten</i> Messung auf jede neue. Ein einziger falscher Stempel — oder
    /// schlicht ein Flip, an dem niemand von Hand gemessen hat — pflanzte sich
    /// dann durch jede weitere Zeile fort.
    /// </remarks>
    [Fact]
    public void EineAutoMessungStempeltDiePhaseVonHeute()
    {
        var quelltext = Quelltext("Services", "AutoMeasurementExecutionService.cs");

        Assert.False(
            Regex.IsMatch(quelltext, @"Stage\s*=\s*_repository\.GetLatestMeasurement\("),
            "Die Automessung schreibt die Phase der letzten Messung ab. Nach einem Flip, an dem "
            + "niemand von Hand gemessen hat, traegt ab da JEDE automatische Zeile „Veg\" — und "
            + "jede naechste schreibt es weiter. Richtig ist GrowStageResolver.Resolve.");
    }

    /// <summary>
    /// Die Zählung: <b>niemand</b> leitet die heutige Phase aus einer Messung ab.
    /// </summary>
    /// <remarks>
    /// <para>Vier Stellen an einem Tag sind kein Zufall, sondern ein Muster —
    /// also wird über die Grundmenge geprüft und nicht über eine Liste. Gesucht
    /// wird die Form <c>…?.Stage ?? GrowStage.…</c>: eine Messung, aus der eine
    /// Phase für <i>jetzt</i> wird.</para>
    ///
    /// <para>Ausnahmen brauchen einen ausgeschriebenen Grund und stehen
    /// unten.</para>
    /// </remarks>
    [Fact]
    public void NiemandLeitetDieHeutigePhaseAusEinerMessungAb()
    {
        /* Ausgeschriebene Ausnahmen. Eine Stelle darf die AUFGESCHRIEBENE Phase
           lesen, wenn es ihr um die Vergangenheit geht — nicht um heute. */
        var ausnahmen = new Dictionary<string, string>
        {
            ["MeasurementAssessmentService.cs"] =
                "Vergleicht die aufgeschriebene Phase mit der gerechneten und meldet den "
                + "Unterschied. Die aufgeschriebene ist hier der Gegenstand, nicht die Abkuerzung.",
        };

        var treffer = new List<string>();
        var gesehen = 0;

        foreach (var datei in Directory.EnumerateFiles(
                     Path.Combine(ProjektWurzel(), "GrowDiary.Web"), "*.cs", SearchOption.AllDirectories))
        {
            gesehen += 1;
            var name = Path.GetFileName(datei);
            if (ausnahmen.ContainsKey(name)) continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                var zeile = zeilen[i];

                // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
                var ohneKommentar = zeile.Split("//")[0];
                /* Auch die geklammerte Form: `latest?.Stage ?? (grow is null ? null :
                   Resolve(...))` stand im Dashboard-Bauer und rutschte im ersten
                   Lauf durch ein zu enges Muster. */
                if (!Regex.IsMatch(ohneKommentar, @"\?\.Stage\s*\?\?")) continue;

                treffer.Add($"{name}:{i + 1}  {zeile.Trim()}");
            }
        }

        // Mengenwaechter: sieht die Zaehlung ihre Grundmenge ueberhaupt?
        Assert.True(gesehen >= 200,
            $"Nur {gesehen} Quelldateien gefunden — die Zaehlung sieht ihre Grundmenge nicht "
            + "und waere auch bei jedem Fehler gruen.");

        Assert.True(treffer.Count == 0,
            "Diese Stellen machen aus der Aufschrift einer Messung die Phase von HEUTE:\n"
            + string.Join("\n", treffer)
            + "\n\nDie Phase eines Grows kommt aus GrowStageResolver.Resolve(grow, heute). Was auf "
            + "einer Messzeile steht, beschreibt DIESE Messung — nach einem Flip laeuft es "
            + "auseinander, und dann widerspricht die Kachel der Kopfzeile.\n"
            + "Wer eine Ausnahme braucht, traegt sie oben mit ausgeschriebenem Grund ein.");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>Ein Grow, der vor 30 Tagen geflippt hat, mit driftendem pH.</summary>
    private IReadOnlyList<string> BefundeFuerBluetegrow(GrowStage aufschrift)
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt " + aufschrift, TentType = TentType.Production });
        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Bluete " + aufschrift,
            TentId = zelt.Id,
            HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running,
            SeedType = SeedType.Feminized,
            StartDate = DateTime.Today.AddDays(-70),
            FlipDate = DateTime.Today.AddDays(-30),
        });
        var grow = _grows.GetGrow(growId)!;

        // Gegenprobe zum Aufbau: der Ermittler muss diesen Grow als Bluete sehen,
        // sonst prueft der Vergleich oben zwei gleiche Faelle.
        Assert.True(
            GrowStageResolver.Resolve(grow, DateTime.Today) is GrowStage.Flower or GrowStage.Finish
                or GrowStage.Transition,
            "Der Aufbau liefert keinen Bluetegrow — dann vergleicht der Test zweimal dasselbe.");

        /* Eine Reihe, die deutlich ueber jedem Band liegt: pH steigt von 6,4 auf
           7,2, EC faellt. Wichtig ist nur, dass ueberhaupt etwas anschlaegt. */
        for (var tag = 12; tag >= 0; tag -= 1)
        {
            _grows.CreateMeasurement(new Measurement
            {
                GrowId = growId,
                TakenAt = DateTime.Today.AddDays(-tag).AddHours(9),
                Stage = aufschrift,
                ReservoirPh = 6.4 + (12 - tag) * 0.07,
                ReservoirEc = 1.9 - (12 - tag) * 0.06,
                ReservoirWaterTempC = 19 + (12 - tag) * 0.25,
                Source = ValueOrigin.Manual,
            });
        }

        var laeufer = new TrendWatchRunner(
            _grows,
            new TargetValueService(Wissen()),
            new NotificationService(
                new NotificationSettingsRepository(_pfade),
                _grows,
                new HomeAssistantService(
                    new TestFakes.StubHttpClientFactory(
                        new TestFakes.RecordingHttpHandler((_, _) =>
                            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK))),
                    NullLogger<HomeAssistantService>.Instance),
                NullLogger<NotificationService>.Instance),
            new AppSettingsRepository(_pfade),
            NullLogger<TrendWatchRunner>.Instance);

        return laeufer.Inspect(growId, DateTime.Now)
            .Select(b => $"{b.Code}/{b.Severity}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    private KnowledgeBaseLoader Wissen()
    {
        var ziel = Path.Combine(_wurzel, "wwwroot", "knowledge-defaults");
        if (!Directory.Exists(ziel))
        {
            var quelle = Path.Combine(ProjektWurzel(), "GrowDiary.Web", "wwwroot", "knowledge-defaults");
            foreach (var datei in Directory.EnumerateFiles(quelle, "*.json", SearchOption.AllDirectories))
            {
                var pfad = Path.Combine(ziel, Path.GetRelativePath(quelle, datei));
                Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
                File.Copy(datei, pfad);
            }
        }

        var lader = new KnowledgeBaseLoader(_pfade, NullLogger<KnowledgeBaseLoader>.Instance);
        lader.Initialize();
        return lader;
    }

    private static string Quelltext(params string[] teile)
        => File.ReadAllText(Path.Combine(new[] { ProjektWurzel(), "GrowDiary.Web" }.Concat(teile).ToArray()));

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
