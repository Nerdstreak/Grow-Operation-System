namespace GrowDiary.Web.Services;

/// <summary>
/// Der Verlauf des Demo-Grows — <b>eine</b> Quelle für Kurven und Messungen.
///
/// <para><b>Der Anlass.</b> „Die messdaten sind zu statisch, da ist kein
/// verlauf zu sehen." Zwei Gründe, beide echt:</para>
/// <list type="number">
///   <item>Die Sensor-Historie reichte <b>24 Stunden</b> zurück
///   (<c>DemoData.HistoryHours</c>), die Zelt-Historie bietet aber 7, 14 und 30
///   Tage an und steht auf 14. Im Diagramm war das ein Strich am rechten
///   Rand.</item>
///   <item>Die Werte kamen aus einer Sinuskurve um einen festen Mittelwert.
///   Über mehrere Tage sieht so etwas jeden Tag gleich aus — ein Band, keine
///   Entwicklung.</item>
/// </list>
///
/// <para><b>Warum eine gemeinsame Quelle.</b> Es gäbe zwei Wege, das zu
/// beheben: die Kurve für die Sensoren und die Kurve für die Messungen. Zwei
/// Wege heißt zwei Wahrheiten — und dann zeigt das Diagramm einen
/// EC-Sägezahn, während das Protokoll daneben etwas anderes behauptet. Beide
/// lesen deshalb hier.</para>
///
/// <para><b>Was der Verlauf erzählt.</b> Sechs Wochen Blüte mit vier
/// Geschichten:</para>
/// <list type="bullet">
///   <item><b>EC im Sägezahn.</b> Frisch angemischt bei 1,02, dann täglich
///   rund +0,035, weil die Pflanze mehr Wasser zieht als Salz. Am sechsten und
///   siebten Tag über dem Blüteziel (1,00–1,20) — genau dann ist der
///   Wasserwechsel fällig. Das ist der Grund, warum es den Ablauf gibt.</item>
///   <item><b>pH gegen die Dosierung.</b> Steigt täglich um rund 0,1, wird
///   alle drei Tage heruntergezogen. Bleibt im Band — der pH darf im RDWC
///   wandern —, aber man sieht, wer ihn hält.</item>
///   <item><b>Wassertemperatur an der Nachtabsenkung.</b> Jede Blütewoche rund
///   0,35 °C tiefer (die Rampe aus beta.32), dazu der Tag-Nacht-Gang.</item>
///   <item><b>Ein Kühlerausfall</b> von Tag −18 bis −14: das Wasser klettert
///   über den Arbeitsbereich, der gelöste Sauerstoff fällt mit — warmes Wasser
///   hält weniger. Danach Erholung. Erst dadurch hat die Diagnose etwas zu
///   finden und der Verlauf eine Pointe.</item>
/// </list>
///
/// <para><b>Kein Zufall.</b> Jeder Wert folgt aus seinem Zeitpunkt. Ein
/// Bestand, der sich bei jedem Anlegen ändert, macht jede Prüfung, die auf
/// einen Wert zeigt, mal grün und mal rot.</para>
/// </summary>
public static class Demoverlauf
{
    /// <summary>Wie weit der Verlauf zurückreicht.</summary>
    /// <remarks>
    /// 42 Tage — mehr als die 30, die die Zelt-Historie höchstens anzeigt.
    /// </remarks>
    public const int TageRueckwaerts = 42;

    /// <summary>Alle wie viele Tage wird das Becken komplett getauscht?</summary>
    /// <remarks>Sieben — so heißt auch der Ablauf: <c>weekly-water-change</c>.</remarks>
    public const int WasserwechselAlleTage = 7;

    /// <summary>Alle wie viele Tage wird pH nachgestellt und HOCl gegeben?</summary>
    /// <remarks>Zwei bis drei laut SOP-RDWC-CAN-N1 §2.2; hier drei.</remarks>
    public const int DosierAlleTage = 3;

    /// <summary>Der Kühler war von Tag −18 bis −14 aus.</summary>
    public const int StoerungVon = 18;

    /// <summary>Bis hierher — danach ist er repariert.</summary>
    public const int StoerungBis = 14;

    /// <summary>Licht an ab 06:00, 18 Stunden lang.</summary>
    public const int LichtAn = 6;

