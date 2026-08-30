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
