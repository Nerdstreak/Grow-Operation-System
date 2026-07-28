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
    public const int HistoryHours = 24;

    /// <summary>Wie eine Demo-Entität heißt — überall sichtbar, nie zu verwechseln.</summary>
    public const string EntityPrefix = "demo";

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
        ["reservoir-level"] = (118, 1.5, 24, -0.35, "L", "Demo Wasserstand"),
        ["orp"] = (352, 28, 10, 0, "mV", "Demo ORP"),
        ["dissolved-oxygen"] = (7.6, 0.5, 9, 0, "mg/L", "Demo Sauerstoff"),
    };

    public static IReadOnlyCollection<string> MetricKeys => Shape.Keys;

    /// <summary>
    /// Der Wert einer Messgröße zu einem Zeitpunkt. Rein — dieselbe Zeit
    /// ergibt denselben Wert, auch nach einem Neustart.
    /// </summary>
    public static double? ValueFor(string metricKey, DateTime whenUtc)
    {
        if (!Shape.TryGetValue(metricKey, out var shape)) return null;

        var stunden = whenUtc.Ticks / (double)TimeSpan.TicksPerHour;
        var welle = Math.Sin(2 * Math.PI * (stunden % shape.Hours) / shape.Hours);

        // Die Drift läuft über den Tag und springt um Mitternacht zurück —
        // sonst liefe der pH nach Wochen ins Unmögliche.
        var seitMitternacht = whenUtc.TimeOfDay.TotalHours;
        var wert = shape.Base + shape.Amp * welle + shape.DriftPerHour * seitMitternacht;

        return Math.Round(wert, metricKey == "reservoir-ph" ? 2 : metricKey is "co2" or "ppfd" or "orp" ? 0 : 1);
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
            };
        }

        var lichtKey = TentSensorMetricKeyMap.Resolve(SensorMetricType.LightStatus);
        states[lichtKey] = new HomeAssistantState
        {
            EntityId = $"{EntityPrefix}.licht",
            State = LightOn(nowUtc) ? "on" : "off",
            FriendlyName = "Demo Licht",
            LastChanged = nowUtc,
        };

        return states;
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
        for (var minuten = HistoryHours * 60; minuten > 0; minuten -= 15)
        {
            var zeitpunkt = nowUtc.AddMinutes(-minuten);
            foreach (var (key, shape) in Shape)
            {
                var wert = ValueFor(key, zeitpunkt);
                if (wert is null) continue;
                yield return new TentSensorReading
                {
                    TentId = tentId,
                    MetricKey = key,
                    Value = wert.Value,
                    Unit = shape.Unit,
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
}
