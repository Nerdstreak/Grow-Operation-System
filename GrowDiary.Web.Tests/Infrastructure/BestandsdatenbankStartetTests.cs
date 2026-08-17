using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Eine Datenbank, die es schon gibt, muss das Update überstehen.
/// </summary>
/// <remarks>
/// <para><b>Der Fehler, der diesen Test erzwungen hat.</b> In beta.43 bekamen
/// die Fotos eine Spalte <c>SymptomId</c> und einen Index darauf. Der Index
/// stand im Kern-Schema — das läuft, <i>bevor</i> <c>EnsureColumn</c> die
/// Spalte in eine bestehende Datenbank einfügt. Ergebnis:
/// „SQLite Error 1: no such column: SymptomId", und die Anwendung startete
/// nicht mehr.</para>
///
/// <para>Bei einer <b>frischen</b> Datenbank fällt das nie auf: dort steht die
/// Spalte im <c>CREATE TABLE</c>, der Index findet sie, alles läuft. Genau
/// deshalb war die gesamte Testsuite grün — jeder Test legt eine neue
/// Datenbank an. Aufgefallen ist es erst beim wirklichen Start gegen die
/// vorhandene Demo-Datenbank.</para>
///
/// <para>Dieser Test baut deshalb eine <i>alte</i> Datenbank nach: Tabellen wie
/// vor der Änderung, ohne die neuen Spalten. Läuft der Initialisierer darüber,
/// muss er sie ergänzen, statt zu scheitern. Es ist die zweite Auflage
/// derselben Falle — für die Grow-Indizes gibt es <c>GrowIndexSql</c> aus
/// genau diesem Grund schon länger.</para>
/// </remarks>
public sealed class BestandsdatenbankStartetTests : IDisposable
{
    private readonly string _temp;

    public BestandsdatenbankStartetTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "Bestand_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    private SqliteConnection Oeffne(AppPaths paths)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }

    private static void Fuehre(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Zaehle(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Eine Datenbank auf den Stand VOR dieser Version zurückversetzen.
    /// </summary>
    /// <remarks>
    /// Eine alte Datenbank von Hand nachzubauen wäre zum Scheitern verurteilt —
    /// sie hat vierzig Tabellen, und jede vergessene Spalte lässt den Test aus
    /// dem falschen Grund fallen. Also andersherum: die aktuelle Datenbank
    /// anlegen und die Neuerungen wieder herausnehmen. Was dann übrig bleibt,
    /// ist genau das, was beim Nutzer auf der Platte liegt.
    /// </remarks>
    private void VersetzeAufAltenStand(AppPaths paths)
    {
        using var connection = Oeffne(paths);
        Fuehre(connection, """
            DROP INDEX IF EXISTS IX_Photos_SymptomId;
            ALTER TABLE Photos DROP COLUMN SymptomId;
            DROP TABLE IF EXISTS CuringReadings;
            DROP TABLE IF EXISTS CuringJars;
            """);
    }

    [Fact]
    public void AnExistingDatabaseSurvivesTheUpdate()
    {
        var paths = new AppPaths(_temp);
        var tent = TestDatabase.InitializeWithDefaultTent(paths);
        // Ein Grow und ein Foto daran, wie es sie vor dem Update schon gab.
        var growId = new GrowRepository(paths).CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Alter Lauf",
            StartDate = new DateTime(2026, 1, 1),
            Status = GrowStatus.Completed,
        });
        using (var connection = Oeffne(paths))
        {
            Fuehre(connection, $"""
                INSERT INTO Photos (GrowId, RelativePath, Tag, Source, IsReferenceShot, TakenAtUtc)
                VALUES ({growId}, '/uploads/1/alt.jpg', 'Root', 'Manual', 0, '2026-01-01T00:00:00Z');
                """);
        }

        VersetzeAufAltenStand(paths);

        // Der Moment des Updates. Vorher endete er mit
        // „no such column: SymptomId" und einer Anwendung, die nicht startete.
        new DatabaseInitializer(paths, NullLogger<DatabaseInitializer>.Instance).Initialize();

        using var geprueft = Oeffne(paths);
        Assert.Equal(1L, Zaehle(geprueft, "SELECT COUNT(*) FROM pragma_table_info('Photos') WHERE name = 'SymptomId';"));
        Assert.Equal(1L, Zaehle(geprueft, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Photos_SymptomId';"));
        Assert.Equal(2L, Zaehle(geprueft, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('CuringJars', 'CuringReadings');"));

        // Und das alte Foto ist noch da. Eine Migration, die Daten verliert,
        // wäre schlimmer als eine, die scheitert.
        Assert.Equal(1L, Zaehle(geprueft, "SELECT COUNT(*) FROM Photos WHERE RelativePath = '/uploads/1/alt.jpg';"));
    }

    [Fact]
    public void TheUpdateCanRunTwiceWithoutComplaining()
    {
        // Ein Add-on-Neustart fuehrt den Initialisierer erneut aus. Was beim
        // zweiten Mal scheitert, ist keine Migration, sondern eine Falle.
        var paths = new AppPaths(_temp);
        TestDatabase.InitializeWithDefaultTent(paths);
        VersetzeAufAltenStand(paths);

        new DatabaseInitializer(paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        new DatabaseInitializer(paths, NullLogger<DatabaseInitializer>.Instance).Initialize();

        using var geprueft = Oeffne(paths);
        Assert.Equal(1L, Zaehle(geprueft, "SELECT COUNT(*) FROM pragma_table_info('Photos') WHERE name = 'SymptomId';"));
    }
}
