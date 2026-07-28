using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Wann die Wirkung einer Dosis nachgetragen werden darf.
/// </summary>
/// <remarks>
/// Ohne diesen Schritt lernt keine Pumpe je etwas. Bei jeder Dosis wird der Wert
/// davor festgehalten, der Wert danach aber von niemandem — <c>ValueAfter</c>
/// blieb immer null, und die Rechnung, die daraus die Wirkung je Milliliter
/// zieht, überspringt genau solche Zeilen. Gelernt wurde also nie, und der
/// Vorschlag sagte auf ewig „noch keine Erfahrung".
///
/// Der Zeitpunkt ist der heikle Teil. Zu früh: die Lösung ist noch nicht
/// durchmischt, der Messwert zeigt eine Schliere, nicht das Becken. Zu spät: die
/// Pflanzen haben getrunken, es wurde nachgefüllt, vielleicht lief eine zweite
/// Dosis — die Änderung hat dann andere Ursachen, und eingetragen würde daraus
/// eine gelernte Wirkung, hinter der etwas ganz anderes steckt.
/// </remarks>
public static class DosingFollowUp
{
    /// <summary>
    /// Wie viele Mischzeiten lang das Fenster offen bleibt.
    /// </summary>
    /// <remarks>
    /// Eine Mischzeit warten, eine weitere Zeit zum Ablesen. Danach lieber gar
    /// kein Datenpunkt als ein falscher — eine verpasste Dosis kostet ein
    /// bisschen Lerngeschwindigkeit, eine falsch zugeschriebene Wirkung kostet
    /// jede spätere Dosis.
    /// </remarks>
    public const double WindowFactor = 2.0;

    /// <summary>Ist diese Dosis so weit, dass ihre Wirkung eingetragen werden darf?</summary>
    public static bool IsReadyForEffect(DoseEvent dose, int mixingMinutes, DateTime nowUtc)
    {
        if (dose.Outcome != DoseOutcome.Done) return false;
        if (dose.DosedMl <= 0) return false;
        if (dose.ValueAfter is not null) return false;
        if (dose.ValueBefore is null) return false;

        // Im Testbetrieb ist nichts geflossen. Was sich danach geändert hat, hat
        // eine andere Ursache — daraus eine Wirkung abzuleiten wäre erfunden.
        if (dose.Simulated) return false;

        var seit = nowUtc - dose.OccurredAtUtc;
        var mischzeit = TimeSpan.FromMinutes(Math.Max(mixingMinutes, 1));
        return seit >= mischzeit && seit <= mischzeit * WindowFactor;
    }

    /// <summary>
    /// Ist das Fenster für diese Dosis endgültig zu?
    /// </summary>
    /// <remarks>
    /// Getrennt von <see cref="IsReadyForEffect"/>, damit der Aufrufer den
    /// Unterschied zwischen „noch nicht" und „nie mehr" kennt und nicht ewig
    /// dieselben alten Zeilen durchsieht.
    /// </remarks>
    public static bool WindowHasClosed(DoseEvent dose, int mixingMinutes, DateTime nowUtc)
    {
        var mischzeit = TimeSpan.FromMinutes(Math.Max(mixingMinutes, 1));
        return nowUtc - dose.OccurredAtUtc > mischzeit * WindowFactor;
    }
}
