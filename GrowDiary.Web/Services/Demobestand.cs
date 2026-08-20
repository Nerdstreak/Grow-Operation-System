using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Ein vollständiger Datenbestand zum Durchspielen — Zelt bis Aushärte-Glas.
///
/// <para><b>Der Anlass.</b> <see cref="DemoData"/> fälscht nur die
/// Home-Assistant-Seite: Sensoren, Verlauf, Dosierungen, Kamera. Grows, Messungen,
/// Ernten und alles andere kommen aus der Datenbank, und die legt niemand an. Auf
/// einem frischen Rechner steht deshalb überall „Noch keine …", und die
/// automatische Prüfung der Oberfläche prüft nichts: von 34 Fällen aus vier
/// E2E-Dateien übersprangen sich <b>31</b>, weil die Daten fehlten, gegen die sie
/// hätten prüfen sollen. Gefunden hat das der Tester — „es fehlen wieder mock
/// daten" —, nicht die Sammlung.</para>
///
/// <para><b>Was er anlegt.</b> Ein Zelt mit RDWC-Aufbau, einen laufenden Grow in
/// der Blüte mit sechs Wochen Messungen (Hand und Automatik gemischt), Fotos,
/// Journal, fällige Aufgaben, ein kalibriertes Messgerät mit Historie, eine
/// Alarmregel, ein offenes Risiko-Ereignis, ein Aushärte-Glas mit Ablesungen —
/// und zwei abgeschlossene Läufe mit Ernte, damit Archiv und Kostenrechnung
/// etwas zu zeigen haben.</para>
///
/// <para><b>Warum er fremde Daten nicht anfassen kann.</b> Er läuft nur, wenn in
/// der Datenbank <b>überhaupt kein Grow</b> steht. Wer schon einen hat, hat
/// eigene Daten — die sind besser als jede Demo, und sie zu ergänzen wäre ein
/// Eingriff, um den niemand gebeten hat.</para>
///
/// <para><b>Alles trägt „Testdaten" im Namen.</b> Wer den Bestand später neben
/// eigenen Läufen sieht, muss die Frage „ist das echt?" nicht stellen.</para>
/// </summary>
public static class Demobestand
{
    /// <summary>
    /// Ortszeit auf die Mittagsstunde legen.
    /// </summary>
    /// <remarks>
    /// Die Datumsfelder eines Grows gehen zwei verschiedene Wege in die
    /// Datenbank: <c>GerminatedAt</c>, <c>VegStartedAt</c> und
    /// <c>FinishStartedAt</c> durch <c>ToStorageUtc</c> (also mit
    /// <c>ToUniversalTime</c>), <c>StartDate</c>, <c>FlipDate</c> und
    /// <c>EndDate</c> dagegen unverschoben. Ein lokales Mitternachtsdatum
    /// rutscht in den UTC-Spalten dadurch einen Tag zurück. Mittags gesetzt
    /// passiert das in keiner europäischen Zeitzone.
    /// </remarks>
    private static DateTime Tag(int vorTagen)
        => DateTime.Today.AddDays(-vorTagen).AddHours(12);

    /// <summary>Läuft der Bestand? Nur in eine Datenbank ganz ohne Grows.</summary>
    public static bool IstNoetig(GrowRepository grows) => grows.GetAllGrows().Count == 0;

