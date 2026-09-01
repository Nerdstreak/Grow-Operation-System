using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Eine Lichtflanke geht durch einen Schreibfehler nicht verloren.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> <c>Process</c> setzte
/// <c>_lastKnownStateByTent[tentId] = current</c>, <b>bevor</b> die Flanke
/// geschrieben war. Wirft die Ablage — ein Datenbank-Konflikt genügt —, hält
/// die Entprellung den neuen Zustand trotzdem für bekannt. Der nächste Poll
/// sieht keinen Übergang mehr, und die Flanke ist <b>für immer</b> weg.</para>
///
/// <para><b>Was daran hängt.</b> Kein Eintrag in der Flankenhistorie, also ein
/// verzerrter gelernter Zyklus; kein Lichteinbruch-Alarm; und im Zelt-Bild
/// fehlt der Übergang. Ausgerechnet in dem Poll, in dem in der Dunkelphase das
/// Licht angeht — genau dem, für den der Wächter gebaut ist.</para>
///
/// <para>Dieselbe Form wie im <c>PumpWatchNotifier</c> (Kühler-Push) und im
/// <c>TrendWatchRunner</c> (Urlaubswächter): gemerkt wird erst, wenn es
/// wirklich passiert ist.</para>
/// </remarks>
public sealed class LichtflankeUeberlebtEinenSchreibfehlerTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly Tent _zelt;

    public LichtflankeUeberlebtEinenSchreibfehlerTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Lichtflanke_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _zelt = new GrowRepository(_pfade).CreateTent(
            new Tent { Name = "Zelt", TentType = TentType.Production });
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>
    /// Wirft das Schreiben, wird die Flanke beim nächsten Poll nachgeholt.
    /// </summary>
    /// <remarks>
    /// Nachgestellt mit einer Ablage, deren Datenbank für den ersten Versuch
    /// nicht erreichbar ist — der einfachste Weg, ein Werfen zu erzwingen,
    /// ohne die Ablage zu verbiegen.
    /// </remarks>
    [Fact]
    public void EinGescheitertesSchreiben_HoltDieFlankeNach()
    {
        var dienst = new LightStatusTransitionService(new GrowRepository(_pfade));
        var jetzt = DateTime.UtcNow;

        // Erst „aus" — damit der Folgezustand ein echter Uebergang ist.
        dienst.Process(_zelt.Id, Zustand("off"), jetzt.AddMinutes(-2));

        /* Die Zieltabelle kurz beiseite raeumen: dann wirft das Schreiben, ohne
           dass die Datei angefasst werden muss. Ein File.Move scheitert hier —
           SQLite haelt die Datei ueber den Verbindungs-Pool offen. */
        Tabelle("ALTER TABLE LightTransitionEvents RENAME TO LightTransitionEvents_weg;");

        var geworfen = false;
        try
        {
            dienst.Process(_zelt.Id, Zustand("on"), jetzt.AddMinutes(-1));
        }
        catch
        {
            geworfen = true;
        }

        Tabelle("ALTER TABLE LightTransitionEvents_weg RENAME TO LightTransitionEvents;");

        Assert.True(geworfen,
            "Das Schreiben hat gar nicht geworfen — dann stellt dieser Fall den Fehler nicht "
            + "nach und sagt nichts. Laesst sich die Ablage so nicht mehr zum Scheitern "
            + "bringen, braucht es einen anderen Weg.");

        // Der ENTSCHEIDENDE Teil: derselbe Zustand noch einmal. Vorher hielt die
        // Entprellung „an" fuer bekannt und lieferte null — die Flanke war weg.
        var nachgeholt = dienst.Process(_zelt.Id, Zustand("on"), jetzt);

        Assert.True(nachgeholt is not null,
            "Nach einem gescheiterten Schreiben liefert der naechste Poll keine Flanke mehr: "
            + "die Entprellung hat sich den neuen Zustand gemerkt, obwohl nichts geschrieben "
            + "wurde. Der Uebergang fehlt damit fuer immer — in der Historie, im gelernten "
            + "Zyklus und im Lichteinbruch-Alarm.");
        Assert.True(nachgeholt!.Kind == LightTransitionKind.LightOn,
            $"Nachgeholt wurde eine {nachgeholt.Kind}-Flanke statt LightOn.");
    }

    /// <summary>
    /// Und ohne Schreibfehler bleibt die Entprellung scharf.
    /// </summary>
    /// <remarks>
    /// Die Gegenrichtung: wer sich gar nichts mehr merkt, liefert bei jedem
    /// Poll eine Flanke — und die Historie ist Müll.
    /// </remarks>
    [Fact]
    public void OhneSchreibfehler_MeldetDerselbeZustandKeineZweiteFlanke()
    {
        var dienst = new LightStatusTransitionService(new GrowRepository(_pfade));
        var jetzt = DateTime.UtcNow;

        dienst.Process(_zelt.Id, Zustand("off"), jetzt.AddMinutes(-2));
        var erste = dienst.Process(_zelt.Id, Zustand("on"), jetzt.AddMinutes(-1));
        var zweite = dienst.Process(_zelt.Id, Zustand("on"), jetzt);

        Assert.True(erste is not null, "Die erste Einschaltflanke wurde gar nicht erkannt.");
        Assert.True(zweite is null,
            "Derselbe Zustand hat eine zweite Flanke ergeben — die Entprellung greift nicht "
            + "mehr, und die Historie fuellt sich bei jedem Poll.");
    }

    /// <summary>Führt eine Anweisung direkt auf der Testdatenbank aus.</summary>
    private void Tabelle(string sql)
    {
        using var verbindung = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = _pfade.DatabasePath,
            }.ToString());
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    private static HomeAssistantState Zustand(string wert)
        => new() { State = wert, LastChanged = DateTime.UtcNow, LastUpdated = DateTime.UtcNow };
}
