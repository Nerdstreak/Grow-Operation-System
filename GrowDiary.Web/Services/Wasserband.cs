using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Der Arbeitsbereich der Wassertemperatur — an <b>einer</b> Stelle.
/// </summary>
/// <remarks>
/// <para><b>Warum es diese Klasse gibt.</b> Die Zahlen 17, 22, 14 und 24 standen
/// fest verdrahtet in <see cref="MeasurementAssessmentService"/> <i>und</i> in
/// <see cref="DeviationAnalyzerService"/>. Zwei Kopien derselben Zahl laufen
/// auseinander; in diesem Projekt ist das für das EC-Ziel und die
/// physikalischen Grenzen belegt.</para>
///
/// <para><b>Und warum die Untergrenze rechnet statt fest zu stehen.</b> Seit
/// beta.32 plant Grow OS eine Nachtabsenkung, seit beta.52 fährt es sie auch:
/// je Blütewoche ein Grad tiefer, bis zum Finish-Nachtwert des Profils — im
/// Standardprofil 16 °C. Der Arbeitsbereich beginnt aber bei 17 °C. Ab
/// Blütewoche 3 meldete die App damit ihre <b>eigene Regelung</b> als
/// Abweichung.</para>
///
/// <para><b>Beide Zahlen stimmen, sie meinen nur verschiedene Tageszeiten.</b>
/// Die Wissensquelle (SOP-RDWC-CAN-N1, <c>water-temperature-band</c>) sagt:
/// „Unter 18 °C wird die Nährstoffaufnahme gehemmt." Genau <i>das</i> ist bei
/// SKX der Zweck der kalten Nacht — das Tor, durch das die Nährstoffe müssen,
/// wird enger, und der Stress geht in Harz. Was tagsüber ein Mangel wäre, ist
/// nachts die Methode.</para>
///
/// <para>Deshalb zieht der Nachtwert des Profils die Untergrenze mit nach
/// unten — <b>nicht</b> eine hier erfundene Zahl. Gibt es kein Profil, bleibt
/// es beim SOP-Wert. Rückwirkend lässt sich nicht belegen, ob das Licht an war
/// (der Lichtplan hat keine Historie) — dieselbe Einschränkung wie beim VPD,
/// und wie dort steht sie im Urteilstext.</para>
/// </remarks>
public static class Wasserband
{
    /// <summary>Untergrenze des Arbeitsbereichs am Tag (SOP-RDWC-CAN-N1).</summary>
    public const double ArbeitsbereichMinC = 17;

    /// <summary>Obergrenze des Arbeitsbereichs (SOP-RDWC-CAN-N1).</summary>
    public const double ArbeitsbereichMaxC = 22;

    /// <summary>Darunter ist es nicht mehr eine Abweichung, sondern ein Notfall.</summary>
    public const double KritischMinC = 14;

    /// <summary>Darüber kippt der Sauerstoff — der Weg zur Wurzelfäule.</summary>
    public const double KritischMaxC = 24;

    /// <summary>
    /// Die Untergrenze, ab der eine Wassertemperatur beanstandet wird.
    /// </summary>
    /// <param name="ziele">
    /// Die Sollwerte der Phase. <c>null</c> heisst: kein Profil aufgelöst — dann
    /// gilt der SOP-Wert unverändert.
    /// </param>
    /// <remarks>
    /// <see cref="Math.Min(double, double)"/> und nicht etwa der Nachtwert
    /// allein: ein Profil, dessen Nachtwert <i>über</i> 17 °C liegt, soll den
    /// Arbeitsbereich nicht nach oben verengen. Das Band ist eine Grenze, kein
    /// Ziel — das Ziel steht im Profil.
    /// </remarks>
    public static double UntergrenzeC(HydroTargetValues? ziele, double? rampenBodenC = null)
    {
        var vomProfil = ziele?.WaterTempNightC ?? ArbeitsbereichMinC;
        var tiefster = rampenBodenC is { } boden ? Math.Min(vomProfil, boden) : vomProfil;
        return Math.Min(ArbeitsbereichMinC, tiefster);
    }

    /// <summary>
    /// Der Wert, auf den die Absenkrampe tatsächlich fährt — oder <c>null</c>,
    /// wenn für diesen Grow keine läuft.
    /// </summary>
    /// <remarks>
    /// <b>Warum nicht einfach der Nachtwert der aktuellen Phase.</b> Die Rampe
    /// startet beim Blüte-Nachtwert und geht je Woche ein Grad tiefer bis zum
    /// <b>Finish</b>-Nachtwert. In Blütewoche 5 fährt sie also längst auf den
    /// Finish-Wert, während die Messung noch als „Blüte" bewertet wird. Eine
    /// erste Fassung dieser Klasse las nur den Phasenwert — und griff damit
    /// genau im Fall nicht, für den sie gebaut wurde. Am laufenden Stand
    /// nachgesehen, nicht am Diff.
    ///
    /// Läuft keine Rampe, gibt <see cref="NachtabsenkungService.Rechnen"/> einen
    /// leeren Plan zurück und hier kommt <c>null</c> heraus: dann bleibt es beim
    /// SOP-Wert. Das Band weitet sich nur, wenn wirklich jemand dorthin steuert.
    /// </remarks>
    public static double? RampenBodenC(GrowRun grow, HydroTargetValues? bluete, HydroTargetValues? finish)
    {
        var plan = NachtabsenkungService.Rechnen(grow, bluete, finish, grow.NightRampFloorC, DateTime.Now);
        return plan.Wochen.Count > 0 ? plan.Wochen.Min(woche => woche.NachtC) : null;
    }

    /// <summary>Der Satz, der zum Urteil gehört — mit Quelle.</summary>
    public static string Begruendung(HydroTargetValues? ziele, double? rampenBodenC = null)
    {
        var unten = UntergrenzeC(ziele, rampenBodenC);
        if (unten >= ArbeitsbereichMinC)
        {
            return $"Arbeitsbereich {ArbeitsbereichMinC:0}–{ArbeitsbereichMaxC:0} °C, "
                   + "Ziel 19–20 °C (SOP-RDWC-CAN-N1).";
        }

        // Kurz halten: der Satz steht in einer Tabellenzelle des Messprotokolls,
        // neben 92 anderen. Beide Quellen müssen trotzdem drin sein.
        return $"Arbeitsbereich {unten:0.#}–{ArbeitsbereichMaxC:0} °C. Die {ArbeitsbereichMinC:0} °C "
               + $"(SOP-RDWC-CAN-N1) gelten am Tag, nachts fährt deine Absenkung auf {unten:0.#} °C. "
               + "Tag oder Nacht ist rückwirkend nicht belegbar.";
    }
}
