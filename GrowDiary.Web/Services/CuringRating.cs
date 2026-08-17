using GrowDiary.Web.Infrastructure;

namespace GrowDiary.Web.Services;

/// <summary>Wie die Feuchte im Glas zu bewerten ist.</summary>
public enum CuringHumidityLevel
{
    /// <summary>Zu trocken — Aroma geht verloren, und zwar endgültig.</summary>
    TooDry,

    /// <summary>Etwas unter dem Fenster.</summary>
    Dry,

    /// <summary>Im Fenster.</summary>
    Good,

    /// <summary>Über dem Fenster — öfter und länger lüften.</summary>
    Damp,

    /// <summary>Schimmelgefahr — jetzt handeln, nicht morgen.</summary>
    MoldRisk,
}

/// <summary>Ein bewerteter Feuchtewert samt Begründung und Quelle.</summary>
public sealed record CuringHumidityVerdict(
    CuringHumidityLevel Level,
    string Summary,
    string Action,
    string Source);

/// <summary>
/// Die Feuchte-Ampel fürs Glas.
/// </summary>
/// <remarks>
/// <para>Gebaut wie die Wasser-Ampel aus beta.30: jede Schwelle nennt ihre
/// Quelle, und was nur eine Faustregel ist, wird als solche benannt.</para>
///
/// <para><b>Warum beide Richtungen zählen.</b> Zu feucht ist die bekannte Gefahr —
/// über 65 % wächst Schimmel im geschlossenen Glas, und was einmal drin ist,
/// nimmt das ganze Glas mit. Zu trocken wird unterschätzt: unter 55 % werden die
/// Terpene spröde und verflüchtigen sich, das Aroma kommt nicht wieder. Anders
/// als zu feucht lässt sich zu trocken kaum reparieren — man kann Feuchte
/// zurückgeben, aber nicht den Duft.</para>
///
/// <para>Die obere Grenze deckt sich mit
/// <see cref="MoldGuard.MaxHumidityPercent"/> für die Cure-Phase: 62 %. Zwei
/// Stellen, dieselbe Zahl — das ist Absicht.</para>
/// </remarks>
public static class CuringRating
{
    public static CuringHumidityVerdict Rate(double humidityPercent)
    {
        var wert = humidityPercent.ToString("0.#", AppCulture.German);

        if (humidityPercent >= 65)
        {
            return new CuringHumidityVerdict(
                CuringHumidityLevel.MoldRisk,
                $"{wert} % — deutlich zu feucht. Im geschlossenen Glas ist das der Bereich, in dem Schimmel wächst.",
                "Glas öffnen, Inhalt auf ein Sieb oder Papier ausbreiten und ein paar Stunden nachtrocknen lassen. Vorher jede Blüte ansehen und riechen: was muffig riecht, kommt nicht zurück ins Glas.",
                CuringSchedule.SourceHumidity);
        }

        if (humidityPercent > CuringSchedule.TargetHumidityMax)
        {
            return new CuringHumidityVerdict(
                CuringHumidityLevel.Damp,
                $"{wert} % — über dem Fenster von {CuringSchedule.TargetHumidityMin:0}–{CuringSchedule.TargetHumidityMax:0} %.",
                "Länger und öfter lüften als der Rhythmus vorgibt, beim Lüften umschichten. Klettert der Wert nach dem Schließen schnell wieder, ist der Kern noch feucht — dann gehört es zurück in die Trocknung.",
                CuringSchedule.SourceHumidity);
        }

        if (humidityPercent >= CuringSchedule.TargetHumidityMin)
        {
            return new CuringHumidityVerdict(
                CuringHumidityLevel.Good,
                $"{wert} % — im Fenster.",
                "Rhythmus beibehalten.",
                CuringSchedule.SourceHumidity);
        }

        if (humidityPercent >= 55)
        {
            return new CuringHumidityVerdict(
                CuringHumidityLevel.Dry,
                $"{wert} % — knapp unter dem Fenster.",
                "Kürzer lüften. Ein Feuchtigkeitsregler (Boveda 62 %) im Glas hält den Wert von selbst.",
                CuringSchedule.SourceHumidity);
        }

        return new CuringHumidityVerdict(
            CuringHumidityLevel.TooDry,
            $"{wert} % — zu trocken. Die Terpene werden spröde und verflüchtigen sich.",
            "Nicht mehr lüften. Ein Feuchtigkeitsregler bringt die Feuchte zurück — das verlorene Aroma kommt damit allerdings nicht wieder.",
            CuringSchedule.SourceHumidity);
    }
}
