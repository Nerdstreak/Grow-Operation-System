using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Erfundene, aber plausible Messwerte für den Entwicklungsrechner.
/// </summary>
/// <remarks>
/// Auf dem Entwicklungsrechner gibt es kein Zelt, keine Sonden und kein Home
/// Assistant. Ohne Werte lässt sich dort nichts prüfen: keine Ampelfarben,
/// keine Kurven, keine Alarme, keine Dosier-Vorschläge. Dieser Modus liefert
/// sie — bewegt, damit man Trends sieht, und deterministisch aus der Uhrzeit,
/// damit zwei Abrufe kurz hintereinander zusammenpassen.
///
/// **Nur über die Umgebungsvariable <c>GROW_OS_DEMO</c>.** Bewusst kein
/// Schalter in der Oberfläche: erfundene Messwerte, die im Betrieb angezeigt
/// werden, wären nicht bloß falsch, sondern gefährlich — an ihnen hängen
/// Alarme und die Dosierung. Was man nicht anklicken kann, klickt man auch
/// nicht versehentlich an.
/// </remarks>
public static class DemoData
{
    /// <summary>Aus der Umgebung gelesen, einmal beim Start.</summary>
    public static bool IsEnabled { get; } =
        (Environment.GetEnvironmentVariable("GROW_OS_DEMO") ?? string.Empty).Trim() is "1" or "true" or "TRUE";

    /// <summary>Der Zeitraum, den <see cref="SeedHistory"/> rückwirkend füllt.</summary>
    /// <remarks>
    /// So weit wie der Verlauf reicht — 42 Tage. Vorher waren es <b>24
    /// Stunden</b>, während die Zelt-Historie 7, 14 und 30 Tage anbietet und
    /// auf 14 steht: im Diagramm war das ein Strich am rechten Rand, und
    /// genau das meinte „da ist kein verlauf zu sehen".
    /// </remarks>
    public const int HistoryHours = Demoverlauf.TageRueckwaerts * 24;

    /// <summary>Wie eine Demo-Entität heißt — überall sichtbar, nie zu verwechseln.</summary>
    public const string EntityPrefix = "demo";

    /// <summary>Die Steckdose, an der im Testbestand der Kühler hängt.</summary>
    /// <remarks>
    /// Steht hier und nicht in <see cref="Demobestand"/>: der Bestand trägt sie
    /// ins Zelt ein, <see cref="StatesFor"/> liefert ihren Zustand. Zwei
    /// abgetippte Kennungen wären nach der ersten Umbenennung stumm
    /// auseinandergelaufen — der Kühler stünde dann dauerhaft auf „Zustand
    /// unbekannt", ohne dass irgendwo etwas rot wird.
    /// </remarks>
    public const string KuehlerSteckdose = "switch.demo_kuehler";

    /// <summary>
    /// Ein Wert je Messgröße: Mittelwert, Schwankung, Periode in Stunden und
    /// eine langsame Drift pro Stunde.
    /// </summary>
    /// <remarks>
    /// pH und EC driften nach oben, Füllstand nach unten — so, wie es in einem
    /// laufenden Reservoir wirklich passiert. Das ist kein Schmuck: dadurch
    /// gibt es auf dem Entwicklungsrechner etwas zu korrigieren, und die
    /// Dosierung lässt sich gegen eine echte Abweichung prüfen.
    /// </remarks>
    private static readonly Dictionary<string, (double Base, double Amp, double Hours, double DriftPerHour, string? Unit, string Label)> Shape = new()
    {
        ["temperature"] = (24.4, 1.3, 24, 0, "°C", "Demo Lufttemperatur"),
        ["humidity"] = (58, 6, 24, 0, "%", "Demo Luftfeuchte"),
        ["co2"] = (780, 120, 12, 0, "ppm", "Demo CO₂"),
        ["ppfd"] = (720, 90, 24, 0, "µmol/m²/s", "Demo PPFD"),
        ["reservoir-ph"] = (5.85, 0.06, 6, 0.012, null, "Demo pH"),
        ["reservoir-ec"] = (1.52, 0.04, 8, 0.006, "mS/cm", "Demo EC"),
        ["reservoir-temp"] = (19.6, 0.7, 24, 0, "°C", "Demo Wassertemperatur"),
        // Kein erfundener Liter-Sensor: ein Becken misst entweder Liter ODER
        // Zentimeter. Solange hier beides stand, gewann der Liter-Wert — und
        // der ganze Weg „eTape kalibrieren, dann Liter sehen" war im
        // Vorfuehrmodus unsichtbar.
        // Ein cm-Pegel wie ein eTape — damit sich der Kalibrier-Assistent ohne
        // Hardware durchspielen laesst. Faellt langsam, wie ein trinkendes Becken.
        ["reservoir-level-cm"] = (31, 0.4, 24, -0.09, "cm", "Demo eTape"),
        ["orp"] = (352, 28, 10, 0, "mV", "Demo ORP"),
        ["dissolved-oxygen"] = (7.6, 0.5, 9, 0, "mg/L", "Demo Sauerstoff"),
    };

