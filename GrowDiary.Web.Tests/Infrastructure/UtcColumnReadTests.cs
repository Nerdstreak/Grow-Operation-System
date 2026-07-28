using System.Text.RegularExpressions;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Spalten, die „…Utc" heißen, müssen auch als UTC herauskommen.
/// </summary>
/// <remarks>
/// <para>Der Ortszeit-Parser (<c>AssumeLocal</c> ohne <c>AdjustToUniversal</c>)
/// wandelt einen gespeicherten Wert mit „Z" beim Lesen in Ortszeit um. Für die
/// Anzeige fällt das nicht auf — der Wert trägt seinen Versatz mit und meint
/// denselben Zeitpunkt. Sobald aber jemand damit rechnet, ist er um den
/// Zeitzonen-Versatz daneben: eine Ablesung von vor zehn Minuten lag plötzlich
/// zwei Stunden in der Zukunft, und die Prüfung „ist der Wert zu alt?" ging ins
/// Leere.</para>
///
/// <para>Auf einem Rechner in UTC — also auf dem Bau-Server — ist der Versatz
/// null und nichts davon sichtbar. Deshalb prüfen diese Tests die Differenz
/// zwischen zwei Zeitpunkten und nicht die abgelesene Zahl.</para>
/// </remarks>
public sealed class UtcColumnReadTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public UtcColumnReadTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        TestDatabase.InitializeWithDefaultTent(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    /// <summary>
    /// Kein Repository darf eine „…Utc"-Spalte mit dem Ortszeit-Parser lesen.
    /// </summary>
    /// <remarks>
    /// Ein Test auf den Quelltext, weil der Fehler sonst genau so
    /// zurückkommt, wie er entstanden ist: beim Anlegen der nächsten Spalte
    /// wird die Zeile daneben kopiert. 45 Lesestellen waren betroffen, ohne
    /// dass ein einziger Test rot wurde.
    /// </remarks>
    [Fact]
    public void NoRepositoryReadsAUtcColumnAsLocalTime()
    {
        var verzeichnis = Path.Combine(FindProjectRoot(), "GrowDiary.Web", "Infrastructure");
        var muster = new Regex(@"ParseStoredDateTime\([^)]*reader\[""[A-Za-z]*Utc""\]");

        var treffer = Directory.EnumerateFiles(verzeichnis, "*.cs")
            .SelectMany(datei => File.ReadLines(datei)
                .Select((zeile, nummer) => (datei, nummer: nummer + 1, zeile))
                .Where(eintrag => muster.IsMatch(eintrag.zeile)))
            .Select(eintrag => $"{Path.GetFileName(eintrag.datei)}:{eintrag.nummer}")
            .ToList();

        Assert.True(treffer.Count == 0,
            "Diese Stellen lesen eine UTC-Spalte als Ortszeit. Richtig ist ParseStoredUtcDateTime:\n"
            + string.Join("\n", treffer));
    }

    /// <summary>
    /// Der Zeitstempel einer Sensor-Ablesung übersteht den Weg durch die Datenbank.
    /// </summary>
    /// <remarks>
    /// Daran hängt mehr als die Anzeige: die Dosierung fragt, ob der letzte
    /// Wasserstand jünger als zwei Stunden ist, und der Lagebericht sagt „vor
    /// X Minuten gemessen". Beides rechnet gegen <c>DateTime.UtcNow</c>.
    /// </remarks>
    [Fact]
    public void ASensorSnapshotKeepsItsInstant()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var vorZehnMinuten = DateTime.UtcNow.AddMinutes(-10);

        repo.AddTentSensorSnapshot(new TentSensorSnapshot
        {
            TentId = tent.Id,
            MetricKey = "reservoir-level",
            Value = 118,
            Unit = "L",
            CapturedAtUtc = vorZehnMinuten,
        });

        var gelesen = repo.GetTentSensorSnapshots(tent.Id, ["reservoir-level"]).Single();

        var alter = DateTime.UtcNow - gelesen.CapturedAtUtc;
        Assert.InRange(alter.TotalMinutes, 9, 11);
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