    /// <summary>
    /// Legt den ganzen Bestand an. Ruft nur, wer <see cref="IstNoetig"/> gefragt hat.
    /// </summary>
    /// <returns>Was angelegt wurde, für die Zeile im Protokoll.</returns>
    public static string Anlegen(IServiceProvider dienste)
    {
        var grows = dienste.GetRequiredService<GrowRepository>();
        var hydro = dienste.GetRequiredService<HydroSetupRepository>();
        var messungen = dienste.GetRequiredService<MeasurementRepository>();
        var ernten = dienste.GetRequiredService<HarvestRepository>();
        var journal = dienste.GetRequiredService<JournalRepository>();
        var aufgaben = dienste.GetRequiredService<TaskRepository>();
        var hardware = dienste.GetRequiredService<HardwareRepository>();
        var alarme = dienste.GetRequiredService<AlertRuleRepository>();
        var aushaerten = dienste.GetRequiredService<CuringRepository>();
        var setups = dienste.GetRequiredService<SetupRepository>();
        var dosierung = dienste.GetRequiredService<DosingRepository>();

        var zelt = ZeltAnlegen(grows);
        var aufbau = AufbauAnlegen(hydro, zelt.Id);
        SorteAnlegen(setups);

        var laufend = LaufenderGrowAnlegen(grows, zelt.Id, aufbau.Id);
        var anzahl = MessungenAnlegen(messungen, laufend);
        JournalAnlegen(journal, laufend.Id);
        AufgabenAnlegen(aufgaben, laufend.Id);
        var geraet = GeraetMitHistorieAnlegen(hardware, zelt.Id, laufend.Id);
        AlarmregelAnlegen(alarme, zelt.Id);
        RisikoAnlegen(hardware, zelt.Id, laufend.Id, geraet.Id);
        GlasAnlegen(aushaerten, laufend.Id);
        PumpenAnlegen(dosierung, zelt.Id);

        AbgeschlossenenGrowAnlegen(grows, ernten, zelt.Id, aufbau.Id,
            "Northern Lights (Testdaten)", "Northern Lights", "Sensi Seeds",
            vorTagen: 190, dauerTage: 82, nass: 412, trocken: 96);
        AbgeschlossenenGrowAnlegen(grows, ernten, zelt.Id, aufbau.Id,
            "Gorilla Glue (Testdaten)", "Gorilla Glue #4", "GG Strains",
            vorTagen: 95, dauerTage: 88, nass: 468, trocken: 108);

        return $"1 Zelt, 1 RDWC-Aufbau, 3 Grows (1 laufend, 2 im Archiv), {anzahl} Messungen";
    }

    private static Tent ZeltAnlegen(GrowRepository grows)
    {
        // Die Objekt-Variante, nicht CreateTent(string): die legt ein nacktes
        // Zelt mit TentType.MultiPurpose an. Der Rueckgabewert traegt die Id —
        // das uebergebene Objekt hat danach weiterhin Id 0.
        return grows.CreateTent(new Tent
        {
            Name = "Blütezelt (Testdaten)",
            // Production, weil das Setup weiter unten SetupType.Production ist:
            // SetupTentCompatibilityPolicy laesst zu einem Production-Zelt nur
            // ein Production-Setup zu.
            TentType = TentType.Production,
            Status = TentStatus.Active,
            WidthCm = 120,
            DepthCm = 120,
            TentHeightCm = 200,
            LightType = "LED",
            LightWatt = 480,
            Notes = "Testdaten — dieses Zelt gibt es nicht.",
            // Die Kuehler-Steuerung ist im Testbestand AN. Sonst waere die
            // Karte auf der Live-Seite unsichtbar und niemand — ich
            // eingeschlossen — saehe je, was sie zeigt.
            ChillerControlEnabled = true,
            ChillerSwitchEntityId = DemoData.KuehlerSteckdose,
            // LeafTempOffsetC bleibt auf seinem Standard (2,0). Auf 0 gesetzt
            // rechnete die App Luft- statt Blatt-VPD.
        });
    }

    private static GrowSystem AufbauAnlegen(HydroSetupRepository hydro, int zeltId)
    {
        // CreateHydroSetup, nicht CreateSystem: nur die erste normalisiert UND
        // prueft. Fuer RDWC gelten vier Zusatzpruefungen, jede wirft.
        return hydro.CreateHydroSetup(new GrowSystem
        {
            TentId = zeltId,
            Name = "RDWC 4er (Testdaten)",
            // Hier ein STRING, nicht das Enum — auf GrowRun heisst dasselbe
            // Feld genauso und ist dort ein Enum. Eine der Fallen des Hauses.
            HydroStyle = Models.HydroStyle.RDWC.ToString(),
            PotCount = 4,
            PotSizeLiters = 27,
            ReservoirLiters = 100,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.External,
            Status = HydroSetupStatus.Active,
            HasCirculationPump = true,
            HasAirPump = true,
            AirPumpLitersPerHour = 3600,
            AirStoneCount = 4,
            HasChiller = true,
            DisplayOrder = 1,
        });
    }