    public static IReadOnlyCollection<string> MetricKeys => Shape.Keys;

    /// <summary>
    /// Der Wert einer Messgröße zu einem Zeitpunkt. Rein — dieselbe Zeit
    /// ergibt denselben Wert, auch nach einem Neustart.
    /// </summary>
    /// <remarks>
    /// <para>Rechnet nicht selbst, sondern fragt <see cref="Demoverlauf"/>.
    /// Vorher lag hier eine eigene Sinuskurve um einen festen Mittelwert —
    /// und die Messungen im <see cref="Demobestand"/> hatten ihre eigene.
    /// Zwei Kurven für dieselbe Sache heißt: das Diagramm zeigt einen
    /// EC-Sägezahn und das Protokoll daneben behauptet etwas anderes.</para>
    /// <para><b>UTC hinein, Ortszeit hinaus.</b> Die Sensor-Tabelle rechnet in
    /// UTC (Spalte <c>CapturedAtUtc</c>), der Verlauf denkt in Kalendertagen
    /// und Tageszeiten — „nachts kühler" heißt nachts <i>hier</i>. Deshalb
    /// die Umrechnung an genau dieser Stelle.</para>
    /// </remarks>
    public static double? ValueFor(string metricKey, DateTime whenUtc)
        => Demoverlauf.Wert(metricKey, whenUtc.ToLocalTime());
    /// <summary>
    /// Die Tageswerte der zurückliegenden Wochen.
    /// </summary>
    /// <remarks>
    /// <para><b>Warum die zusätzlich nötig sind.</b> Die App bewahrt
    /// <b>sieben Tage</b> Rohablesungen auf; alles Ältere räumt
    /// <c>HomeAssistantSnapshotWorker.CleanupOldReadingsAsync</c> weg und
    /// rollt es vorher zu Tageswerten zusammen. Die Zelt-Historie bietet
    /// aber 7, 14 und 30 Tage an.</para>
    ///
    /// <para>Wer also nur Rohablesungen säte, bekam im 14-Tage-Diagramm
    /// genau einen Punkt — gemessen, nachdem 11 520 gesäte Zeilen auf 312
    /// zusammengeschmolzen waren. Das war der zweite Grund für „da ist kein
    /// verlauf zu sehen", und er lässt sich nicht durch mehr Rohdaten
    /// beheben: die werden ja gerade gelöscht.</para>
    ///
    /// <para>Min, Max und die Bänder kommen aus demselben Verlauf, im
    /// Stundenraster über den Tag gerechnet — sonst behauptete das
    /// Tagesband etwas anderes als die Kurve daneben.</para>
    /// </remarks>
    public static IEnumerable<TentSensorDailyStat> SeedDailyStats(int tentId, DateTime heute)
    {
        // Der jüngste Tag bleibt den Rohablesungen überlassen: für ihn ist
        // die Aggregation noch nicht gelaufen, und zwei Quellen für denselben
        // Tag ergäben einen Sprung im Diagramm.
        for (var tag = Demoverlauf.TageRueckwaerts; tag >= 1; tag--)
        {
            var datum = heute.AddDays(-tag);

            foreach (var key in Demoverlauf.Schluessel)
            {
                var werte = new List<double>();
                for (var stunde = 0; stunde < 24; stunde++)
                {
                    var wert = Demoverlauf.Wert(key, datum.AddHours(stunde));
                    if (wert is { } vorhanden) werte.Add(vorhanden);
                }

                if (werte.Count == 0) continue;
                werte.Sort();

                yield return new TentSensorDailyStat
                {
                    TentId = tentId,
                    MetricKey = key,
                    Date = DateOnly.FromDateTime(datum),
                    Min = werte[0],
                    Max = werte[^1],
                    Median = werte[werte.Count / 2],
                    P5 = werte[(int)(werte.Count * 0.05)],
                    P95 = werte[(int)(werte.Count * 0.95)],
                    Avg = Math.Round(werte.Average(), 2),
                    Count = werte.Count,
                    Unit = Demoverlauf.Einheit(key),
                };
            }
        }
    }

