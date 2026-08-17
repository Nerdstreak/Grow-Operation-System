using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>Wie dringend eine Ablesung am Glas ist.</summary>
public enum CuringDueLevel
{
    /// <summary>Nichts zu tun, der letzte Termin liegt noch nicht lange zurück.</summary>
    Ok,

    /// <summary>Heute dran.</summary>
    Due,

    /// <summary>Überfällig — der Termin ist verstrichen.</summary>
    Overdue,

    /// <summary>Das Glas ist fertig ausgehärtet, es gibt keine Termine mehr.</summary>
    Finished,
}

/// <summary>Was am Glas als Nächstes ansteht.</summary>
public sealed record CuringDuty(
    CuringDueLevel Level,
    /// <summary>Wie viele Tage das Glas schon steht (Tag 1 = Einglastag).</summary>
    int DayInCure,
    /// <summary>Der Abstand zwischen zwei Lüftungen in dieser Woche, in Tagen.</summary>
    int IntervalDays,
    /// <summary>Wie lange gelüftet werden soll, in Minuten (von–bis).</summary>
    int BurpMinutesMin,
    int BurpMinutesMax,
    /// <summary>Wann die nächste Lüftung fällig ist. <c>null</c> beim fertigen Glas.</summary>
    DateTime? NextDueUtc,
    string Text,
    string Source);

/// <summary>
/// Der Lüft-Rhythmus beim Aushärten — welche Woche, welcher Abstand, wie lange.
/// </summary>
/// <remarks>
/// <para>Reine Rechnung ohne Datenbank, damit sie prüfbar ist. Dieselbe Bauweise
/// wie beim Fälligkeits-Wächter aus beta.29 — nur für Gläser statt für
/// Reservoirs.</para>
///
/// <para><b>Die Zahlen und wo sie herkommen.</b> Alles Folgende ist belegt, nichts
/// geschätzt:</para>
/// <list type="bullet">
///   <item>Woche 1: täglich lüften, 5–10 Minuten. Die feuchte Luft aus dem Inneren
///     der Blüten sammelt sich im Glas; wer sie nicht herauslässt, züchtet
///     Schimmel statt Aroma.</item>
///   <item>Woche 2: alle 2–3 Tage, 2–3 Minuten.</item>
///   <item>Woche 3–4: wöchentlich, 1–2 Minuten.</item>
///   <item>Ab Tag 30: nicht mehr nach Kalender, sondern nach Hygrometer.</item>
/// </list>
/// <para>Quelle: atmosiscience.com, „How Long &amp; How to Burp"; Ziel-Fenster
/// 58–62 % rF aus budtrainer.com, „The 62% RH Jar Curing Guide (2026)". Das
/// obere Ende deckt sich mit <see cref="MoldGuard.MaxHumidityPercent"/> für
/// <see cref="GrowStage.Cure"/> — beide sagen 62 %.</para>
///
/// <para><b>Mit Feuchtigkeitsregler im Glas</b> (Boveda, Integra) verschiebt sich
/// die Aufgabe: der Regler hält die Feuchte selbst im Fenster, das Lüften muss
/// dann nur noch die Luft der ersten Tage austauschen. Der Rhythmus wird
/// entsprechend gestreckt — aber nicht abgeschafft, denn ein Regler kann nur
/// Feuchte tauschen, keine Luft.</para>
/// </remarks>
public static class CuringSchedule
{
    /// <summary>Das Fenster, in dem die Feuchte im Glas liegen soll.</summary>
    public const double TargetHumidityMin = 58;
    public const double TargetHumidityMax = 62;

    /// <summary>Ab hier gilt „nach Hygrometer" statt „nach Kalender".</summary>
    public const int HygrometerPhaseFromDay = 30;

    /// <summary>Unter dieser Dauer ist ein Glas nicht ausgehärtet, sondern nur gestanden.</summary>
    public const int MinimumCureDays = 14;

    public const string SourceBurping = "atmosiscience.com — „How Long & How to Burp\"";
    public const string SourceHumidity = "budtrainer.com — „The 62% RH Jar Curing Guide (2026)\"";