    private static void SorteAnlegen(SetupRepository setups)
    {
        setups.CreateStrain(new Strain
        {
            Name = "White Widow (Testdaten)",
            Breeder = "Royal Queen Seeds",
            Dominance = StrainDominance.Hybrid,
            FlowerWeeksMin = 8,
            FlowerWeeksMax = 9,
            SeedKind = Models.SeedKind.Feminized,
            ThcPercent = 19,
            CbdPercent = 0.6,
            Notes = "Testdaten.",
        });
    }

    private static GrowRun LaufenderGrowAnlegen(GrowRepository grows, int zeltId, int aufbauId)
    {
        // Der Flip liegt 35 Tage zurueck. Das ist Absicht und liegt zwischen
        // zwei Grenzen des GrowStageResolver: unter 10 Tagen kaeme
        // GrowStage.Transition heraus, ab Tag 49 (9 Wochen minus 14 Tage
        // Finish-Vorlauf) GrowStage.Finish. Dazwischen liegt die Bluete.
        var grow = new GrowRun
        {
            TentId = zeltId,
            SystemId = aufbauId,
            Name = "White Widow (Testdaten)",
            Strain = "White Widow",
            Breeder = "Royal Queen Seeds",
            Status = GrowStatus.Running,
            MediumType = MediumType.Hydro,
            HydroStyle = Models.HydroStyle.RDWC,
            IrrigationType = IrrigationType.ActiveHydro,
            Environment = GrowEnvironment.Indoor,
            WaterSource = WaterSource.Tap,
            SeedType = Models.SeedType.Feminized,
            StartMaterial = StartMaterial.Seed,
            EntryPoint = GrowEntryPoint.Germination,
            PlantCount = 4,
            StartDate = Tag(73),
            GerminatedAt = Tag(73),
            VegStartedAt = Tag(64),
            FlipDate = Tag(35),
            BreederFlowerWeeksMin = 8,
            BreederFlowerWeeksMax = 9,
            Light = "LED 480 W",
            ReservoirSize = "100 L",
            Notes = "Testdaten: laufender Lauf in der Blüte.",
            // Ohne die Rampe gibt es keinen Sollwert, und ohne Sollwert
            // schaltet der Kuehler-Regler bewusst gar nichts — die Karte
            // saegte dann immer denselben Satz.
            NightRampEnabled = true,
        };

        grow.Id = grows.CreateGrow(grow);
        return grow;
    }

