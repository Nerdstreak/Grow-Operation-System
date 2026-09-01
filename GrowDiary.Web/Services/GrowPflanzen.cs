using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Ein Topf und die Sorte, die darin steht.
/// </summary>
/// <param name="Topf">Die Topfnummer, ab 1 — dieselbe wie <c>PlantInstance.SiteIndex</c>.</param>
/// <param name="StrainId">Die Sorte; <c>null</c> heisst „ohne Sorte".</param>
/// <remarks>
/// <b>Der Anlass (31.08.2026).</b> Der Tester hat definiert, was ein Grow ist:
/// ein Durchgang im RDWC mit N Pflanzen und N Sorten. Genau das steht hier —
/// die Belegung des Systems, Topf für Topf.
/// </remarks>
public readonly record struct TopfBelegung(int Topf, int? StrainId);

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
        int growId,
        IReadOnlyList<TopfBelegung>? belegung = null)
    {
        var grow = grows.GetGrow(growId);
        if (grow is null)
        {
            return 0;
        }

        // Schon erfasst? Dann gehoert der Bestand dem Nutzer.
        if (setups.GetPlantsByGrow(growId).Count > 0)
        {
            return 0;
        }

        var toepfe = Topfzahl(grow, hydro);

        /* Der Nutzer hat je Topf eine Sorte gewaehlt.

           Bis zum 31.08.2026 ging das nicht: das Formular bot EIN Sortenfeld
           und schickte den Nutzer per Hinweis auf die Karte "Pflanzen &
           Sorten". Der Tester hat es dann ausgeschrieben - ein Grow ist ein
           Durchgang mit N Pflanzen und N Sorten, und die gehoeren dorthin, wo
           auch die Toepfe stehen. */
        if (belegung is { Count: > 0 })
        {
            var gueltig = belegung
                .Where(eintrag => eintrag.Topf >= 1 && (toepfe is not > 0 || eintrag.Topf <= toepfe))
                .GroupBy(eintrag => eintrag.Topf)
                .Select(gruppe => gruppe.Last())
                .OrderBy(eintrag => eintrag.Topf)
                .ToList();

            foreach (var eintrag in gueltig)
            {
                Anlegen(setups, growId, eintrag.Topf, eintrag.StrainId ?? grow.StrainId);
            }

            return gueltig.Count;
        }

        if (grow.PlantCount is not > 0)
        {
            return 0;
        }

        /* Nicht mehr Pflanzen als Toepfe. Die Sperre gibt es beim einzelnen
           Anlegen schon ("acht Pflanzen in einem Vier-Topf-System"); sie darf
           nicht dadurch umgangen werden, dass jemand ins Formular 20 schreibt. */
        var anzahl = toepfe is > 0
            ? Math.Min(grow.PlantCount.Value, toepfe.Value)
            : grow.PlantCount.Value;

        for (var topf = 1; topf <= anzahl; topf += 1)
        {
            Anlegen(setups, growId, topf, grow.StrainId);
        }

        return anzahl;
    }

    /// <summary>
    /// Beim BEARBEITEN: setzt die Sorten der genannten Töpfe.
    /// </summary>
    /// <remarks>
    /// <para><b>Eine Zuweisung, keine Ersetzung.</b> Was nicht in der Liste
    /// steht, bleibt unberührt — auch Pflanzen in Töpfen, die das Formular
    /// nicht nennt. Löschen bleibt der Karte „Pflanzen &amp; Sorten"
    /// vorbehalten, die vorher nachfragt: ein Formular, das still Pflanzen
    /// entfernt, ist genau der Datenverlust, den der Tester schon einmal
    /// gemeldet hat.</para>
    ///
    /// <para><b>Ein leerer Topf wird gefüllt</b>, ein belegter bekommt nur
    /// seine Sorte gewechselt — der Name der vorhandenen Pflanze bleibt, denn
    /// er hängt am Topf und der ändert sich hier nicht.</para>
    /// </remarks>
    /// <returns>Wie viele Pflanzen angelegt oder geändert wurden.</returns>
    public static int SortenSetzen(
        GrowRepository grows,
        SetupRepository setups,
        HydroSetupRepository hydro,
        int growId,
        IReadOnlyList<TopfBelegung> belegung)
    {
        var grow = grows.GetGrow(growId);
        if (grow is null || belegung.Count == 0)
        {
            return 0;
        }

        var toepfe = Topfzahl(grow, hydro);
        var vorhanden = setups.GetPlantsByGrow(growId);
        var geaendert = 0;

        foreach (var eintrag in belegung.GroupBy(e => e.Topf).Select(g => g.Last()).OrderBy(e => e.Topf))
        {
            if (eintrag.Topf < 1 || (toepfe is > 0 && eintrag.Topf > toepfe))
            {
                continue;
            }

            var pflanze = vorhanden.FirstOrDefault(p => p.SiteIndex == eintrag.Topf);
            if (pflanze is null)
            {
                Anlegen(setups, growId, eintrag.Topf, eintrag.StrainId ?? grow.StrainId);
                geaendert += 1;
                continue;
            }

            if (pflanze.StrainId == eintrag.StrainId)
            {
                continue;
            }

            pflanze.StrainId = eintrag.StrainId;
            setups.UpdatePlant(pflanze);
            geaendert += 1;
        }

        return geaendert;
    }

    /// <summary>Wieviele Töpfe das System des Grows hat; <c>null</c> ohne System.</summary>
    private static int? Topfzahl(GrowRun grow, HydroSetupRepository hydro)
        => grow.SystemId is { } systemId ? hydro.GetHydroSetup(systemId)?.PotCount : null;

    private static void Anlegen(SetupRepository setups, int growId, int topf, int? strainId)
        => setups.CreatePlant(new PlantInstance
        {
            GrowId = growId,
            StrainId = strainId,
            Label = Name(topf),
            SiteIndex = topf,
            PlantRole = PlantRole.Production,
            PlantStatus = PlantStatus.Active,
        });
}