    /// <summary>Wie viele Tage liegt dieser Zeitpunkt zurück?</summary>
    private static int TageZurueck(DateTime ortszeit) => (DateTime.Today - ortszeit.Date).Days;

    /// <summary>Wie alt ist die Reihe an diesem Tag? 0 am Anfang, 42 heute.</summary>
    private static int Alter(DateTime ortszeit) => Math.Max(0, TageRueckwaerts - TageZurueck(ortszeit));

    /// <summary>War der Kühler an diesem Tag aus?</summary>
    public static bool Stoerung(DateTime ortszeit)
    {
        var zurueck = TageZurueck(ortszeit);
        return zurueck <= StoerungVon && zurueck >= StoerungBis;
    }

    /// <summary>Ist zu dieser Stunde Licht?</summary>
    public static bool LichtBrennt(DateTime ortszeit) => ortszeit.Hour >= LichtAn;

    /// <summary>
    /// Der Tag-Nacht-Gang: −1 um 6 Uhr (kälteste Stunde), +1 um 18 Uhr.
    /// </summary>
    private static double Tagesgang(DateTime ortszeit)
        => Math.Sin(2 * Math.PI * (ortszeit.TimeOfDay.TotalHours - 12) / 24);

    /// <summary>Tage seit dem letzten Wasserwechsel (0 bis 6).</summary>
    public static int SeitWasserwechsel(DateTime ortszeit) => Alter(ortszeit) % WasserwechselAlleTage;

    /// <summary>Tage seit der letzten Dosierung (0 bis 2).</summary>
    public static int SeitDosierung(DateTime ortszeit) => Alter(ortszeit) % DosierAlleTage;

    /// <summary>Die wievielte Blütewoche, als Bruch.</summary>
    private static double Bluetewoche(DateTime ortszeit) => Alter(ortszeit) / 7.0;

    /* ------------------------------------------------------------------ */
    /* Die vier Geschichten                                                */
    /* ------------------------------------------------------------------ */

    /// <summary>EC in mS/cm — Sägezahn über die Woche.</summary>
    public static double Ec(DateTime ortszeit)
        => 1.02 + SeitWasserwechsel(ortszeit) * 0.035 + (1 + Tagesgang(ortszeit)) * 0.006;

    /// <summary>pH — Sägezahn über drei Tage, steigt bei Licht schneller.</summary>
    public static double Ph(DateTime ortszeit)
        => 5.78 + SeitDosierung(ortszeit) * 0.1 + (1 + Tagesgang(ortszeit)) * 0.02;

    /// <summary>Wassertemperatur in °C — Nachtabsenkung, plus Kühlerausfall.</summary>
    public static double WasserTempC(DateTime ortszeit)
        => 20.6 - Bluetewoche(ortszeit) * 0.35
           + (Stoerung(ortszeit) ? 4.4 : 0)
           + Tagesgang(ortszeit) * 0.75;

    /// <summary>Gelöster Sauerstoff in mg/L — fällt mit der Wärme.</summary>
    /// <remarks>
    /// Warmes Wasser hält weniger Sauerstoff. Deshalb faellt er waehrend des
    /// Kuehlerausfalls mit — dieselbe Ursache, zwei sichtbare Kurven.
    /// </remarks>
    public static double SauerstoffMgL(DateTime ortszeit)
        => Stoerung(ortszeit) ? 5.8 : 7.6 - Bluetewoche(ortszeit) * 0.05 - Tagesgang(ortszeit) * 0.15;

    /// <summary>Lufttemperatur in °C.</summary>
    public static double LuftTempC(DateTime ortszeit) => 24.75 + Tagesgang(ortszeit) * 0.75;

    /// <summary>Luftfeuchte in % — sinkt, wenn es waermer wird.</summary>
    /// <remarks>
    /// Zusammen mit der Lufttemperatur ergibt das ein Blatt-VPD um 1,03 bis
    /// 1,14 kPa und liegt damit im Blueteziel 1,00–1,20.
    /// </remarks>
    public static double FeuchtePercent(DateTime ortszeit) => 54 - Tagesgang(ortszeit) * 1.5;

    /// <summary>ORP in mV — faellt zwischen den HOCl-Gaben ab.</summary>
    public static double OrpMv(DateTime ortszeit) => 437 - SeitDosierung(ortszeit) * 19;