    /// <summary>
    /// Was an einem Glas jetzt ansteht.
    /// </summary>
    /// <param name="jar">Das Glas.</param>
    /// <param name="lastBurpUtc">Wann zuletzt gelüftet wurde; <c>null</c> = noch nie.</param>
    /// <param name="nowUtc">Jetzt.</param>
    public static CuringDuty Evaluate(CuringJar jar, DateTime? lastBurpUtc, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(jar);

        if (jar.FinishedAtUtc is not null)
        {
            return new CuringDuty(
                CuringDueLevel.Finished,
                DayInCure: Tage(jar.FilledAtUtc, jar.FinishedAtUtc.Value),
                IntervalDays: 0,
                BurpMinutesMin: 0,
                BurpMinutesMax: 0,
                NextDueUtc: null,
                Text: "Fertig ausgehärtet.",
                Source: SourceBurping);
        }

        var tag = Tage(jar.FilledAtUtc, nowUtc);
        var (intervall, minutenVon, minutenBis, text) = Fenster(tag, jar.HasHumidityPack);

        // Ab Tag 30 gibt der Kalender nichts mehr her — dann entscheidet das
        // Hygrometer. Eine Frist zu erfinden waere hier eine Scheingenauigkeit.
        if (tag >= HygrometerPhaseFromDay)
        {
            return new CuringDuty(
                CuringDueLevel.Ok, tag, intervall, minutenVon, minutenBis,
                NextDueUtc: null, Text: text, Source: SourceBurping);
        }

        // Ohne eine einzige Lüftung zählt der Einglaszeitpunkt als Ausgangspunkt.
        var seit = lastBurpUtc ?? jar.FilledAtUtc;
        var faellig = seit.AddDays(intervall);
        var level = nowUtc >= faellig.AddDays(1) ? CuringDueLevel.Overdue
            : nowUtc >= faellig ? CuringDueLevel.Due
            : CuringDueLevel.Ok;

        return new CuringDuty(level, tag, intervall, minutenVon, minutenBis, faellig, text, SourceBurping);
    }

    /// <summary>
    /// Das Rhythmus-Fenster für einen Tag im Aushärten.
    /// </summary>
    private static (int Intervall, int MinutenVon, int MinutenBis, string Text) Fenster(int tag, bool mitRegler)
    {
        // Mit Feuchtigkeitsregler wird der Abstand verdoppelt: der Regler haelt
        // die Feuchte, das Lueften tauscht nur noch die Luft.
        var faktor = mitRegler ? 2 : 1;
        var zusatz = mitRegler ? " (mit Feuchtigkeitsregler gestreckt)" : string.Empty;

        if (tag >= HygrometerPhaseFromDay)
        {
            return (0, 0, 0,
                $"Ab Tag {HygrometerPhaseFromDay} nicht mehr nach Kalender lüften, sondern nach Hygrometer: "
                + $"bleibt das Glas einen ganzen Tag zwischen {TargetHumidityMin:0} und {TargetHumidityMax:0} %, ohne zu klettern, ist die tägliche Phase vorbei.");
        }

        if (tag <= 7)
        {
            return (1 * faktor, 5, 10,
                $"Woche 1: täglich 5–10 Minuten lüften und dabei umschichten — unten liegt es feuchter als oben.{zusatz}");
        }

        if (tag <= 14)
        {
            return (2 * faktor, 2, 3, $"Woche 2: alle 2–3 Tage 2–3 Minuten lüften.{zusatz}");
        }

        return (7 * faktor, 1, 2, $"Woche 3–4: wöchentlich 1–2 Minuten lüften.{zusatz}");
    }

    /// <summary>Tag 1 ist der Einglastag — so zählt man vor dem Glas, nicht ab null.</summary>
    private static int Tage(DateTime von, DateTime bis)
        => Math.Max(1, (int)Math.Floor((bis.Date - von.Date).TotalDays) + 1);
}
