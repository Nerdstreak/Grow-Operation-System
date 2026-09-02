using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Was über einen Grow protokolliert wird, lässt sich auch wieder lesen.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>AuditEntries</c> war
/// <b>schreib-only</b>. Vier Controller schrieben hinein — Grows, Messungen,
/// Journal, Abläufe —, es gab einen Index
/// (<c>IX_AuditEntries_GrowId_CreatedAtUtc</c>) für eine Abfrage, die niemand
/// stellte, und <c>AuditRepository</c> hatte genau eine öffentliche Methode:
/// <c>Add</c>. Kein <c>SELECT</c>, keine Route, kein Aufruf in der
/// Oberfläche.</para>
///
/// <para><b>Was das heisst.</b> Die App sammelt seit Monaten die Geschichte
/// jedes Grows — „Grow angelegt", „Messung erfasst", „Flip 12/12" mit Zeitpunkt
/// — und niemand kommt daran. Ein Protokoll, das man nicht lesen kann, ist kein
/// Protokoll, sondern Schreibarbeit bei jeder Änderung.</para>
///
/// <para>Es geht auch nicht um eine Kleinigkeit: genau diese Zeilen beantworten
/// „wann habe ich eigentlich geflippt" und „wer hat den Wert geändert", wenn
/// jemand hinterher sucht.</para>
/// </remarks>
public sealed class DieGrowChronikIstLesbarTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;
    private readonly AuditRepository _chronik;
    private readonly int _growId;
    private readonly int _andererGrow;

    public DieGrowChronikIstLesbarTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Chronik_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
        _chronik = new AuditRepository(_pfade);

        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        _growId = Grow(zelt.Id, "Erster");
        _andererGrow = Grow(zelt.Id, "Zweiter");
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Was hineingeschrieben wurde, kommt heraus.</summary>
    [Fact]
    public void WasHineingeschriebenWurde_KommtHeraus()
    {
        _chronik.Add(Eintrag(_growId, "Grow angelegt", "Grow 'Erster' wurde erstellt"));
        _chronik.Add(Eintrag(_growId, "Flip 12/12", "Bluete gestartet"));

        var zeilen = _chronik.GetForGrow(_growId);

        Assert.True(zeilen.Count == 2,
            $"Zwei Eintraege geschrieben, {zeilen.Count} gelesen. Ein Protokoll, das man nicht "
            + "lesen kann, ist kein Protokoll, sondern Schreibarbeit bei jeder Aenderung.");
        Assert.Contains(zeilen, z => z.Action == "Flip 12/12");
        Assert.Contains(zeilen, z => z.Summary.Contains("Erster", StringComparison.Ordinal));
    }

    /// <summary>Das Neueste steht oben.</summary>
    /// <remarks>
    /// Wer nachsieht, sucht fast immer das Letzte. Der Index
    /// <c>IX_AuditEntries_GrowId_CreatedAtUtc</c> ist genau dafür angelegt — er
    /// war bis heute für eine Abfrage da, die niemand stellte.
    /// </remarks>
    [Fact]
    public void DasNeuesteStehtOben()
    {
        _chronik.Add(Eintrag(_growId, "zuerst", "a"));
        Thread.Sleep(5);
        _chronik.Add(Eintrag(_growId, "danach", "b"));

        Assert.True(_chronik.GetForGrow(_growId)[0].Action == "danach",
            "Der aelteste Eintrag steht oben. Wer nachsieht, sucht fast immer das Letzte.");
    }

    /// <summary>Ein anderer Grow bekommt seine eigene Geschichte.</summary>
    /// <remarks>
    /// Die Gegenrichtung: käme alles zurück, stünde bei einem Grow, was in
    /// einem anderen passiert ist — und das ist schlimmer als gar keine
    /// Chronik.
    /// </remarks>
    [Fact]
    public void EinAndererGrow_BekommtSeineEigeneGeschichte()
    {
        _chronik.Add(Eintrag(_growId, "im ersten", "a"));
        _chronik.Add(Eintrag(_andererGrow, "im zweiten", "b"));

        var erste = _chronik.GetForGrow(_growId);

        Assert.True(erste.Count == 1 && erste[0].Action == "im ersten",
            "Beim ersten Grow steht, was im zweiten passiert ist: "
            + string.Join(", ", erste.Select(z => z.Action)));
    }

    /// <summary>Ohne Eintraege eine leere Liste — keine Ausnahme.</summary>
    [Fact]
    public void OhneEintraege_EineLeereListe()
    {
        Assert.Empty(_chronik.GetForGrow(_growId));
    }

    /// <summary>Die Menge ist begrenzt — eine Chronik wächst unbegrenzt.</summary>
    /// <remarks>
    /// Nach einem Jahr hat ein Grow hunderte Zeilen. Wer nachsieht, will die
    /// letzten; alles auf einmal zu liefern macht die Antwort mit der Zeit
    /// immer langsamer, ohne dass es jemandem auffällt.
    /// </remarks>
    [Fact]
    public void DieMengeIstBegrenzt()
    {
        for (var i = 0; i < 30; i += 1)
        {
            _chronik.Add(Eintrag(_growId, "Nummer " + i, "x"));
        }

        Assert.True(_chronik.GetForGrow(_growId, limit: 10).Count == 10,
            "Die Grenze wird nicht beachtet. Nach einem Jahr hat ein Grow hunderte Zeilen, "
            + "und die Antwort wird still immer langsamer.");
    }

    // ------------------------------------------------------------------ Hilfe

    private int Grow(int zeltId, string name)
        => _grows.CreateGrow(new GrowRun
        {
            Name = name, TentId = zeltId, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today.AddDays(-20),
        });

    private static AuditEntry Eintrag(int growId, string aktion, string zusammenfassung)
        => new() { GrowId = growId, EntityType = "Grow", Action = aktion, Summary = zusammenfassung };
}