    /// <summary>
    /// Sechs Wochen Messungen, Hand und Automatik gemischt.
    /// </summary>
    /// <remarks>
    /// <para><b>Warum gemischt.</b> Die Automatik kann nur elf Felder füllen
    /// (<c>AutoMeasurementField</c>); Drain, Addback und Höhe bleiben
    /// Handarbeit. Ein Bestand, in dem beides vorkommt, ist deshalb nicht nur
    /// hübscher, sondern die einzige Lage, in der die Herkunfts-Anzeige
    /// („Hand" gegen „Automatik") überhaupt etwas zu unterscheiden hat.</para>
    ///
    /// <para><b>TakenAt ist Ortszeit</b>, nicht UTC — die Spalte heißt nicht
    /// „…Utc". Wer hier UTC hineinschreibt, datiert in Deutschland jede Messung
    /// ein bis zwei Stunden zurück und schiebt sie über Mitternacht auf den
    /// falschen Tag.</para>
    ///
    /// <para><b>In die Bilanz zählen genau fünf Größen:</b> pH, EC, ORP,
    /// Wassertemperatur und VPD — letzteres nur, wenn Lufttemperatur UND
    /// Feuchte in <i>derselben</i> Zeile stehen. Deshalb stehen sie hier immer
    /// zusammen.</para>
    /// </remarks>
    /// <summary>
    /// Sechs Wochen Messungen — aus <see cref="Demoverlauf"/>, nicht aus
    /// einer eigenen Kurve.
    /// </summary>
    /// <remarks>
    /// <para><b>Warum von dort.</b> Die Diagramme auf der Live- und der
    /// Zeltseite kommen aus den gefälschten Sensoren, dieses Protokoll aus
    /// den Messungen. Zwei Kurven für dieselbe Sache heißt: das Diagramm
    /// zeigt einen EC-Sägezahn, und die Zeile daneben behauptet etwas
    /// anderes. Beide lesen deshalb dieselbe Quelle.</para>
    ///
    /// <para><b>Was hier eigen bleibt.</b> Nur die Auswahl: WANN gemessen
    /// wird und WELCHE Felder dabei anfallen. Die Automatik kann elf Felder
    /// füllen und misst zweimal am Tag; von Hand kommen ORP, Sauerstoff,
    /// Füllstand und Höhe dazu, dafür nur einmal die Woche. Ein Bestand, in
    /// dem beides vorkommt, ist die einzige Lage, in der die
    /// Herkunfts-Anzeige („Hand" gegen „Automatik") etwas zu unterscheiden
    /// hat.</para>
    ///
    /// <para><b>TakenAt ist Ortszeit</b>, nicht UTC — die Spalte heißt nicht
    /// „…Utc". Wer hier UTC hineinschreibt, datiert in Deutschland jede
    /// Messung ein bis zwei Stunden zurück und schiebt sie über Mitternacht
    /// auf den falschen Tag.</para>
    /// </remarks>
    private static int MessungenAnlegen(MeasurementRepository messungen, GrowRun grow)
    {
        var anzahl = 0;

        for (var tag = Demoverlauf.TageRueckwaerts; tag >= 0; tag--)
        {
            var wann = DateTime.Today.AddDays(-tag);

            // Die Automatik misst zweimal am Tag: nach Licht an und vor Licht aus.
            foreach (var stunde in new[] { 7, 21 })
            {
                var zeitpunkt = wann.AddHours(stunde);

                // Nichts in der Zukunft anlegen. Wer den Bestand morgens
                // erzeugt, haette sonst eine Messung von „heute 21:00" — die
                // Beurteilung weist die als „unplausibler Zeitpunkt" aus und
                // laesst sie ganz aus der Bilanz. Gesehen beim LESEN der
                // Seite, nicht beim Messen.
                if (zeitpunkt > DateTime.Now) continue;

                messungen.CreateMeasurement(new Measurement
                {
                    GrowId = grow.Id,
                    TakenAt = zeitpunkt,
                    Stage = PhaseAm(grow, wann),
                    Source = ValueOrigin.HomeAssistant,
                    ReservoirPh = Runden(Demoverlauf.Ph(zeitpunkt), 2),
                    ReservoirEc = Runden(Demoverlauf.Ec(zeitpunkt), 2),
                    ReservoirWaterTempC = Runden(Demoverlauf.WasserTempC(zeitpunkt), 1),
                    AirTemperatureC = Runden(Demoverlauf.LuftTempC(zeitpunkt), 1),
                    HumidityPercent = Runden(Demoverlauf.FeuchtePercent(zeitpunkt), 0),
                    ReservoirLevelLiters = Runden(Demoverlauf.FuellstandLiter(zeitpunkt), 0),
                });
                anzahl++;
            }

            // Von Hand am Vorabend des Wasserwechsels — mit den Feldern, die
            // die Automatik gar nicht kennt.
            var abends = wann.AddHours(19).AddMinutes(20);
            if (Demoverlauf.SeitWasserwechsel(wann) == Demoverlauf.WasserwechselAlleTage - 1
                && abends <= DateTime.Now)
            {
                messungen.CreateMeasurement(new Measurement
                {
                    GrowId = grow.Id,
                    TakenAt = abends,
                    Stage = PhaseAm(grow, wann),
                    Source = ValueOrigin.Manual,
                    ReservoirPh = Runden(Demoverlauf.Ph(abends), 2),
                    ReservoirEc = Runden(Demoverlauf.Ec(abends), 2),
                    ReservoirWaterTempC = Runden(Demoverlauf.WasserTempC(abends), 1),
                    AirTemperatureC = Runden(Demoverlauf.LuftTempC(abends), 1),
                    HumidityPercent = Runden(Demoverlauf.FeuchtePercent(abends), 0),
                    OrpMv = Runden(Demoverlauf.OrpMv(abends), 0),
                    DissolvedOxygenMgL = Runden(Demoverlauf.SauerstoffMgL(abends), 1),
                    ReservoirLevelLiters = Runden(Demoverlauf.FuellstandLiter(abends), 0),
                    HeightCm = Runden(46 + (Demoverlauf.TageRueckwaerts - tag) * 0.9, 0),
                    Notes = Demoverlauf.Stoerung(wann)
                        ? "Testdaten: Wasser zu warm, Kühler prüfen."
                        : "Testdaten: Kontrolle vor dem Wasserwechsel.",
                });
                anzahl++;
            }
        }

        // Eine frische Messung zum Schluss — sonst haengt „zuletzt gemessen"
        // je nach Uhrzeit bis zu einen Tag zurueck, und die App mahnt im
        // Demobestand sofort zum Nachmessen.
        var jetzt = DateTime.Now.AddMinutes(-90);
        messungen.CreateMeasurement(new Measurement
        {
            GrowId = grow.Id,
            TakenAt = jetzt,
            Stage = PhaseAm(grow, jetzt),
            Source = ValueOrigin.Manual,
            ReservoirPh = Runden(Demoverlauf.Ph(jetzt), 2),
            ReservoirEc = Runden(Demoverlauf.Ec(jetzt), 2),
            ReservoirWaterTempC = Runden(Demoverlauf.WasserTempC(jetzt), 1),
            AirTemperatureC = Runden(Demoverlauf.LuftTempC(jetzt), 1),
            HumidityPercent = Runden(Demoverlauf.FeuchtePercent(jetzt), 0),
            OrpMv = Runden(Demoverlauf.OrpMv(jetzt), 0),
            DissolvedOxygenMgL = Runden(Demoverlauf.SauerstoffMgL(jetzt), 1),
            ReservoirLevelLiters = Runden(Demoverlauf.FuellstandLiter(jetzt), 0),
            HeightCm = Runden(46 + Demoverlauf.TageRueckwaerts * 0.9, 0),
            Notes = "Testdaten: letzte Kontrolle.",
        });
        anzahl++;
        // EINE Zeile mit unmoeglichen Werten. Die gehoert in den Demobestand,
        // weil die App dafuer eine eigene Anzeige hat — Zeichen, Farbe, Zaehler
        // in der Bilanz —, und ohne so eine Zeile sieht sie niemand. Genau
        // dieser Fall ist beim Nutzer aufgeschlagen: 9000 Grad Luft standen im
        // Protokoll wie jede andere Zahl.
        //
        // Der Grund ist ein echter: ein Messgeraet, das die Verbindung verliert,
        // meldet oft nicht nichts, sondern Unsinn.
        var gestoert = DateTime.Today.AddDays(-11).AddHours(14);
        messungen.CreateMeasurement(new Measurement
        {
            GrowId = grow.Id,
            TakenAt = gestoert,
            Stage = PhaseAm(grow, gestoert),
            Source = ValueOrigin.HomeAssistant,
            ReservoirEc = 99999,
            ReservoirWaterTempC = 5000,
            AirTemperatureC = 9000,
            Co2Ppm = -500,
            Notes = "Testdaten: Sonde hatte einen Aussetzer — die Werte kann es nicht geben.",
        });
        anzahl++;

        return anzahl;
    }

