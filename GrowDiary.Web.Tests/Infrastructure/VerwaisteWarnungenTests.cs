using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Tests.Infrastructure;

/// <summary>
/// Die Bestandspflege darf nur schliessen, was wirklich verwaist ist.
/// </summary>
/// <remarks>
/// <para>Wer vor 2.0.0-beta.48 einen Grow gelöscht hat, behielt dessen
/// Warnungen: für immer offen auf der Aufgabenseite, weil der Abgleich nur über
/// die aktiven Grows läuft. Beim Start werden sie deshalb geschlossen.</para>
///
/// <para>Das ist die riskantere Hälfte der Änderung — sie greift bei JEDEM
/// Start in Bestandsdaten. Deshalb prüft dieser Test nicht nur, dass die
/// verwaisten geschlossen werden, sondern vor allem, dass alle anderen
/// unangetastet bleiben: eine Warnung ohne Grow-Bezug, eine an einem lebenden
/// Grow, und eine an einem abgeschlossenen. Der Grenzfall <c>GrowId = 0</c> ist
/// eigens dabei, weil <c>IS NOT NULL</c> ihn zunächst mitgeschlossen hätte.</para>
/// </remarks>
public sealed class VerwaisteWarnungenTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public VerwaisteWarnungenTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-verwaist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        TestDatabase.InitializeWithDefaultTent(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void SchliesstNurDieVerwaisten()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();

        var lebend = repo.CreateGrow(new GrowRun
        {
            Name = "Lebt", TentId = tent.Id, Status = GrowStatus.Running,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        var abgeschlossen = repo.CreateGrow(new GrowRun
        {
            Name = "Abgeschlossen", TentId = tent.Id, Status = GrowStatus.Completed,
            StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        var amLebenden = Anlegen(repo, tent.Id, lebend, "am lebenden Grow");
        var amAbgeschlossenen = Anlegen(repo, tent.Id, abgeschlossen, "am abgeschlossenen Grow");
        var ohneGrow = Anlegen(repo, tent.Id, null, "ohne Grow-Bezug");

        // Zwei Faelle, die es nur in Bestandsdaten geben kann: eine Id, die es
        // nicht gibt, und die 0 — beide werden am Repository vorbei gesetzt,
        // weil genau so die Leichen entstanden sind.
        var verwaist = Anlegen(repo, tent.Id, lebend, "verwaist");
        var nullId = Anlegen(repo, tent.Id, lebend, "GrowId 0");
        SetzeGrowId(verwaist, 9999);
        SetzeGrowId(nullId, 0);

        // Ein zweiter Start der App fuehrt die Nachpflege erneut aus.
        GrowDiary.Web.Tests.TestDatabase.Initialize(_paths);

        Assert.Equal(RiskEventStatus.Resolved, repo.GetRiskEvent(verwaist)!.Status);

        Assert.Equal(RiskEventStatus.Open, repo.GetRiskEvent(amLebenden)!.Status);
        Assert.Equal(RiskEventStatus.Open, repo.GetRiskEvent(amAbgeschlossenen)!.Status);
        Assert.Equal(RiskEventStatus.Open, repo.GetRiskEvent(ohneGrow)!.Status);
        Assert.Equal(RiskEventStatus.Open, repo.GetRiskEvent(nullId)!.Status);
    }

    [Fact]
    public void HaengtDenGrundAnStattIhnZuErsetzen()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();
        var grow = repo.CreateGrow(new GrowRun
        {
            Name = "Weg", TentId = tent.Id, Status = GrowStatus.Running,
            StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        var id = Anlegen(repo, tent.Id, grow, "mit Notiz");
        SetzeGrowId(id, 9999);

        GrowDiary.Web.Tests.TestDatabase.Initialize(_paths);

        var notiz = repo.GetRiskEvent(id)!.Notes ?? string.Empty;
        // Die vorhandene Notiz bleibt stehen — sonst geht der Grund verloren,
        // aus dem der Eintrag ueberhaupt entstanden ist.
        Assert.Contains("mit Notiz", notiz);
        Assert.Contains("geloescht", notiz);
    }

    private int Anlegen(GrowRepository repo, int tentId, int? growId, string notiz)
        => repo.CreateRiskEvent(new RiskEvent
        {
            EventType = RiskEventType.Other,
            Severity = RiskEventSeverity.Warning,
            Status = RiskEventStatus.Open,
            Source = RiskEventSource.Deviation,
            Title = "EC: Abweichung pruefen",
            Description = "egal",
            TentId = tentId,
            GrowId = growId,
            StartedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Notes = notiz,
        }).Id;

    /// <summary>Am Repository vorbei — so sind die Leichen entstanden.</summary>
    private void SetzeGrowId(int riskEventId, int growId)
    {
        using var connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE RiskEvents SET GrowId = $grow WHERE Id = $id;";
        command.Parameters.AddWithValue("$grow", growId);
        command.Parameters.AddWithValue("$id", riskEventId);
        command.ExecuteNonQuery();
    }
}