    /// <summary>Das Licht: 18/6, an ab 06:00.</summary>
    public static bool LightOn(DateTime whenUtc) => whenUtc.Hour is >= 6 and < 24;

    /// <summary>Alle Messwerte eines Zelts, so wie Home Assistant sie liefern würde.</summary>
    /// <remarks>
    /// Bewusst ALLE bekannten Messgrößen, nicht nur zugeordnete Sensoren: auf
    /// einem frischen Entwicklungsrechner ist nichts zugeordnet, und dann wäre
    /// der Bildschirm wieder leer — genau das, was dieser Modus beheben soll.
    /// </remarks>
    public static Dictionary<string, HomeAssistantState> StatesFor(DateTime nowUtc)
    {
        var states = new Dictionary<string, HomeAssistantState>();
        foreach (var (key, shape) in Shape)
        {
            var wert = ValueFor(key, nowUtc);
            if (wert is null) continue;
            states[key] = new HomeAssistantState
            {
                EntityId = $"{EntityPrefix}.{key.Replace('-', '_')}",
                State = wert.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FriendlyName = shape.Label,
                UnitOfMeasurement = shape.Unit,
                NumericValue = wert,
                LastChanged = nowUtc,
            LastUpdated = nowUtc,
            };
        }

        // Die Kuehler-Steckdose unter ihrer ENTITAETS-Kennung: der Regler sucht
        // sie genau so, weil dort steht, ob Strom anliegt — der Chiller-Sensor
        // sagt nur, ob das Geraet laeuft.
        var kuehlerAn = Demoverlauf.KuehlerLaeuft(nowUtc.ToLocalTime());
        states[KuehlerSteckdose] = new HomeAssistantState
        {
            EntityId = KuehlerSteckdose,
            State = kuehlerAn ? "on" : "off",
            FriendlyName = "Demo Kühler-Steckdose",
            LastChanged = nowUtc,
            LastUpdated = nowUtc,
        };

        var lichtKey = TentSensorMetricKeyMap.Resolve(SensorMetricType.LightStatus);
        states[lichtKey] = new HomeAssistantState
        {
            EntityId = $"{EntityPrefix}.licht",
            State = LightOn(nowUtc) ? "on" : "off",
            FriendlyName = "Demo Licht",
            LastChanged = nowUtc,
            LastUpdated = nowUtc,
        };

        return states;
    }

    /// <summary>Der Zustand einer einzelnen Entität im Testbestand.</summary>
    /// <remarks>
    /// Bewusst NUR die Kühler-Steckdose: sie ist die einzige Entität, die der
    /// Testbestand unter ihrer eigenen Kennung schaltet. Alles andere geht
    /// über <see cref="StatesFor"/> und dessen Metrik-Kennungen. Wer hier
    /// grosszügig würde, baute dem Betrieb wieder eine Kulisse.
    /// </remarks>
    public static HomeAssistantState? EntityState(string entityId, DateTime nowUtc)
    {
        if (!string.Equals(entityId, KuehlerSteckdose, StringComparison.OrdinalIgnoreCase)) return null;

        var an = Demoverlauf.KuehlerLaeuft(nowUtc.ToLocalTime());
        return new HomeAssistantState
        {
            EntityId = KuehlerSteckdose,
            State = an ? "on" : "off",
            FriendlyName = "Demo Kühler-Steckdose",
            LastChanged = nowUtc,
            LastUpdated = nowUtc,
        };
    }