    /// <summary>
    /// Welche Phase galt an diesem Tag?
    /// </summary>
    /// <remarks>
    /// <c>Measurement.Stage</c> wird <b>gelesen</b>, nicht gerechnet: die
    /// Beurteilung prüft gegen die Sollwerte dieser Phase. Wer sechs Wochen
    /// stumpf <c>Veg</c> schreibt, lässt Blütewochen gegen Veg-Ziele prüfen —
    /// und die Seite zeigt daneben die gerechnete Phase, sodass der Widerspruch
    /// im Bestand steht.
    /// </remarks>
    private static GrowStage PhaseAm(GrowRun grow, DateTime wann)
    {
        if (grow.FlipDate is not { } flip) return GrowStage.Veg;
        var seitFlip = (wann.Date - flip.Date).Days;
        if (seitFlip < 0) return GrowStage.Veg;
        return seitFlip < 10 ? GrowStage.Transition : GrowStage.Flower;
    }



    private static double Runden(double wert, int stellen) => Math.Round(wert, stellen);

    private static void JournalAnlegen(JournalRepository journal, int growId)
    {
        var eintraege = new (int VorTagen, JournalEntryType Typ, string Titel, string Text)[]
        {
            (64, JournalEntryType.GerminationConfirmed, "Sämling vorbei",
                "Testdaten: echte gezackte Blätter da, ab hier Wachstum."),
            (49, JournalEntryType.Training, "LST angelegt",
                "Testdaten: vier Haupttriebe waagerecht gezogen."),
            (35, JournalEntryType.Action, "Auf 12/12 geflippt",
                "Testdaten: Licht umgestellt, EC auf 1,1 angehoben."),
            (21, JournalEntryType.ReservoirChange, "Wasserwechsel",
                "Testdaten: komplett getauscht, Becken gespült."),
            (9, JournalEntryType.Observation, "Blattränder Woche 5",
                "Testdaten: leichte Spitzenverbrennung an zwei Pflanzen, EC leicht zurückgenommen."),
        };

        foreach (var (vorTagen, typ, titel, text) in eintraege)
        {
            journal.Create(new JournalEntry
            {
                GrowId = growId,
                Title = titel,
                Body = text,
                EntryType = typ,
                Source = ValueOrigin.Manual,
                // Echtes UTC: die Spalte heisst OccurredAtUtc.
                OccurredAtUtc = DateTime.UtcNow.AddDays(-vorTagen),
            });
        }
    }

