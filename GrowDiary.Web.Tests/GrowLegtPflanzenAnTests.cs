using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Ein Grow mit Pflanzenzahl und Sorte legt seine Pflanzen gleich mit an.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (28.08.2026).</b> Gemeldet: „Die Grow logik ist noch
/// komisch, der User kann unter grow nur eine Sorte auswählen aber bei den
/// Töpfen für den Grow 4 Stück auswählen, das muss gefixt werden."</para>
///
/// <para>Nachgestellt an der laufenden App: ein Grow mit
/// <c>plantCount: 4, strainId: 1</c> legte <b>null</b> Pflanzen an. Wer vier
/// Töpfe fährt, klickte danach viermal „Pflanze hinzufügen" und wählte jedes
/// Mal dieselbe Sorte — obwohl er sie im Formular schon angegeben hatte.</para>
///
/// <para><b>Die Regel.</b> Steht im Formular eine Pflanzenzahl, entstehen so
/// viele Pflanzen: durchnummeriert auf Topf 1..N, mit der Sorte des Grows.
/// Danach lässt sich je Topf eine andere Sorte wählen — dafür ist die Karte
/// „Pflanzen &amp; Sorten" da. Der Nutzer wollte ausdrücklich, „dass er
/// automatisch durchzählt".</para>
///
/// <para><b>Was NICHT passiert.</b> Beim Bearbeiten entstehen keine Pflanzen.
/// Wer eine entfernt hat, will sie nicht beim nächsten Speichern zurück.</para>
/// </remarks>
public sealed class GrowLegtPflanzenAnTests : IDisposable
{
    private readonly string _temp;
    private readonly AppPaths _paths;
    private readonly GrowRepository _grows;
    private readonly SetupRepository _setups;
    private readonly HydroSetupRepository _hydro;

    public GrowLegtPflanzenAnTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "GrowPflanzen_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        _paths = new AppPaths(_temp);
        TestDatabase.Initialize(_paths);
        _grows = new GrowRepository(_paths);
        _setups = new SetupRepository(_paths);
        _hydro = new HydroSetupRepository(_paths, new TentRepository(_paths));
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    [Fact]
    public void PflanzenzahlUndSorte_LegenDiePflanzenAn()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));
        var sorte = _setups.CreateStrain(new Strain { Name = "White Widow" });

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, sorte.Id, pflanzen: 4));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);

        var pflanzen = _setups.GetPlantsByGrow(growId);

        Assert.Equal(4, pflanzen.Count);
        Assert.Equal([1, 2, 3, 4], pflanzen.Select(p => p.SiteIndex).OrderBy(x => x).ToArray());
        Assert.All(pflanzen, p => Assert.Equal(sorte.Id, p.StrainId));
        // Der Name folgt dem Topf — dieselbe Regel wie in der Pflanzen-Karte.
        Assert.All(pflanzen, p => Assert.Equal($"Pflanze {p.SiteIndex}", p.Label));
    }

    [Fact]
    public void MehrPflanzenAlsToepfe_LegtNurSoVieleAnWieHineinpassen()
    {
        /* Die Sperre gibt es schon beim einzelnen Anlegen („acht Pflanzen in
           einem Vier-Topf-System"). Sie darf nicht dadurch umgangen werden,
           dass jemand ins Formular 20 schreibt. */
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));
        var sorte = _setups.CreateStrain(new Strain { Name = "White Widow" });

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, sorte.Id, pflanzen: 20));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);

        Assert.Equal(4, _setups.GetPlantsByGrow(growId).Count);
    }

    [Fact]
    public void OhneSorte_LegtDiePflanzenTrotzdemAn()
    {
        /* Die Sorte ist im Formular freiwillig („— frei eintragen —"). Ohne
           sie sind die Töpfe trotzdem belegt, und genau das soll der Plan
           zeigen. */
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 3));

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, strainId: null, pflanzen: 3));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);

        var pflanzen = _setups.GetPlantsByGrow(growId);
        Assert.Equal(3, pflanzen.Count);
        Assert.All(pflanzen, p => Assert.Null(p.StrainId));
    }

    [Fact]
    public void SchonErfasstePflanzen_BleibenUnberuehrt()
    {
        /* Der zweite Durchgang. Ein Aufruf, der beim zweiten Mal nochmal
           anlegt, verdoppelt den Bestand — und genau diese Klasse Fehler hat
           der Nutzer gerade gemeldet („taucht doppelt auf"). */
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));
        var sorte = _setups.CreateStrain(new Strain { Name = "White Widow" });

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, sorte.Id, pflanzen: 4));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);

        Assert.Equal(4, _setups.GetPlantsByGrow(growId).Count);
    }

    [Fact]
    public void OhnePflanzenzahl_LegtNichtsAn()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, strainId: null, pflanzen: null));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);

        Assert.Empty(_setups.GetPlantsByGrow(growId));
    }

    /// <summary>
    /// Je Topf eine eigene Sorte — schon beim Anlegen.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (31.08.2026).</b> Der Tester hat definiert, was ein
    /// Grow ist: „ein Durchgang in einem RDWC/DWC, der N Pflanzen mit N
    /// verschiedenen Sorten/Phenos beinhalten kann. In dem Grow sollten die
    /// ganzen Sorten im RDWC-System stehen wie bei den Töpfen."</para>
    ///
    /// <para>Das Datenmodell konnte das längst — <c>PlantInstance</c> trägt
    /// <c>StrainId</c> und <c>SiteIndex</c> je Pflanze. Nur das Formular
    /// konnte es nicht: es bot EIN Sortenfeld und schickte den Nutzer per
    /// Hinweis weg („Leg den Grow an und trag danach unter ‚Pflanzen &amp;
    /// Sorten' jede Pflanze ein"). Ein Weg, der aus zwei Schritten besteht,
    /// weil das Formular einen davon nicht kann, ist kein Weg.</para>
    /// </remarks>
    [Fact]
    public void TopfBelegung_LegtJedenTopfMitSeinerEigenenSorteAn()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));
        var widow = _setups.CreateStrain(new Strain { Name = "White Widow" });
        var gorilla = _setups.CreateStrain(new Strain { Name = "Gorilla Glue" });

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, widow.Id, pflanzen: 4));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId,
        [
            new TopfBelegung(1, widow.Id),
            new TopfBelegung(2, widow.Id),
            new TopfBelegung(3, gorilla.Id),
            new TopfBelegung(4, gorilla.Id),
        ]);

        var pflanzen = _setups.GetPlantsByGrow(growId).OrderBy(p => p.SiteIndex).ToList();

        Assert.Equal(4, pflanzen.Count);
        Assert.Equal([widow.Id, widow.Id, gorilla.Id, gorilla.Id], pflanzen.Select(p => p.StrainId).ToArray());
        Assert.Equal([1, 2, 3, 4], pflanzen.Select(p => p.SiteIndex).ToArray());
    }

    /// <summary>
    /// Beim Bearbeiten wird die Sorte GESETZT, nicht neu angelegt — und was
    /// nicht genannt ist, bleibt unberührt.
    /// </summary>
    /// <remarks>
    /// Die Liste ist eine Zuweisung, keine Ersetzung. Wer Topf 3 im Formular
    /// nicht anfasst, verliert seine Pflanze dort nicht — Löschen bleibt der
    /// Karte mit ihrer Rückfrage vorbehalten. Ein Formular, das still Pflanzen
    /// entfernt, ist genau der Datenverlust, den der Tester schon einmal
    /// gemeldet hat.
    /// </remarks>
    [Fact]
    public void TopfBelegung_SetztSortenUndLoeschtNichts()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));
        var widow = _setups.CreateStrain(new Strain { Name = "White Widow" });
        var gorilla = _setups.CreateStrain(new Strain { Name = "Gorilla Glue" });

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, widow.Id, pflanzen: 3));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);
        Assert.Equal(3, _setups.GetPlantsByGrow(growId).Count);

        // Topf 2 bekommt eine andere Sorte, Topf 4 ist neu, Topf 1 und 3 werden
        // nicht genannt.
        GrowPflanzen.SortenSetzen(_grows, _setups, _hydro, growId,
        [
            new TopfBelegung(2, gorilla.Id),
            new TopfBelegung(4, gorilla.Id),
        ]);

        var pflanzen = _setups.GetPlantsByGrow(growId).OrderBy(p => p.SiteIndex).ToList();

        Assert.Equal(4, pflanzen.Count);
        Assert.Equal(widow.Id, pflanzen[0].StrainId);   // Topf 1 unberuehrt
        Assert.Equal(gorilla.Id, pflanzen[1].StrainId); // Topf 2 gewechselt
        Assert.Equal(widow.Id, pflanzen[2].StrainId);   // Topf 3 unberuehrt
        Assert.Equal(gorilla.Id, pflanzen[3].StrainId); // Topf 4 neu
    }

    /// <summary>
    /// Ein gelöschter Grow lässt keine Pflanzen-Leichen zurück.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (01.09.2026).</b> <c>DeleteGrow</c> löschte nur die
    /// Zeile in <c>Grows</c>. Die Pflanzen blieben stehen, mit einem
    /// <c>GrowId</c> auf einen Lauf, den es nicht mehr gibt. Im Testbestand
    /// lagen <b>92</b> solche Leichen, und jeder volle E2E-Lauf legte zwei
    /// weitere dazu. Gefunden vom Prüfer.</para>
    ///
    /// <para><b>Dieselbe Klasse wie bei den Warnungen</b>: für die hat
    /// <c>DeleteGrow</c> seit dem 18.08.2026 einen eigenen Satz, weil sie sonst
    /// „für immer offen" auf der Aufgabenseite standen. Eine Tabelle weiter
    /// stand dieselbe Lücke.</para>
    ///
    /// <para><b>Warum nicht alles.</b> Eine Mutterpflanze gehört dem Aufbau,
    /// nicht dem Durchgang — sie überlebt den Lauf und verliert nur den Bezug
    /// darauf. Wer sie mitlöschte, nähme dem Nutzer seine Mutter mit dem Grow.</para>
    /// </remarks>
    [Fact]
    public void GrowLoeschen_NimmtSeineProduktionsPflanzenMit_UndLaesstDieMutterStehen()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var aufbau = _hydro.CreateHydroSetup(NeuerAufbau(zelt.Id, toepfe: 4));
        var sorte = _setups.CreateStrain(new Strain { Name = "White Widow" });

        var growId = _grows.CreateGrow(NeuerGrow(zelt.Id, aufbau.Id, sorte.Id, pflanzen: 3));
        GrowPflanzen.NachAnlage(_grows, _setups, _hydro, growId);

        var mutter = _setups.CreatePlant(new PlantInstance
        {
            GrowId = growId,
            StrainId = sorte.Id,
            Label = "Mutter",
            PlantRole = PlantRole.Mother,
            PlantStatus = PlantStatus.Active,
        });

        Assert.Equal(4, _setups.GetPlantsByGrow(growId).Count);

        _grows.DeleteGrow(growId);

        // Die drei Produktionspflanzen sind weg — restlos, nicht nur abgehaengt.
        var uebrig = _setups.GetPlants().Where(p => p.GrowId == growId).ToList();
        Assert.True(uebrig.Count == 0,
            $"{uebrig.Count} Pflanzen zeigen noch auf den geloeschten Grow {growId}: "
            + string.Join(", ", uebrig.Select(p => $"{p.Label} ({p.PlantRole})")));

        // Die Mutter lebt weiter, ohne Lauf.
        var nachher = _setups.GetPlant(mutter.Id);
        Assert.True(nachher is not null, "Die Mutterpflanze wurde mit dem Grow geloescht.");
        Assert.Null(nachher!.GrowId);

        // Und keine Produktionspflanze ist bloss abgehaengt worden.
        Assert.DoesNotContain(_setups.GetPlants(), p => p.Label.StartsWith("Pflanze ", StringComparison.Ordinal));
    }

    private static GrowSystem NeuerAufbau(int zeltId, int toepfe) => new()
    {
        TentId = zeltId,
        Name = "RDWC",
        HydroStyle = GrowDiary.Web.Models.HydroStyle.RDWC.ToString(),
        PotCount = toepfe,
        PotSizeLiters = 27,
        ReservoirLiters = 100,
        LayoutType = HydroSetupLayoutType.Grid2x2,
        ReservoirPosition = ReservoirPosition.External,
        Status = HydroSetupStatus.Active,
        HasCirculationPump = true,
        HasAirPump = true,
        AirPumpLitersPerHour = 3600,
        AirStoneCount = toepfe,
    };

    private static GrowRun NeuerGrow(int zeltId, int aufbauId, int? strainId, int? pflanzen) => new()
    {
        Name = "Testlauf",
        TentId = zeltId,
        SystemId = aufbauId,
        HydroStyle = GrowDiary.Web.Models.HydroStyle.RDWC,
        StrainId = strainId,
        PlantCount = pflanzen,
        StartDate = DateTime.Today,
        Status = GrowStatus.Planning,
    };
}