    /// <summary>
    /// Die Entitätenliste für die Auswahlfelder — Messwerte plus vier
    /// schaltbare Steckdosen, damit sich Dosierpumpen zuordnen lassen.
    /// </summary>
    public static IReadOnlyList<HomeAssistantEntity> Entities(DateTime nowUtc)
    {
        var liste = new List<HomeAssistantEntity>();
        foreach (var (key, shape) in Shape)
        {
            liste.Add(new HomeAssistantEntity
            {
                EntityId = $"sensor.{EntityPrefix}_{key.Replace('-', '_')}",
                FriendlyName = shape.Label,
                State = ValueFor(key, nowUtc)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                UnitOfMeasurement = shape.Unit,
                Domain = "sensor",
            });
        }

        foreach (var (name, label) in new[]
                 {
                     ("ph_minus", "Demo Dosierpumpe pH Minus"),
                     ("ph_plus", "Demo Dosierpumpe pH Plus"),
                     ("nutrient_a", "Demo Dosierpumpe Nährstoff A"),
                     ("nutrient_b", "Demo Dosierpumpe Nährstoff B"),
                 })
        {
            liste.Add(new HomeAssistantEntity
            {
                EntityId = $"switch.{EntityPrefix}_{name}",
                FriendlyName = label,
                State = "off",
                Domain = "switch",
            });
        }

        liste.Add(new HomeAssistantEntity
        {
            EntityId = $"camera.{EntityPrefix}_zelt",
            FriendlyName = "Demo Kamera",
            State = "idle",
            Domain = "camera",
        });

        return liste;
    }

    /// <summary>
    /// Die letzten 24 Stunden je Messgröße, im Viertelstundentakt.
    /// </summary>
    /// <remarks>
    /// Aus demselben Generator wie die Live-Werte — die Kurve endet also genau
    /// dort, wo die Kachel steht. Ohne das wäre der Verlauf erst nach einem Tag
    /// Laufzeit zu sehen, und Kurven, Verlaufsseite und Trend-Wächter liessen
    /// sich auf dem Entwicklungsrechner gar nicht prüfen.
    /// </remarks>
    public static IEnumerable<TentSensorReading> SeedHistory(int tentId, DateTime nowUtc)
    {
        // Zwei Auflösungen. Die letzten zwei Tage im Viertelstundentakt —
        // dort schaut man auf die Kurve und will sie glatt sehen. Davor
        // stündlich: 42 Tage im Viertelstundentakt wären rund 48 000 Zeilen
        // je Zelt, und im 30-Tage-Diagramm sieht man den Unterschied nicht.
        const int FeinBisStunden = 48;

        for (var minuten = HistoryHours * 60; minuten > 0; minuten -= 15)
        {
            var grob = minuten > FeinBisStunden * 60;
            if (grob && minuten % 60 != 0) continue;

            var zeitpunkt = nowUtc.AddMinutes(-minuten);
            foreach (var key in Demoverlauf.Schluessel)
            {
                var wert = ValueFor(key, zeitpunkt);
                if (wert is null) continue;
                yield return new TentSensorReading
                {
                    TentId = tentId,
                    MetricKey = key,
                    Value = wert.Value,
                    Unit = Demoverlauf.Einheit(key),
                    CapturedAtUtc = zeitpunkt,
                };
            }
        }
    }