    private static void AufgabenAnlegen(TaskRepository aufgaben, int growId)
    {
        // Eine ueberfaellige, eine heute faellige, eine kuenftige — sonst zeigt
        // die Aufgabenseite nur einen Zustand.
        var liste = new (string Titel, double StundenBisFaellig, TaskPriority Rang, string? Notiz)[]
        {
            ("pH-Sonde nachkalibrieren", -52, TaskPriority.Critical,
                "Testdaten: driftet seit dem Wasserwechsel."),
            ("EC messen und Addback rechnen", 5, TaskPriority.High, null),
            ("Luftfilter prüfen", 76, TaskPriority.Normal,
                "Testdaten: Vorfilter absaugen, Aktivkohle bleibt."),
        };

        foreach (var (titel, stunden, rang, notiz) in liste)
        {
            aufgaben.Create(new GrowTask
            {
                GrowId = growId,
                Title = titel,
                // Echtes UTC — ein DateTime mit unbestimmter Art gaelte als
                // Ortszeit und verschoebe sich beim Schreiben.
                DueAtUtc = DateTime.UtcNow.AddHours(stunden),
                Priority = rang,
                Status = GrowTaskStatus.Open,
                Notes = notiz,
            });
        }
    }

    private static HardwareItem GeraetMitHistorieAnlegen(HardwareRepository hardware, int zeltId, int growId)
    {
        var geraet = hardware.CreateHardwareItem(new HardwareItem
        {
            Name = "Bluelab pH-Sonde (Testdaten)",
            Category = "Sensor",
            DeviceKind = HardwareDeviceKind.FixedSensor,
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.High,
            CalibrationIntervalDays = 14,
            TentId = zeltId,
            GrowId = growId,
            InstalledAtUtc = DateTime.UtcNow.AddDays(-120),
            Manufacturer = "Bluelab",
            Model = "Guardian Monitor",
            SerialNumber = "BL-2291",
        });

        // Drei durchgefuehrte Kalibrierungen. PerformedAtUtc IMMER selbst
        // setzen: sonst legt ApplyCalibrationDefaults alle drei auf heute, und
        // aus einer Historie wird ein Stapel.
        var verlauf = new (int VorTagen, decimal Vorher, CalibrationResult Ergebnis)[]
        {
            (42, 7.02m, CalibrationResult.Passed),
            (28, 7.09m, CalibrationResult.Passed),
            (14, 7.14m, CalibrationResult.AdjustmentNeeded),
        };

        foreach (var (vorTagen, vorher, ergebnis) in verlauf)
        {
            var wann = DateTime.UtcNow.AddDays(-vorTagen);
            hardware.CreateCalibrationEvent(new CalibrationEvent
            {
                HardwareItemId = geraet.Id,
                Title = "2-Punkt-Kalibrierung pH",
                CalibrationType = CalibrationEventType.Ph,
                Status = CalibrationEventStatus.Completed,
                Result = ergebnis,
                ReferenceSolution = "pH 7,00 / pH 4,00 Pufferlösung",
                ReferenceValue = 7.00m,
                BeforeValue = vorher,
                AfterValue = 7.00m,
                TemperatureC = 20.5m,
                PerformedAtUtc = wann,
                NextDueAtUtc = wann.AddDays(14),
            });
        }

        // NICHT CompleteCalibrationEvent zum Nachtragen benutzen: das legt zu
        // jedem Abschluss selbst einen neuen Termin an, und aus drei Nachtraegen
        // wuerden drei offene Erinnerungen.
        return geraet;
    }

