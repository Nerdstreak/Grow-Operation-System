using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;

namespace GrowDiary.Web.Services;

/// <summary>
/// Wie es um den Wasserwechsel eines Grows steht — eine Zahl, ein Zustand.
/// </summary>
/// <param name="ZuletztUtc">Der jüngste Beleg, oder <c>null</c>, wenn es keinen gibt.</param>
/// <param name="TageSeit">Tage seit dem letzten Wechsel; <c>null</c> ohne Beleg.</param>
/// <param name="IntervallTage">Was der Ablauf vorsieht.</param>
/// <param name="WarnungAbTagen">Ab hier ist er fällig.</param>
/// <param name="KritischAbTagen">Ab hier ist er überfällig.</param>
/// <param name="Zustand">unbekannt · frisch · faellig · ueberfaellig</param>
public sealed record WasserwechselStand(
    DateTime? ZuletztUtc,
    int? TageSeit,
    int IntervallTage,
    int WarnungAbTagen,
    int KritischAbTagen,
    string Zustand);

/// <summary>
/// Baut den Stand des Wasserwechsels — aus denselben Quellen, aus denen die
/// Mahnung kommt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (31.08.2026).</b> Der Nutzer hat einen Wasserwechsel
/// gemacht und wollte ihn eintragen — und fand die Stelle nicht. Der Weg dahin
/// war „Addback" im Menü, scrollen, dritter Abschnitt. Das Wort „Wasserwechsel"
/// stand auf dem ganzen Weg nirgends.</para>
///
/// <para><b>Warum ein Dienst und keine Rechnung in der Oberfläche.</b> Die
/// Zahl „vor N Tagen" steht künftig an vier Stellen: auf der eigenen Seite, in
/// der Mahnung der Aufgaben, in den Risiko-Karten und im Trend. Gerechnet wird
/// sie einmal — hier. Dieselbe Zahl zweimal zu rechnen ist in diesem Projekt
/// schon dreimal auseinandergelaufen (EC-Ziel, physikalische Grenzen,
/// Sauerstoff-Schwelle).</para>
///
/// <para><b>Die Schwellen kommen aus dem Wissen</b>, nicht aus dem Kopf: der
/// Ablauf <c>weekly-water-change</c> trägt seinen Rhythmus selbst („alle 7
/// Tage, Warnung nach 8, kritisch nach 10"). Genau die Zahlen benutzt auch
/// <see cref="SopDueService"/> — deshalb können Seite und Mahnung nicht
/// verschiedene Auskünfte geben.</para>
/// </remarks>
public sealed class WasserwechselStandService
{
    /// <summary>Der Ablauf, der den Rhythmus trägt.</summary>
    public const string SopId = "weekly-water-change";

    /// <summary>
    /// Rückfall, falls der Ablauf im Wissen fehlt — die Werte der
    /// mitgelieferten Datei, damit die Seite auch dann etwas Sinnvolles sagt.
    /// </summary>
    private const int StandardIntervall = 7;
    private const int StandardWarnung = 8;
    private const int StandardKritisch = 10;

    private readonly GrowRepository _grows;
    private readonly KnowledgeBaseLoader _wissen;

    public WasserwechselStandService(GrowRepository grows, KnowledgeBaseLoader wissen)
    {
        _grows = grows;
        _wissen = wissen;
    }

    /// <summary>Der Stand für einen Grow; <c>null</c>, wenn es ihn nicht gibt.</summary>
    public WasserwechselStand? Fuer(int growId)
    {
        if (_grows.GetGrow(growId) is null) return null;

        var zuletzt = Wasserwechsel.ZuletztUtc(
            _grows.GetMeasurementsForGrow(growId),
            _grows.GetChangeoutsForGrow(growId));

        var (intervall, warnung, kritisch) = Zeitplan();

        // Ortszeit gegen Ortszeit: „vor N Tagen" ist eine Kalenderauskunft,
        // keine Stundenrechnung. Wer hier UTC gegen DateTime.Today hält,
        // verschiebt den Wechsel um bis zu zwei Stunden — im Sommer genug, um
        // einen Tag zu kippen.
        int? tage = zuletzt is { } wann
            ? Math.Max(0, (DateTime.Today - wann.ToLocalTime().Date).Days)
            : null;

        var zustand = tage is not { } seit ? "unbekannt"
            : seit >= kritisch ? "ueberfaellig"
            : seit >= warnung ? "faellig"
            : "frisch";

        return new WasserwechselStand(zuletzt, tage, intervall, warnung, kritisch, zustand);
    }

    private (int Intervall, int Warnung, int Kritisch) Zeitplan()
    {
        var sop = _wissen.Sops.FirstOrDefault(s =>
            string.Equals(s.Id, SopId, StringComparison.OrdinalIgnoreCase));

        var plan = sop?.Triggers?.FirstOrDefault(t =>
            string.Equals(t.Type, "Schedule", StringComparison.OrdinalIgnoreCase) && t.IntervalDays is > 0);

        if (plan?.IntervalDays is not { } intervall) return (StandardIntervall, StandardWarnung, StandardKritisch);

        return (intervall, plan.WarningAfterDays ?? intervall + 1, plan.CriticalAfterDays ?? intervall + 3);
    }
}