    /// <summary>Fuellstand in Litern — faellt ueber die Woche, springt beim Wechsel zurueck.</summary>
    public static double FuellstandLiter(DateTime ortszeit)
        => 96 - SeitWasserwechsel(ortszeit) * 2.4 - (1 + Tagesgang(ortszeit)) * 0.55;

    /// <summary>Derselbe Pegel als Zentimeter — was ein eTape misst.</summary>
    /// <remarks>
    /// Kein zweiter erfundener Sensor: ein Becken misst entweder Liter ODER
    /// Zentimeter. Der cm-Wert ist die Umrechnung ueber die Grundflaeche des
    /// 100-Liter-Beckens, damit sich der Kalibrier-Assistent durchspielen
    /// laesst.
    /// </remarks>
    public static double FuellstandCm(DateTime ortszeit) => FuellstandLiter(ortszeit) / 3.1;

    /// <summary>PPFD — null, solange das Licht aus ist.</summary>
    public static double Ppfd(DateTime ortszeit)
        => LichtBrennt(ortszeit) ? 720 + Tagesgang(ortszeit) * 60 : 0;

    /// <summary>CO₂ in ppm — faellt bei Licht, weil die Pflanze zehrt.</summary>
    public static double Co2Ppm(DateTime ortszeit)
        => LichtBrennt(ortszeit) ? 760 - Tagesgang(ortszeit) * 90 : 900;

    /* ------------------------------------------------------------------ */
    /* Die Bruecke zu den Sensor-Schluesseln                                */
    /* ------------------------------------------------------------------ */

    /// <summary>
    /// Der Wert einer Messgröße zu einem Zeitpunkt — für die gefälschten
    /// Home-Assistant-Sensoren.
    /// </summary>
    /// <param name="metricKey">
    /// Ein Schlüssel aus <see cref="TentSensorMetricKeyMap"/>. Unbekannte
    /// geben <c>null</c>: sie sollen keinen erfundenen Wert bekommen.
    /// </param>
    /// <param name="ortszeit">
    /// <b>Ortszeit</b>, nicht UTC. Der Verlauf ist in Kalendertagen und
    /// Tageszeiten gedacht — „nachts kühler" heißt nachts <i>hier</i>.
    /// </param>
    public static double? Wert(string metricKey, DateTime ortszeit) => metricKey switch
    {
        "temperature" => Math.Round(LuftTempC(ortszeit), 1),
        "humidity" => Math.Round(FeuchtePercent(ortszeit), 0),
        "co2" => Math.Round(Co2Ppm(ortszeit), 0),
        "ppfd" => Math.Round(Ppfd(ortszeit), 0),
        "reservoir-ph" => Math.Round(Ph(ortszeit), 2),
        "reservoir-ec" => Math.Round(Ec(ortszeit), 2),
        "reservoir-temp" => Math.Round(WasserTempC(ortszeit), 1),
        "reservoir-level-cm" => Math.Round(FuellstandCm(ortszeit), 1),
        "orp" => Math.Round(OrpMv(ortszeit), 0),
        "dissolved-oxygen" => Math.Round(SauerstoffMgL(ortszeit), 1),
        _ => null,
    };

    /// <summary>Alle Schlüssel, für die es einen Verlauf gibt.</summary>
    /// <remarks>
    /// Bewusst als Liste und nicht über Reflexion: sie muss zu
    /// <see cref="Wert"/> passen, und ein Enum-Wert ohne Fall dort soll
    /// auffallen statt still null zu liefern.
    /// </remarks>
    public static readonly string[] Schluessel =
    [
        "temperature", "humidity", "co2", "ppfd",
        "reservoir-ph", "reservoir-ec", "reservoir-temp", "reservoir-level-cm",
        "orp", "dissolved-oxygen",
    ];

    /// <summary>Die Einheit zu einem Schlüssel — leer, wo es keine gibt (pH).</summary>
    public static string? Einheit(string metricKey) => metricKey switch
    {
        "temperature" or "reservoir-temp" => "°C",
        "humidity" => "%",
        "co2" => "ppm",
        "ppfd" => "µmol/m²/s",
        "reservoir-ec" => "mS/cm",
        "reservoir-level-cm" => "cm",
        "orp" => "mV",
        "dissolved-oxygen" => "mg/L",
        _ => null,
    };
}