    private static void AlarmregelAnlegen(AlertRuleRepository alarme, int zeltId)
    {
        // ReplaceForTent LOESCHT erst alle Regeln des Zelts — hier unkritisch,
        // weil das Zelt gerade erst entstanden ist. Wer spaeter eine Regel
        // ergaenzt, muss vorher GetForTent lesen.
        alarme.ReplaceForTent(zeltId,
        [
            new TentAlertRule
            {
                TentId = zeltId,
                // Der kanonische Klartext-Schluessel, kein Enum-Name.
                MetricKey = "reservoir-ph",
                MinValue = 5.5,
                MaxValue = 6.3,
                NotifyService = string.Empty,
                Enabled = true,
                CooldownMinutes = 30,
            },
            new TentAlertRule
            {
                TentId = zeltId,
                MetricKey = "reservoir-temp",
                MinValue = 17,
                MaxValue = 22,
                NotifyService = string.Empty,
                Enabled = true,
                CooldownMinutes = 60,
            },
        ]);
    }

    private static void RisikoAnlegen(HardwareRepository hardware, int zeltId, int growId, int geraetId)
    {
        hardware.CreateRiskEvent(new RiskEvent
        {
            Title = "Umwälzpumpe meldet an, zieht aber 0 W (Testdaten)",
            EventType = RiskEventType.PumpOffline,
            Severity = RiskEventSeverity.Critical,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Manual,
            Description = "Testdaten: Zustand „an“, Leistungsaufnahme seit 25 Minuten unter 2 W.",
            HardwareItemId = geraetId,
            TentId = zeltId,
            GrowId = growId,
            StartedAtUtc = DateTime.UtcNow.AddHours(-3),
            LastSeenAtUtc = DateTime.UtcNow,
            RawValue = "0.4",
            // DedupeKey bleibt leer: mit Schluessel wuerde ein zweiter Aufruf
            // das bestehende Ereignis nur fortschreiben statt eines anzulegen.
        });
    }

    private static void GlasAnlegen(CuringRepository aushaerten, int growId)
    {
        var glas = aushaerten.CreateJar(new CuringJar
        {
            GrowId = growId,
            Label = "Glas 1 — obere Blüten (Testdaten)",
            FilledAtUtc = DateTime.UtcNow.AddDays(-9),
            WeightG = 84.5,
            HasHumidityPack = false,
            Notes = "Testdaten: dunkel im Schrank.",
        });

        // Vier Ablesungen, die sich dem Zielfenster 58–62 % nähern. Eine davon
        // OHNE BurpedMinutes: „nachgesehen, nicht gelüftet" ist ein eigener
        // Fall, und ohne ihn kaeme er im Bestand nie vor.
        var ablesungen = new (int VorTagen, double Feuchte, int? Gelueftet, string? Notiz)[]
        {
            (8, 68.0, 15, "Testdaten: riecht noch nach Heu."),
            (6, 64.5, 10, null),
            (4, 61.0, null, "Testdaten: nur nachgesehen."),
            (1, 60.0, 5, null),
        };

        foreach (var (vorTagen, feuchte, gelueftet, notiz) in ablesungen)
        {
            aushaerten.CreateReading(new CuringReading
            {
                JarId = glas,
                ReadAtUtc = DateTime.UtcNow.AddDays(-vorTagen),
                HumidityPercent = feuchte,
                BurpedMinutes = gelueftet,
                Source = CuringReadingSource.Manual,
                Note = notiz,
            });
        }
    }

