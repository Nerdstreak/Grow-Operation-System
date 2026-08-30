using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Legt die Pflanzen eines frisch angelegten Grows an — eine je Topf.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (28.08.2026).</b> Gemeldet: „Die Grow logik ist noch
/// komisch, der User kann unter grow nur eine Sorte auswählen aber bei den
/// Töpfen für den Grow 4 Stück auswählen, das muss gefixt werden."</para>
///
/// <para>Nachgemessen an der laufenden App: ein Grow mit
/// <c>plantCount: 4, strainId: 1</c> legte <b>null</b> Pflanzen an. Wer vier
/// Töpfe fährt, klickte danach viermal „Pflanze hinzufügen" und wählte jedes
/// Mal dieselbe Sorte — obwohl er sie im Formular schon angegeben hatte. Die
/// Karte „Pflanzen &amp; Sorten" stand derweil auf „keine ist einzeln
/// erfasst", und der Zeltplan zeichnete vier leere Töpfe.</para>
///
/// <para><b>Was hier passiert.</b> Steht im Formular eine Pflanzenzahl,
/// entstehen so viele Pflanzen: auf Topf 1..N, mit der Sorte des Grows, und
/// jede heisst nach ihrem Topf. Danach lässt sich je Topf eine andere Sorte
/// wählen — dafür ist die Karte da. Der Nutzer wollte ausdrücklich, „dass er
/// automatisch durchzählt".</para>
///
/// <para><b>Was NICHT passiert.</b> Beim Bearbeiten eines Grows entsteht
/// nichts. Wer eine Pflanze entfernt hat, will sie nicht beim nächsten
/// Speichern zurück — und ein Aufruf, der beim zweiten Mal nochmal anlegt,
/// verdoppelt den Bestand. Genau diese Klasse Fehler ist am selben Tag
/// gemeldet worden („taucht doppelt auf"), deshalb prüft
/// <c>GrowLegtPflanzenAnTests</c> den zweiten Durchgang eigens.</para>
/// </remarks>
public static class GrowPflanzen
{
    /// <summary>
    /// Der Name einer Pflanze folgt ihrem Topf.
    /// </summary>
    /// <remarks>
    /// Dieselbe Regel wie in <c>GrowPlantsCard.tsx</c>. Ein Topf trägt eine
    /// Pflanze, seine Nummer ist also eindeutig — anders als eine Zählung über
    /// die Menge, die nach jeder Löschung eine Nummer doppelt vergibt.
    /// </remarks>
    public static string Name(int topf) => $"Pflanze {topf}";

    /// <summary>
    /// Nach dem Anlegen eines Grows aufrufen. Tut nichts, wenn der Grow schon
    /// Pflanzen hat oder keine Pflanzenzahl trägt.
    /// </summary>
    /// <returns>Wie viele Pflanzen angelegt wurden.</returns>
    public static int NachAnlage(
        GrowRepository grows,
        SetupRepository setups,
        HydroSetupRepository hydro,
        int growId)
    {
        var grow = grows.GetGrow(growId);
        if (grow?.PlantCount is not > 0)
        {
            return 0;
        }

        // Schon erfasst? Dann gehoert der Bestand dem Nutzer.
        if (setups.GetPlantsByGrow(growId).Count > 0)
        {
            return 0;
        }

        /* Nicht mehr Pflanzen als Toepfe. Die Sperre gibt es beim einzelnen
           Anlegen schon („acht Pflanzen in einem Vier-Topf-System"); sie darf
           nicht dadurch umgangen werden, dass jemand ins Formular 20 schreibt. */
        var toepfe = grow.SystemId is { } systemId
            ? hydro.GetHydroSetup(systemId)?.PotCount
            : null;
        var anzahl = toepfe is > 0
            ? Math.Min(grow.PlantCount.Value, toepfe.Value)
            : grow.PlantCount.Value;

        for (var topf = 1; topf <= anzahl; topf += 1)
        {
            setups.CreatePlant(new PlantInstance
            {
                GrowId = growId,
                StrainId = grow.StrainId,
                Label = Name(topf),
                SiteIndex = topf,
                PlantRole = PlantRole.Production,
                PlantStatus = PlantStatus.Active,
            });
        }

        return anzahl;
    }
}
