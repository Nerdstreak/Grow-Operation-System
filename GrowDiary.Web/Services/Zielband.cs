using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;

namespace GrowDiary.Web.Services;

/// <summary>
/// Das Zielband eines Grows — dieselbe Antwort für alle, die danach fragen.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Der Schalter „Diese Wochen-Ziele auch
/// auf dem Bildschirm verwenden" (Feedchart) wirkte nur im
/// <see cref="GrowDashboardComposer"/>. Messprotokoll, Diagnose und
/// Empfehlungen rechneten weiter mit dem Profil.</para>
///
/// <para>Ein Grow mit Athena Blended in Blütewoche 4: das Chart nennt EC 2,6,
/// das Profil <c>rdwc-default</c> für Flower 1,0–1,2. Bei gemessenem EC 2,60
/// sagte die Live-Kachel „im Ziel", das Messprotokoll derselben Messung „weit
/// über dem Ziel". Zwei Auskünfte über einen Wert, nebeneinander auf dem
/// Schirm.</para>
///
/// <para><b>Die Kette, in dieser Reihenfolge:</b></para>
/// <list type="number">
///   <item>Profil: Grow → System → Anbaustil (<see cref="SetpointProfileResolver"/>)</item>
///   <item>Die Sollwerte dieses Profils für die Phase</item>
///   <item>Feedchart-Ziele der Woche, <b>wenn der Grow sie will</b></item>
///   <item>Eigene Grenzwerte des Nutzers — die gewinnen immer</item>
/// </list>
///
/// <para>Schritt 4 zuletzt, weil eine selbst eingetragene Grenze keine
/// Empfehlung mehr ist, sondern eine Ansage.</para>
/// </remarks>
public static class Zielband
{
    /// <summary>Das Band für einen Grow in einer Phase; <c>null</c> ohne Profil.</summary>
    /// <param name="targetValues">Die Sollwerte aus dem Wissen.</param>
    /// <param name="wissen">Für die Feedchart-Ziele; <c>null</c> lässt Schritt 3 aus.</param>
    /// <param name="grow">Der Lauf.</param>
    /// <param name="stage">Seine Phase.</param>
    /// <param name="systemProfileId">Das Profil des Hydro-Systems, falls es eins hat.</param>
    /// <param name="eigeneGrenzen">Die Grenzwert-Regeln des Zelts.</param>
    public static HydroTargetValues? FuerGrow(
        TargetValueService targetValues,
        KnowledgeBaseLoader? wissen,
        GrowRun grow,
        GrowStage stage,
        string? systemProfileId,
        IReadOnlyList<TentAlertRule>? eigeneGrenzen)
    {
        var profil = SetpointProfileResolver.Resolve(
            grow.SetpointProfileId, systemProfileId, grow.HydroStyle);

        var band = targetValues.GetTargets(profil.ProfileId, stage);
        if (band is null) return null;

        // Will der Grow die Wochen-Ziele seines Feedcharts, gelten sie — sonst
        // stuende beim Mischen EC 2,6 und auf dem Bildschirm etwas anderes.
        if (wissen is not null
            && MischplanService.ZielSpalteFuerGrow(grow, wissen.NutrientPrograms) is { } chartZiel)
        {
            band = MischplanService.MitFeedchart(band, chartZiel.Spalte);
        }

        return UserTargets.Overlay(band, eigeneGrenzen);
    }
}