    /// <summary>
    /// Zwei Dosierpumpen, an die vorhandenen Demo-Schalter gehängt.
    /// </summary>
    /// <remarks>
    /// <para><b>Warum das hier fehlte.</b> <see cref="DemoData"/> legt die
    /// Home-Assistant-Schalter längst an (<c>switch.demo_ph_minus</c> und drei
    /// weitere) und trägt sogar eine Dosier-Historie nach — aber nur für
    /// Pumpen, die schon in der Datenbank stehen
    /// (<c>foreach (var pump in dosing.GetPumps())</c>). Auf einem frischen
    /// Rechner gibt es keine, die Schleife läuft null Mal, und
    /// <c>/dosierung</c> bleibt leer. Gemessen im strengen Lauf: die einzige
    /// von 25 Seiten, die durchfiel.</para>
    ///
    /// <para>Die Automatik bleibt bewusst <b>aus</b>: eine Demo, die von sich
    /// aus Säure dosiert, wäre eine Zumutung — und ohne kalibrierte Sonde
    /// sperrt sie sich ohnehin selbst.</para>
    /// </remarks>
    private static void PumpenAnlegen(DosingRepository dosierung, int zeltId)
    {
        var pumpen = new (string Name, DosingPurpose Zweck, string Schalter, string Mittel, double Konzentration)[]
        {
            ("pH Minus (Testdaten)", DosingPurpose.PhDown, "switch.demo_ph_minus", "Phosphorsäure 30 %", 30),
            ("Nährstoff A (Testdaten)", DosingPurpose.Nutrient, "switch.demo_nutrient_a", "Athena Pro Core", 100),
        };

        foreach (var (name, zweck, schalter, mittel, konzentration) in pumpen)
        {
            dosierung.InsertPump(new DosingPump
            {
                TentId = zeltId,
                Name = name,
                Purpose = zweck,
                HaEntityId = schalter,
                Agent = mittel,
                ConcentrationPercent = konzentration,
                MlPerMinute = 45,
                CostPerLiterEur = 24.90,
                CalibratedAtUtc = DateTime.UtcNow.AddDays(-12),
                TubeChangedAtUtc = DateTime.UtcNow.AddDays(-30),
                // Aus. Siehe oben.
                AutomationEnabled = false,
            });
        }
    }

    private static void AbgeschlossenenGrowAnlegen(
        GrowRepository grows, HarvestRepository ernten, int zeltId, int aufbauId,
        string name, string sorte, string zuechter,
        int vorTagen, int dauerTage, double nass, double trocken)
    {
        // Das Verhaeltnis nass zu trocken liegt bei rund 23 % — der uebliche
        // Bereich fuer ordentlich getrocknetes Material. Bewusst keine
        // Bestleistung: eine Demo mit Traumwerten prueft die Anzeige nicht,
        // sie schmeichelt ihr.
        var geerntet = Tag(vorTagen);
        var gestartet = geerntet.AddDays(-dauerTage);

        var grow = new GrowRun
        {
            TentId = zeltId,
            SystemId = aufbauId,
            Name = name,
            Strain = sorte,
            Breeder = zuechter,
            // Nur Completed und Aborted landen im Archiv. Vergessen heisst:
            // der Lauf steht im Dashboard statt im Archiv.
            Status = GrowStatus.Completed,
            MediumType = MediumType.Hydro,
            HydroStyle = Models.HydroStyle.RDWC,
            IrrigationType = IrrigationType.ActiveHydro,
            Environment = GrowEnvironment.Indoor,
            SeedType = Models.SeedType.Feminized,
            StartMaterial = StartMaterial.Seed,
            EntryPoint = GrowEntryPoint.Germination,
            PlantCount = 4,
            StartDate = gestartet,
            GerminatedAt = gestartet,
            VegStartedAt = gestartet.AddDays(9),
            FlipDate = gestartet.AddDays(37),
            FinishStartedAt = geerntet.AddDays(-9),
            EndDate = geerntet,
            Light = "LED 480 W",
            ReservoirSize = "100 L",
            Notes = "Testdaten: abgeschlossener Lauf.",
        };

        grow.Id = grows.CreateGrow(grow);

        ernten.Create(new HarvestEntry
        {
            GrowId = grow.Id,
            // Reines Ortsdatum, kein UTC-Zeitpunkt.
            HarvestedAt = geerntet,
            WetWeightG = nass,
            DryWeightG = trocken,
            DryDays = 11,
            Rating = 4,
            YieldNotes = $"Testdaten: 4 Pflanzen, rund {Math.Round(trocken / 4)} g je Pflanze.",
            FlavorNotes = "Erdig, leicht süß.",
            EffectNotes = "Ruhig, körperbetont.",
            NugStructure = "Dicht.",
        });
    }
}
