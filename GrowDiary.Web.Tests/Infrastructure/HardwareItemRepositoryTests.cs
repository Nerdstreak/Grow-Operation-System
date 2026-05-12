using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class HardwareItemRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;

    public HardwareItemRepositoryTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-hardware-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesHardwareItemsTableAndIndexes()
    {
        using var connection = OpenConnection();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'HardwareItems';";
        Assert.Equal(1L, tableCommand.ExecuteScalar());

        foreach (var index in new[]
        {
            "IX_HardwareItems_TentId",
            "IX_HardwareItems_SetupId",
            "IX_HardwareItems_GrowId",
            "IX_HardwareItems_WearTemplateId",
            "IX_HardwareItems_Status",
            "IX_HardwareItems_TentSensorId"
        })
        {
            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            indexCommand.Parameters.AddWithValue("$name", index);
            Assert.Equal(1L, indexCommand.ExecuteScalar());
        }
    }

    [Fact]
    public void HardwareItem_CreateGetUpdateAndListFilters()
    {
        var repo = new GrowRepository(_paths);
        var tent = repo.GetTents().Single();

        var created = repo.CreateHardwareItem(new HardwareItem
        {
            Name = "pH Sonde",
            Category = "Sensor",
            Status = HardwareItemStatus.Active,
            Criticality = HardwareItemCriticality.High,
            TentId = tent.Id,
            WearTemplateId = "ph-probe",
            HaEntityId = "sensor.ph",
            Manufacturer = "Atlas",
            Model = "EZO pH",
            SerialNumber = "PH-1",
            InstalledAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            ExpectedLifespanDays = 450,
            InspectionIntervalDays = 30,
            Notes = "Main reservoir probe"
        });

        var loaded = repo.GetHardwareItem(created.Id)!;
        Assert.Equal("pH Sonde", loaded.Name);
        Assert.Equal(tent.Id, loaded.TentId);
        Assert.Equal(HardwareItemStatus.Active, loaded.Status);

        loaded.Name = "pH Sonde aktualisiert";
        loaded.Status = HardwareItemStatus.MaintenanceDue;
        loaded.Criticality = HardwareItemCriticality.Critical;
        loaded.Notes = "Kalibrierung faellig";
        repo.UpdateHardwareItem(loaded);

        var byTent = repo.GetHardwareItemsByTent(tent.Id).Single();
        Assert.Equal("pH Sonde aktualisiert", byTent.Name);
        Assert.Equal(HardwareItemCriticality.Critical, byTent.Criticality);

        var byStatus = repo.GetHardwareItemsByStatus(HardwareItemStatus.MaintenanceDue).Single();
        Assert.Equal(created.Id, byStatus.Id);

        Assert.Single(repo.GetHardwareItems());
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
}
