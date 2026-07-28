using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>Woher der Messwert stammt, gegen den dosiert wird.</summary>
public enum ReadingSource
{
    None,
    /// <summary>Sensor über Home Assistant.</summary>
    Sensor,
    /// <summary>Von Hand eingetragene Messung.</summary>
    Manual,
}

/// <summary>Woher der Zielwert stammt.</summary>
public enum TargetSource
{
    None,
    /// <summary>Grenzwert, den der Nutzer selbst eingetragen hat.</summary>
    User,
    /// <summary>Phasen-Sollwert aus dem Profil.</summary>
    Profile,
}

/// <summary>
/// Alles, was vor einer Dosis bekannt sein muss — samt Herkunft.
/// </summary>
/// <remarks>
/// Die Herkunft wird mitgeführt, weil sie auf dem Bildschirm stehen muss. „3,4 ml"
/// ohne die Angabe, gegen welchen Wert und welches Ziel gerechnet wurde, ist eine
/// Zahl, der man nur glauben oder nicht glauben kann.
/// </remarks>
public sealed record DosingSituation(
    DosingContext Context,
    double? Target,
    TargetSource TargetFrom,
    ReadingSource ReadingFrom,
    /// <summary>Skaliert die Dosis auf den Fuellstand: halb leer = halbe Menge.</summary>
    double VolumeFactor = 1,
    /// <summary>Ab wann Dosen zaehlen — geschnitten am letzten Wasserwechsel.</summary>
    DateTime? LearnSinceUtc = null)
{
    public static DosingSituation Empty(IReadOnlyList<DoseEvent> dosesToday)
        => new(new DosingContext(null, null, null, false, dosesToday, null),
            null, TargetSource.None, ReadingSource.None);
}

/// <summary>
/// Die Entscheidungen, die keine Datenbank brauchen: welcher Messwert gilt und
/// welches Ziel.
/// </summary>
public static class DosingSituationRules
{
    /// <summary>
    /// Sensor oder Handeintrag — der jüngere gewinnt.
    /// </summary>
    /// <remarks>
    /// Nicht „Sensor schlägt Hand": wer eben von Hand gemessen hat, hat mit
    /// grosser Wahrscheinlichkeit gerade kalibriert oder nachgeschaut, weil der
    /// Sensor zweifelhaft war. Und nicht „Hand schlägt Sensor": eine Messung von
    /// vorgestern gegen einen Sensorwert von vor zwei Minuten wäre grob falsch.
    /// Es zählt, was zuletzt bekannt wurde.
    /// </remarks>
    public static (double? Value, TimeSpan? Age, ReadingSource From) PickReading(
        double? sensorValue, DateTime? sensorAtUtc,
        double? manualValue, DateTime? manualAtUtc,
        DateTime nowUtc)
    {
        var hatSensor = sensorValue is not null && sensorAtUtc is not null;
        var hatHand = manualValue is not null && manualAtUtc is not null;
        if (!hatSensor && !hatHand) return (null, null, ReadingSource.None);

        var nimmHand = hatHand && (!hatSensor || manualAtUtc!.Value > sensorAtUtc!.Value);
        var wert = nimmHand ? manualValue!.Value : sensorValue!.Value;
        var wann = nimmHand ? manualAtUtc!.Value : sensorAtUtc!.Value;

        // Ein Wert aus der Zukunft (verstellte Uhr, verrutschte Zeitzone) darf
        // nicht als „gerade eben" durchgehen und die Altersgrenze aushebeln.
        var alter = nowUtc - wann;
        return (wert, alter < TimeSpan.Zero ? TimeSpan.Zero : alter,
            nimmHand ? ReadingSource.Manual : ReadingSource.Sensor);
    }

    /// <summary>
    /// Der Zielwert: der eingetragene Grenzwert schlägt den Phasen-Sollwert.
    /// </summary>
    /// <remarks>
    /// Dosiert wird auf die Mitte des Bandes, nicht auf seinen Rand. Wer auf die
    /// Grenze dosiert, steht nach der nächsten Drift sofort wieder draussen.
    /// </remarks>
    public static (double? Target, TargetSource From) PickTarget(
        (double? Min, double? Max)? userRange,
        (double Min, double Max)? profileRange)
    {
        if (userRange is { } eigen && Mitte(eigen.Min, eigen.Max) is { } eigeneMitte)
        {
            return (eigeneMitte, TargetSource.User);
        }

        if (profileRange is { } profil)
        {
            return ((profil.Min + profil.Max) / 2, TargetSource.Profile);
        }

        return (null, TargetSource.None);
    }

    /// <summary>
    /// Die Mitte einer Spanne. Eine halbe Grenze ergibt kein Ziel: „nicht über
    /// 6,2" sagt nichts darüber, worauf dosiert werden soll.
    /// </summary>
    private static double? Mitte(double? min, double? max)
        => min is { } untere && max is { } obere ? (untere + obere) / 2 : null;
}
