using GrowDiary.Web.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class DatabaseInitializerLegacyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public DatabaseInitializerLegacyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-legacy-test-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void Initialize_AddsNullableSetupIdToLegacyGrowsWithoutLosingData()
    {
        CreateLegacyGrowsTableWithoutSetupId();

        var initializer = new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance);
        var exception = Record.Exception(() => initializer.Initialize());

        Assert.Null(exception);

        using var connection = OpenConnection();
        using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Grows') WHERE name = 'SetupId' AND [notnull] = 0;";
        Assert.Equal(1L, columnCommand.ExecuteScalar());

        using var growCommand = connection.CreateCommand();
        growCommand.CommandText = "SELECT Name, SystemId FROM Grows WHERE Id = 1;";
        using var reader = growCommand.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Legacy Grow", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));
    }

    private void CreateLegacyGrowsTableWithoutSetupId()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Grows (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NULL,
                Name TEXT NOT NULL,
                Strain TEXT NULL,
                Breeder TEXT NULL,
                Status TEXT NOT NULL,
                MediumType TEXT NOT NULL,
                FeedingStyle TEXT NOT NULL,
                HydroStyle TEXT NOT NULL,
                Environment TEXT NOT NULL,
                Light TEXT NULL,
                ContainerSize TEXT NULL,
                IrrigationStyle TEXT NULL,
                Nutrients TEXT NULL,
                Notes TEXT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            INSERT INTO Grows (
                Name, Status, MediumType, FeedingStyle, HydroStyle, Environment,
                StartDate, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                'Legacy Grow', 'Planning', 'Hydro', 'None', 'DWC', 'Indoor',
                '2026-01-01', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'
            );
        """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
}