    /// <summary>
    /// Ein gezeichnetes Kamerabild — mit Uhrzeit, damit man sieht, dass es sich
    /// erneuert, und mit „DEMO" quer darüber, damit es nie für echt gehalten wird.
    /// </summary>
    public static byte[] CameraImage(string entityId, DateTime whenLocal)
    {
        var uhr = whenLocal.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 360" width="640" height="360">
              <rect width="640" height="360" fill="#0b1310"/>
              <g fill="none" stroke="#1f7d49" stroke-width="2" opacity="0.55">
                <circle cx="320" cy="150" r="54"/>
                <path d="M320 96 v108 M266 150 h108 M282 112 l76 76 M358 112 l-76 76"/>
              </g>
              <text x="320" y="252" text-anchor="middle" fill="#52e98c"
                    font-family="monospace" font-size="30" letter-spacing="8">DEMO KAMERA</text>
              <text x="320" y="286" text-anchor="middle" fill="#7d8f86"
                    font-family="monospace" font-size="16">{System.Security.SecurityElement.Escape(entityId)}</text>
              <text x="320" y="316" text-anchor="middle" fill="#7d8f86"
                    font-family="monospace" font-size="20">{uhr}</text>
            </svg>
            """;
        return System.Text.Encoding.UTF8.GetBytes(svg);
    }

    /// <summary>
    /// Die Verbindungseinstellungen, die im Testbetrieb gelten sollen.
    /// </summary>
    /// <remarks>
    /// Damit gilt Home Assistant überall als verbunden — sonst hielten
    /// Watchdog, Live und Dosierung den Rechner für unkonfiguriert und
    /// blockierten, bevor die erfundenen Werte überhaupt gefragt wären.
    /// Die Adresse ist bewusst keine echte: hier geht nie ein Aufruf raus.
    /// </remarks>
    public static HomeAssistantSettings Settings() => new()
    {
        Enabled = true,
        BaseUrl = "http://demo.invalid",
        AccessToken = "demo",
    };

    /// <summary>
    /// Ein paar zurückliegende Dosen mit Wirkung — damit die Pumpe etwas gelernt hat.
    /// </summary>
    /// <remarks>
    /// Ohne das lässt sich Stufe 2 auf dem Entwicklungsrechner gar nicht ansehen.
    /// Gelernt wird aus Dosen mit Wert davor und danach, und simulierte Dosen
    /// lehren bewusst nichts: im Testbetrieb ist nichts geflossen, jede Änderung
    /// danach hat eine andere Ursache. Diese hier sind deshalb als echt
    /// eingetragen — im Testdatenmodus ist ohnehin die ganze Datenbank erfunden,
    /// von den Messwerten an, und der Streifen „Testdaten" steht über jeder Seite.
    ///
    /// Die Wirkung ist bewusst nicht exakt gleich: −0,10 bis −0,12 pH je ml. Eine
    /// perfekt konstante Wirkung gibt es an keinem echten Becken, und ein
    /// Vorschlag, der aus makellosen Zahlen entsteht, prüft nichts.
    /// </remarks>
    public static IEnumerable<DoseEvent> SeedDoses(int pumpId, int tentId, DateTime nowUtc)
    {
        var muster = new[]
        {
            (Stunden: 52.0, Ml: 3.5, Vorher: 6.42, Wirkung: -0.11),
            (Stunden: 34.0, Ml: 2.0, Vorher: 6.28, Wirkung: -0.12),
            (Stunden: 22.0, Ml: 3.0, Vorher: 6.35, Wirkung: -0.10),
            (Stunden: 9.0,  Ml: 2.5, Vorher: 6.31, Wirkung: -0.11),
        };

        foreach (var (stunden, ml, vorher, wirkung) in muster)
        {
            yield return new DoseEvent
            {
                PumpId = pumpId,
                TentId = tentId,
                OccurredAtUtc = nowUtc.AddHours(-stunden),
                Trigger = DoseTrigger.Manual,
                Outcome = DoseOutcome.Done,
                RequestedMl = ml,
                DosedMl = ml,
                SecondsRun = Math.Round(ml / 45.0 * 60, 2),
                ValueBefore = vorher,
                ValueAfter = Math.Round(vorher + ml * wirkung, 3),
                Reason = "Testdaten: zurückliegende Dosis mit gemessener Wirkung.",
                Simulated = false,
            };
        }
    }

    /// <summary>
    /// Eine kalibrierte pH-Sonde — sonst bleibt die Automatik im Testbetrieb gesperrt.
    /// </summary>
    /// <remarks>
    /// Die Automatik verlangt eine Sonde, die kalibriert und nicht überfällig
    /// ist. Das ist keine Formalie: eine driftende Sonde meldet 6,0, während 5,4
    /// im Becken steht, und dosiert wird dann überzeugt in die falsche Richtung.
    /// Auf dem Entwicklungsrechner gibt es keine Sonde, also auch keine
    /// Kalibrierung — und ohne die liesse sich Stufe 3 nirgends durchspielen.
    /// </remarks>
    public static (HardwareItem Probe, CalibrationEvent Calibration) SeedProbe(int tentId, DateTime nowUtc)
    {
        var probe = new HardwareItem
        {
            Name = "Demo pH-Sonde",
            Category = "Sonde",
            DeviceKind = HardwareDeviceKind.FixedSensor,
            MetricType = SensorMetricType.ReservoirPh,
            TentId = tentId,
            Status = HardwareItemStatus.Active,
            CalibrationIntervalDays = 14,
            Notes = "Testdaten — diese Sonde gibt es nicht.",
        };

        var calibration = new CalibrationEvent
        {
            CalibrationType = CalibrationEventType.Ph,
            Status = CalibrationEventStatus.Completed,
            Result = CalibrationResult.Passed,
            Title = "Testdaten: pH-Kalibrierung",
            PerformedAtUtc = nowUtc.AddDays(-3),
            NextDueAtUtc = nowUtc.AddDays(11),
        };

        return (probe, calibration);
    }
}
