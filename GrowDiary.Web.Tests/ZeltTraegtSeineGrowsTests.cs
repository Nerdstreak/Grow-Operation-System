using System.Text.RegularExpressions;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Ein Zelt aus dem Repository kennt seine laufenden Grows.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> <c>Tent.ActiveGrows</c> wurde von
/// genau zwei MVC-Controllern von Hand nachgezogen
/// (<c>HomeController.cs:41</c>, <c>TentsController.cs:53</c>) — vom Repository
/// nie. Auf jedem anderen Weg war die Liste <b>leer</b>, und sieben Stellen in
/// den Diensten lesen sie:</para>
///
/// <list type="bullet">
///   <item><c>DosingContextBuilder</c> — Volumenfaktor, Wasserwechsel, zwei weitere</item>
///   <item><c>LightWatchService</c> — der Lichteinbruch-Wächter</item>
///   <item><c>GrowDashboardComposer</c> — zweimal</item>
/// </list>
///
/// <para><b>Was das kostete.</b> Der Volumenfaktor blieb immer 1: eine Pumpe
/// fuhr die am vollen Becken gelernte Dosis in ein halb volles. Der
/// Lichteinbruch in der Dunkelphase wurde nie gemeldet, obwohl es dafür einen
/// eigenen, geprüften Wächter gibt. Ein Kommentar im Repo wusste es
/// (<c>TentsController.cs:49</c>: „Tent.ActiveGrows wurde bis hierher von
/// niemandem gefüllt"), der Code daneben nicht.</para>
///
/// <para><b>Warum hier und nicht in den Diensten.</b> Sieben Stellen einzeln zu
/// reparieren hieße, den Fehler siebenmal nicht zu machen. Das Feld gehört dem
/// Zelt; wer ein Zelt aus dem Repository holt, bekommt es gefüllt — genau wie
/// <c>Sensors</c>, das dort seit jeher mitgeladen wird.</para>
/// </remarks>
public sealed class ZeltTraegtSeineGrowsTests : IDisposable
{
    private readonly string _temp;
    private readonly GrowRepository _grows;

    public ZeltTraegtSeineGrowsTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "ZeltGrows_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var pfade = new AppPaths(_temp);
        TestDatabase.Initialize(pfade);
        _grows = new GrowRepository(pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    [Fact]
    public void GetTent_LiefertDieLaufendenGrows()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var laufend = Anlegen(zelt.Id, "Laufend", GrowStatus.Running);
        Anlegen(zelt.Id, "Beendet", GrowStatus.Completed);

        var geholt = _grows.GetTent(zelt.Id);

        Assert.NotNull(geholt);
        Assert.True(geholt!.ActiveGrows.Count == 1,
            $"Das Zelt traegt {geholt.ActiveGrows.Count} laufende Grows statt einem. Sieben Stellen "
            + "in den Diensten lesen diese Liste — ist sie leer, bleibt der Volumenfaktor der "
            + "Dosierung auf 1 und der Lichteinbruch-Waechter kehrt sofort zurueck.");
        Assert.Equal(laufend, geholt.ActiveGrows[0].Id);
    }

    [Fact]
    public void GetTents_LiefertSieFuerJedesZelt()
    {
        var eins = _grows.CreateTent(new Tent { Name = "Eins", TentType = TentType.Production });
        var zwei = _grows.CreateTent(new Tent { Name = "Zwei", TentType = TentType.Production });
        Anlegen(eins.Id, "A", GrowStatus.Running);
        Anlegen(zwei.Id, "B", GrowStatus.Running);
        Anlegen(zwei.Id, "C", GrowStatus.Planning);

        var zelte = _grows.GetTents();

        Assert.True(zelte.Count >= 2, $"Nur {zelte.Count} Zelte — die Pruefung sieht ihre Grundmenge nicht.");
        Assert.Single(zelte.Single(z => z.Id == eins.Id).ActiveGrows);
        Assert.Equal(2, zelte.Single(z => z.Id == zwei.Id).ActiveGrows.Count);
    }

