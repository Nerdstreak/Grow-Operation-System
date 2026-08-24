using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Der Testbestand darf den eigenen Regeln der App nicht widersprechen.
/// </summary>
/// <remarks>
/// <para><b>Warum es diese Datei geben muss.</b> Bis zum 24.08.2026 hatte
/// <see cref="Demobestand"/> keinen einzigen Test — obwohl er die Grundlage
/// ist, gegen die alles andere geprüft wird: jede Oberflächen-Messung, jeder
/// E2E-Lauf, jeder Blick auf die laufende App. Ein Fehler <i>im Bestand</i>
/// verdeckt deshalb Fehler <i>in der App</i>, und genau das ist beim Kühler
/// schon passiert: die Testdaten trugen die Steckdose unter einer Kennung ein,
/// die der Regler im Betrieb nie gelesen hätte.</para>
///
/// <para><b>Was hier NICHT geprüft wird.</b> Nicht, ob die Zahlen hübsch sind.
/// Geprüft wird, ob der Bestand eine Geschichte erzählt, die Grow OS selbst für
/// möglich hält — mit denselben Prüfern, die auch auf echte Daten losgehen. Wo
/// die App eine Warnung ausgeben würde, ist der Bestand falsch, nicht die
/// Warnung.</para>
/// </remarks>
public sealed class DemobestandStimmigTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), "grow-os-demo-" + Guid.NewGuid().ToString("N"));

    private readonly ServiceProvider _dienste;
    private readonly GrowRepository _grows;

    public DemobestandStimmigTests()
    {
        Directory.CreateDirectory(_wurzel);
        var pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(pfade);

        // Zaehlung statt Liste: JEDE Ablage aus dem Betrieb wird eingetragen.
        // Eine handgeschriebene Liste haette hier bei jeder neuen Ablage einen
        // Testfehler erzeugt, der nichts mit dem Bestand zu tun hat — und beim
        // ersten Mal genau das getan.
        var sammlung = new ServiceCollection();
        sammlung.AddLogging();
        sammlung.AddSingleton(pfade);

        var ablagen = typeof(GrowRepository).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                        && t.Namespace == typeof(GrowRepository).Namespace
                        && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();

        Assert.True(ablagen.Count >= 10,
            $"Nur {ablagen.Count} Ablagen gefunden — die Reflexion sieht ihre Grundmenge nicht.");

        foreach (var typ in ablagen) sammlung.AddSingleton(typ);
        _dienste = sammlung.BuildServiceProvider();

        _grows = _dienste.GetRequiredService<GrowRepository>();
        Assert.True(Demobestand.IstNoetig(_grows), "Eine frische Datenbank sollte leer sein.");
        Demobestand.Anlegen(_dienste);
    }

    public void Dispose()
    {
        _dienste.Dispose();
        try { Directory.Delete(_wurzel, recursive: true); } catch (IOException) { }
    }

    private GrowRun LaufenderGrow()
    {
        var laufend = _grows.GetAllGrows().Where(g => g.Status == GrowStatus.Running).ToList();
        Assert.Single(laufend);
        return laufend[0];
    }

    /// <summary>Der Bestand legt überhaupt etwas an.</summary>
    /// <remarks>
    /// Der Mengenwächter für alles Folgende: liefe <see cref="Demobestand.Anlegen"/>
    /// still ins Leere, wären alle anderen Prüfungen hier grün, ohne etwas
    /// gesehen zu haben.
    /// </remarks>
    [Fact]
    public void Der_Bestand_legt_wirklich_etwas_an()
    {
        Assert.NotEmpty(_grows.GetTents());
        Assert.True(_grows.GetAllGrows().Count >= 3, "Ein laufender Grow und zwei im Archiv.");
        Assert.False(Demobestand.IstNoetig(_grows));
    }

    /// <summary>Der Lichtzyklus des Bestands passt zur Phase des Grows.</summary>
    /// <remarks>
    /// <para><b>Der Fund, aus dem diese Datei entstand.</b> Der Bestand fuhr
    /// 18/6 bei einem Grow, dessen Flip 35 Tage zurücklag. Genau dazu sagt
    /// <see cref="LightCycleLearner.Mismatch"/>: <i>„Der Grow ist in der Blüte,
    /// das Licht läuft aber 18/6. Das verhindert die Blüte."</i> Aufgefallen ist
    /// es niemandem, weil der Bestand keine Lichtflanken anlegt und der Lerner
    /// deshalb nie etwas zu vergleichen bekam — die Prüfung der App lief über
    /// ihre eigenen Testdaten nie.</para>
    /// </remarks>
    [Fact]
    public void Der_Lichtzyklus_passt_zur_Phase_des_Grows()
    {
        var grow = LaufenderGrow();
        var phase = GrowStageResolver.Resolve(grow, DateTime.Today);

        var tag = DateTime.Today.AddDays(-1);
        var anStunden = Enumerable.Range(0, 24)
            .Count(h => Demoverlauf.LichtBrennt(tag.AddHours(h).AddMinutes(30)));

        // Mengenwaechter: 0 oder 24 Stunden waeren keine Aussage ueber einen Zyklus.
        Assert.InRange(anStunden, 1, 23);

        var zyklus = new LearnedCycle(
            anStunden,
            new TimeOnly(Demoverlauf.LichtAn, 0),
            new TimeOnly(Demoverlauf.LichtAus % 24, 0),
            Days: 7);

        var beanstandung = LightCycleLearner.Mismatch(zyklus, phase, grow.SeedType);
        Assert.True(beanstandung is null,
            $"Der Testbestand widerspricht der eigenen Regel der App: {beanstandung}");
    }

    /// <summary>Lichtplan, Lichtkurve und Zeit-Entitäten nennen dieselbe Uhrzeit.</summary>
    /// <remarks>
    /// Drei Stellen, an denen dieselbe Uhrzeit steht — Lichtplan des Zelts,
    /// Kurvengenerator, <c>time.</c>-Entitäten für den AC-Test. Laufen sie
    /// auseinander, schlägt der Vorschlag im Versuchsaufbau eine Zeit vor, die
    /// nichts mit dem Licht zu tun hat, das der Bestand fährt.
    /// </remarks>
    [Fact]
    public void Lichtplan_und_Lichtkurve_nennen_dieselbe_Uhrzeit()
    {
        var zelt = _grows.GetTents()[0];
        var plan = _grows.GetActiveLightScheduleForTent(zelt.Id);

        Assert.NotNull(plan);
        Assert.Equal(Demoverlauf.LichtAnUhr, plan!.LightsOnTime);
        Assert.Equal(Demoverlauf.LichtAusUhr, plan.LightsOffTime);

        var ein = DemoData.EntityState(DemoData.LichtEinZeit, DateTime.UtcNow);
        var aus = DemoData.EntityState(DemoData.LichtAusZeit, DateTime.UtcNow);
        Assert.NotNull(ein);
        Assert.NotNull(aus);
        Assert.Equal(Demoverlauf.LichtAnUhr, AcTest.AlsHhMm(ein!.State));
        Assert.Equal(Demoverlauf.LichtAusUhr, AcTest.AlsHhMm(aus!.State));
    }

    /// <summary>Jede Entität, die der Bestand einträgt, antwortet auch.</summary>
    /// <remarks>
    /// <para><b>Der Kühler-Fehler in allgemeiner Form.</b> Der Bestand trug eine
    /// Steckdose ein, die im Betrieb unter einer anderen Kennung gesucht wurde —
    /// die Oberfläche zeigte trotzdem einen Zustand, weil die Testdaten ihn
    /// zusätzlich unter der Metrik-Kennung lieferten. Eine eingetragene Kennung,
    /// die auf dem <b>Betriebsweg</b> nichts zurückgibt, ist eine Kulisse.</para>
    ///
    /// <para>Geprüft wird über <see cref="DemoData.EntityState"/> — denselben
    /// Weg, den <c>GetEntityStateAsync</c> im Testbetrieb nimmt.</para>
    /// </remarks>
    [Fact]
    public void Jede_eingetragene_Entitaet_antwortet_auf_dem_Betriebsweg()
    {
        var einstellungen = _dienste.GetRequiredService<AppSettingsRepository>();
        var zelt = _grows.GetTents()[0];
        var geraete = AcTest.Lesen(einstellungen, zelt.Id);

        // Mengenwaechter: ohne Geraete prueft die Schleife nichts.
        Assert.NotEmpty(geraete);

        // Die Kennungen kommen aus dem, was WIRKLICH gespeichert ist — Zelt und
        // Geraete-Eintraege. Der erste Anlauf hat `DemoData.KuehlerSteckdose`
        // abgetippt: damit prueft der Test seine eigene Annahme statt den
        // Bestand. Ein Pruefer hat die Zeile im Bestand auf
        // "switch.demo_wasserkuehler" gesetzt — plausibel, aber falsch — und
        // alle 1381 Tests blieben gruen. Genau der Fehler, gegen den diese
        // Datei geschrieben ist.
        var ausGeraeten = geraete
            .SelectMany(g => new[] { g.LeistungEntityId, g.ModusEntityId, g.EinZeitEntityId, g.AusZeitEntityId });

        var ausZelt = new[] { zelt.ChillerSwitchEntityId, zelt.WaterTargetEntityId };

        var ausHardware = _dienste.GetRequiredService<HardwareRepository>()
            .GetHardwareItemsByTent(zelt.Id)
            .Select(h => h.HaEntityId);

        var kennungen = ausGeraeten.Concat(ausZelt).Concat(ausHardware)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(kennungen.Count >= 4, "Zu wenige Kennungen — die Zaehlung sieht ihre Grundmenge nicht.");

        var stumm = kennungen
            .Where(k => DemoData.EntityState(k, DateTime.UtcNow) is null)
            .ToList();

        Assert.True(stumm.Count == 0,
            "Der Bestand traegt Kennungen ein, die auf dem Betriebsweg nichts melden: "
            + string.Join(", ", stumm));
    }

    /// <summary>Jede Messgröße, für die es einen Wert gibt, ist auch zugeordnet.</summary>
    /// <remarks>
    /// <para><b>Der Fall, den niemand gesehen hat.</b> Auf der
    /// Home-Assistant-Seite stand im Testbetrieb <i>„Entities gemappt: 0 von
    /// 17"</i>, während die Live-Seite dreizehn Werte zeigte — der Testbestand
    /// lieferte alles ohne Zuordnung. Damit lief jede Prüfung am zugeordneten
    /// Weg vorbei: die Sensorliste am Zelt, das Alter eines Werts, die
    /// Warnungen über fehlende Zuordnungen.</para>
    ///
    /// <para>Die Zählung geht über die Aufzählung, nicht über eine Liste: was
    /// der Bestand liefert, muss zugeordnet sein.</para>
    /// </remarks>
    [Fact]
    public void Jede_gelieferte_Messgroesse_ist_dem_Zelt_zugeordnet()
    {
        var zelt = _grows.GetTents()[0];
        var zugeordnet = _grows.GetTentSensors(zelt.Id)
            .Select(s => TentSensorMetricKeyMap.Resolve(s.MetricType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var geliefert = DemoData.StatesFor(DateTime.UtcNow).Keys
            .Where(k => Enum.GetValues<SensorMetricType>()
                .Any(a => string.Equals(TentSensorMetricKeyMap.Resolve(a), k, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Mengenwaechter: ohne gelieferte Groessen prueft die Schleife nichts.
        Assert.True(geliefert.Count >= 8,
            $"Nur {geliefert.Count} Messgroessen im Testbestand — die Zaehlung sieht ihre Grundmenge nicht.");

        var fehlend = geliefert.Where(k => !zugeordnet.Contains(k)).ToList();
        Assert.True(fehlend.Count == 0,
            "Der Testbestand liefert Werte, die keinem Sensor am Zelt zugeordnet sind: "
            + string.Join(", ", fehlend));
    }

    /// <summary>Der Bestand legt zu jedem Zelt einen Lichtplan an.</summary>
    /// <remarks>
    /// Ohne ihn laufen Alarme (Tag/Nacht), Stromkosten und der Zeitplan-Vorschlag
    /// im Testbetrieb auf einer Ersatzannahme — also genau dort nicht, wo man
    /// sie ansehen kann.
    /// </remarks>
    [Fact]
    public void Jedes_Zelt_hat_einen_Lichtplan()
    {
        var zelte = _grows.GetTents();
        Assert.NotEmpty(zelte);

        var ohne = zelte
            .Where(z => _grows.GetActiveLightScheduleForTent(z.Id) is null)
            .Select(z => z.Name)
            .ToList();

        Assert.True(ohne.Count == 0, "Ohne Lichtplan: " + string.Join(", ", ohne));
    }

    /// <summary>Der Bestand erzählt den Mehrsorten-Fall — je Topf eine Pflanze.</summary>
    /// <remarks>
    /// <para><b>Der Anlass.</b> Ein Nutzer fährt im RDWC je Topf eine eigene
    /// Sorte und hat den Weg dafür nicht gefunden — auch deshalb, weil der
    /// Testbestand keine einzige Pflanze anlegte: die Karte „Pflanzen &amp;
    /// Sorten" zeigte in der Demo nur ihren Leerzustand, und kein Screenshot,
    /// kein E2E-Lauf und kein Blick auf die laufende App konnte den
    /// Mehrsorten-Weg je sehen.</para>
    /// </remarks>
    [Fact]
    public void Der_Bestand_erzaehlt_den_Mehrsorten_Fall()
    {
        var setups = _dienste.GetRequiredService<SetupRepository>();
        var grow = LaufenderGrow();
        var pflanzen = setups.GetPlantsByGrow(grow.Id);

        // Mengenwaechter: ohne Pflanzen prueft alles Weitere nichts.
        Assert.True(pflanzen.Count >= 3,
            $"Nur {pflanzen.Count} Pflanzen im Bestand — der Mehrsorten-Fall braucht mehrere.");

        // So viele Pflanzen, wie der Grow behauptet — sonst widerspricht sich
        // der Bestand selbst (PlantCount 4, aber 2 erfasst).
        Assert.Equal(grow.PlantCount, pflanzen.Count);

        // MEHRERE Sorten, jede Pflanze mit einer: das ist der gemeldete Fall.
        var sorten = pflanzen.Select(p => p.StrainId).Distinct().ToList();
        Assert.True(sorten.Count >= 2,
            "Alle Pflanzen tragen dieselbe Sorte — der Mehrsorten-Fall ist unsichtbar.");
        Assert.DoesNotContain(null, sorten);

        // Jede Pflanze in ihrem eigenen Topf, Nummern ab 1 im Bereich des
        // Systems — die Zaehlung der Draufsicht.
        var toepfe = pflanzen.Select(p => p.SiteIndex).ToList();
        Assert.DoesNotContain(null, toepfe);
        Assert.Equal(toepfe.Count, toepfe.Distinct().Count());
        Assert.All(toepfe, t => Assert.InRange(t!.Value, 1, grow.PlantCount ?? int.MaxValue));
    }
}