    /// <summary>
    /// Und die Liste enthält wirklich die Felder, an denen die Dienste hängen.
    /// </summary>
    /// <remarks>
    /// Eine Liste mit den richtigen Ids, aber ohne <c>SystemId</c>, würde den
    /// Volumenfaktor genauso auf 1 lassen — der Fehler wäre nur eine Ebene
    /// tiefer gerutscht.
    /// </remarks>
    [Fact]
    public void DieGrowsTragenDieFelder_AnDenenDieDiensteHaengen()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var hydro = new HydroSetupRepository(new AppPaths(_temp), new TentRepository(new AppPaths(_temp)));
        var aufbau = hydro.CreateHydroSetup(new GrowSystem
        {
            TentId = zelt.Id,
            Name = "RDWC",
            HydroStyle = HydroStyle.RDWC.ToString(),
            PotCount = 4,
            PotSizeLiters = 27,
            ReservoirLiters = 100,
            LayoutType = HydroSetupLayoutType.Grid2x2,
            ReservoirPosition = ReservoirPosition.External,
            Status = HydroSetupStatus.Active,
            HasCirculationPump = true,
            HasAirPump = true,
            AirPumpLitersPerHour = 3600,
            AirStoneCount = 4,
        });

        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf",
            TentId = zelt.Id,
            SystemId = aufbau.Id,
            HydroStyle = HydroStyle.RDWC,
            IrrigationType = IrrigationType.ActiveHydro,
            Status = GrowStatus.Running,
            StartDate = DateTime.Today,
        });

        var grow = _grows.GetTent(zelt.Id)!.ActiveGrows.Single();

        Assert.Equal(growId, grow.Id);
        Assert.Equal(aufbau.Id, grow.SystemId);
        Assert.Equal(IrrigationType.ActiveHydro, grow.IrrigationType);
    }

    /// <summary>
    /// Niemand zieht die Liste mehr von Hand nach.
    /// </summary>
    /// <remarks>
    /// <para>Zählung statt Liste: die beiden MVC-Controller taten es, weil das
    /// Repository es nicht tat. Bleibt eine solche Zeile stehen, während das
    /// Repository dasselbe schon geladen hat, ist das eine zweite Wahrheit —
    /// und beim nächsten Umbau läuft sie auseinander.</para>
    ///
    /// <para>Kommentare zählen nicht mit: eine Erwähnung ist keine Verwendung.</para>
    /// </remarks>
    [Fact]
    public void NiemandZiehtDieListeNochVonHandNach()
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.Web");
        var dateien = Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.True(dateien.Count >= 200,
            $"Nur {dateien.Count} .cs-Dateien — die Pruefung sieht ihre Grundmenge nicht.");

        var blockkommentar = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
        var zeilenkommentar = new Regex(@"//.*?$", RegexOptions.Multiline);
        var vonHand = new Regex(@"\.ActiveGrows\s*=(?!=)");

        var verstoesse = new List<string>();
        var gesehen = 0;

        foreach (var datei in dateien)
        {
            var name = Path.GetFileName(datei);
            var code = zeilenkommentar.Replace(blockkommentar.Replace(File.ReadAllText(datei), string.Empty), string.Empty);
            if (!code.Contains(".ActiveGrows", StringComparison.Ordinal)) continue;

            gesehen += 1;
            // Das Repository DARF sie setzen — es ist die Stelle, die sie lädt.
            if (name is "GrowRepository.cs" or "TentRepository.cs") continue;
            if (vonHand.IsMatch(code)) verstoesse.Add(name);
        }

        Assert.True(gesehen >= 2,
            "Niemand liest ActiveGrows mehr — dann misst diese Pruefung nichts. "
            + "Entweder ist das Feld tot und gehoert geloescht, oder der Suchausdruck greift nicht.");

        Assert.True(verstoesse.Count == 0,
            "Diese Dateien fuellen Tent.ActiveGrows von Hand, obwohl das Repository es tut:\n  "
            + string.Join("\n  ", verstoesse)
            + "\n\nZwei Stellen fuer dieselbe Liste laufen auseinander.");
    }

    private int Anlegen(int zeltId, string name, GrowStatus status)
        => _grows.CreateGrow(new GrowRun
        {
            Name = name,
            TentId = zeltId,
            HydroStyle = HydroStyle.RDWC,
            Status = status,
            StartDate = DateTime.Today,
            EndDate = status is GrowStatus.Completed or GrowStatus.Aborted ? DateTime.Today : null,
        });

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
