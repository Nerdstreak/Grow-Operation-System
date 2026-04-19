using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class DatabaseInitializer
{
    private readonly AppPaths _paths;

    public DatabaseInitializer(AppPaths paths)
    {
        _paths = paths;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabasePath)!);
        Directory.CreateDirectory(_paths.UploadRootPath);
        EnsureSchema();
        SeedDefaults();
        AutoAssignExistingGrowsToTents();
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();

        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Grows (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NULL,
                Name TEXT NOT NULL,
                Strain TEXT NULL,
                Breeder TEXT NULL,
                Status TEXT NOT NULL,
                MediumType TEXT NOT NULL,
                FeedingStyle TEXT NOT NULL,
                HydroStyle TEXT NOT NULL,
                MediumDetail TEXT NULL,
                Environment TEXT NOT NULL,
                Light TEXT NULL,
                ContainerSize TEXT NULL,
                ReservoirSize TEXT NULL,
                IrrigationStyle TEXT NULL,
                Nutrients TEXT NULL,
                Notes TEXT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Measurements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                TakenAt TEXT NOT NULL,
                Stage TEXT NOT NULL,
                Source TEXT NOT NULL DEFAULT 'Manual',
                Notes TEXT NULL,
                AirTemperatureC REAL NULL,
                HumidityPercent REAL NULL,
                HeightCm REAL NULL,
                WaterAmountMl REAL NULL,
                RunoffAmountMl REAL NULL,
                IrrigationPh REAL NULL,
                IrrigationEc REAL NULL,
                DrainPh REAL NULL,
                DrainEc REAL NULL,
                ReservoirPh REAL NULL,
                ReservoirEc REAL NULL,
                ReservoirWaterTempC REAL NULL,
                ReservoirLevelCm REAL NULL,
                ReservoirLevelLiters REAL NULL,
                DissolvedOxygenMgL REAL NULL,
                OrpMv REAL NULL,
                TopOffLiters REAL NULL,
                AddbackEc REAL NULL,
                SolutionChange INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Photos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                MeasurementId INTEGER NULL,
                RelativePath TEXT NOT NULL,
                Caption TEXT NULL,
                Tag TEXT NOT NULL DEFAULT 'Overview',
                Source TEXT NOT NULL DEFAULT 'Manual',
                IsReferenceShot INTEGER NOT NULL DEFAULT 0,
                TakenAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE,
                FOREIGN KEY (MeasurementId) REFERENCES Measurements (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Tents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Kind TEXT NOT NULL,
                Notes TEXT NULL,
                DisplayOrder INTEGER NOT NULL DEFAULT 0,
                AccentColor TEXT NOT NULL DEFAULT '#69b578',
                TemperatureEntityId TEXT NULL,
                HumidityEntityId TEXT NULL,
                VpdEntityId TEXT NULL,
                ReservoirPhEntityId TEXT NULL,
                ReservoirEcEntityId TEXT NULL,
                ReservoirLevelEntityId TEXT NULL,
                ReservoirTempEntityId TEXT NULL,
                LightEntityId TEXT NULL,
                CameraEntityId TEXT NULL,
                LightCycle TEXT NULL,
                PpfdEntityId TEXT NULL,
                PpfdTarget TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS TentSensorSnapshots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                MetricKey TEXT NOT NULL,
                Value REAL NOT NULL,
                Unit TEXT NULL,
                CapturedAtUtc TEXT NOT NULL,
                FOREIGN KEY (TentId) REFERENCES Tents (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS JournalEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                MeasurementId INTEGER NULL,
                Title TEXT NULL,
                Body TEXT NULL,
                EntryType TEXT NOT NULL,
                Source TEXT NOT NULL DEFAULT 'Manual',
                OccurredAtUtc TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE,
                FOREIGN KEY (MeasurementId) REFERENCES Measurements (Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS GrowTasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Notes TEXT NULL,
                DueAtUtc TEXT NULL,
                Priority TEXT NOT NULL DEFAULT 'Normal',
                Status TEXT NOT NULL DEFAULT 'Open',
                CreatedAtUtc TEXT NOT NULL,
                CompletedAtUtc TEXT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AuditEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                EntityType TEXT NOT NULL,
                EntityId INTEGER NULL,
                Action TEXT NOT NULL,
                Summary TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS GrowTemplates (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                MediumType TEXT NOT NULL,
                FeedingStyle TEXT NOT NULL,
                HydroStyle TEXT NOT NULL,
                MediumDetail TEXT NULL,
                Environment TEXT NOT NULL,
                SuggestedTentKind TEXT NULL,
                Light TEXT NULL,
                ContainerSize TEXT NULL,
                ReservoirSize TEXT NULL,
                IrrigationStyle TEXT NULL,
                Nutrients TEXT NULL,
                Notes TEXT NULL,
                AccentColor TEXT NOT NULL DEFAULT '#79c97f'
            );

            CREATE TABLE IF NOT EXISTS HarvestEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                HarvestedAt TEXT NOT NULL,
                WetWeightG REAL NULL,
                DryWeightG REAL NULL,
                DryDays INTEGER NULL,
                YieldNotes TEXT NULL,
                Rating REAL NULL,
                FlavorNotes TEXT NULL,
                EffectNotes TEXT NULL,
                NugStructure TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS TentSensorReadings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                MetricKey TEXT NOT NULL,
                Value REAL NOT NULL,
                Unit TEXT,
                CapturedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_TentSensorReadings_TentMetric
                ON TentSensorReadings(TentId, MetricKey, CapturedAtUtc);

            CREATE TABLE IF NOT EXISTS TentSensorDailyStats (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                MetricKey TEXT NOT NULL,
                Date TEXT NOT NULL,
                Min REAL NOT NULL,
                Max REAL NOT NULL,
                Median REAL NOT NULL,
                P5 REAL NOT NULL,
                P95 REAL NOT NULL,
                Avg REAL NOT NULL,
                Count INTEGER NOT NULL,
                Unit TEXT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_TentSensorDailyStats_Unique
                ON TentSensorDailyStats(TentId, MetricKey, Date);

            CREATE INDEX IF NOT EXISTS IX_Measurements_GrowId_TakenAt ON Measurements(GrowId, TakenAt DESC);
            CREATE INDEX IF NOT EXISTS IX_Photos_GrowId_TakenAt ON Photos(GrowId, TakenAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_Grows_TentId_Status ON Grows(TentId, Status);
            CREATE INDEX IF NOT EXISTS IX_TentSensorSnapshots_TentId_MetricKey_CapturedAtUtc ON TentSensorSnapshots(TentId, MetricKey, CapturedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_JournalEntries_GrowId_OccurredAtUtc ON JournalEntries(GrowId, OccurredAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_GrowTasks_GrowId_Status_DueAtUtc ON GrowTasks(GrowId, Status, DueAtUtc);
            CREATE INDEX IF NOT EXISTS IX_AuditEntries_GrowId_CreatedAtUtc ON AuditEntries(GrowId, CreatedAtUtc DESC);
        """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "Grows", "TentId", "INTEGER NULL");
        EnsureColumn(connection, "Grows", "MediumDetail", "TEXT NULL");
        EnsureColumn(connection, "Grows", "ReservoirSize", "TEXT NULL");
        EnsureColumn(connection, "GrowTemplates", "MediumDetail", "TEXT NULL");
        EnsureColumn(connection, "GrowTemplates", "ReservoirSize", "TEXT NULL");
        EnsureColumn(connection, "Tents", "CameraEntityId", "TEXT NULL");
        EnsureColumn(connection, "Tents", "LightCycle", "TEXT NULL");
        EnsureColumn(connection, "Tents", "PpfdEntityId", "TEXT NULL");
        EnsureColumn(connection, "Tents", "PpfdTarget", "TEXT NULL");
        // Sprint 7
        EnsureColumn(connection, "Tents", "OrpEntityId",             "TEXT NULL");
        EnsureColumn(connection, "Tents", "DissolvedOxygenEntityId", "TEXT NULL");
        EnsureColumn(connection, "Tents", "Co2EntityId",             "TEXT NULL");
        EnsureColumn(connection, "Measurements", "Source", "TEXT NOT NULL DEFAULT 'Manual'");
        EnsureColumn(connection, "Measurements", "PpfdMol", "REAL NULL");
        EnsureColumn(connection, "Measurements", "Co2Ppm", "REAL NULL");
        EnsureColumn(connection, "Photos", "Tag", "TEXT NOT NULL DEFAULT 'Overview'");
        EnsureColumn(connection, "Photos", "Source", "TEXT NOT NULL DEFAULT 'Manual'");
        EnsureColumn(connection, "Photos", "IsReferenceShot", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Grows", "IrrigationType", "TEXT NOT NULL DEFAULT 'Manual'");
        EnsureColumn(connection, "Grows", "WaterSource", "TEXT NOT NULL DEFAULT 'Tap'");
        EnsureColumn(connection, "Grows", "SeedType",                       "TEXT NOT NULL DEFAULT 'Feminized'");
        EnsureColumn(connection, "Grows", "StartMaterial",                  "TEXT NOT NULL DEFAULT 'Seed'");
        EnsureColumn(connection, "Grows", "GerminationMethod",              "TEXT NULL");
        EnsureColumn(connection, "Grows", "CloneSource",                    "TEXT NULL");
        EnsureColumn(connection, "Grows", "CloneIsRooted",                  "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Grows", "BreederFlowerWeeksMin",          "INTEGER NULL");
        EnsureColumn(connection, "Grows", "BreederFlowerWeeksMax",          "INTEGER NULL");
        EnsureColumn(connection, "Grows", "PlantCount",                     "INTEGER NULL");
        EnsureColumn(connection, "Grows", "PhenoNumber",                    "INTEGER NULL");
        EnsureColumn(connection, "Grows", "PropagationMedium",              "TEXT NULL");
        EnsureColumn(connection, "Grows", "HasChiller",                     "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "Grows", "EntryPoint",                     "TEXT NOT NULL DEFAULT 'Germination'");
        EnsureColumn(connection, "Grows", "DaysAlreadyInPhase",             "INTEGER NULL");
        EnsureColumn(connection, "Grows", "AutoflowerDaysSinceGermination", "INTEGER NULL");
        EnsureColumn(connection, "Grows", "FlipDate",                       "TEXT NULL");
        // Sprint 10
        EnsureColumn(connection, "Grows", "GerminatedAt", "TEXT NULL");
        EnsureColumn(connection, "Grows", "RootedAt",     "TEXT NULL");
        // Group D — Zelt physisches Setup
        EnsureColumn(connection, "Tents", "WidthCm",             "INTEGER NULL");
        EnsureColumn(connection, "Tents", "DepthCm",             "INTEGER NULL");
        EnsureColumn(connection, "Tents", "HeightCm",            "INTEGER NULL");
        EnsureColumn(connection, "Tents", "LightType",           "TEXT NULL");
        EnsureColumn(connection, "Tents", "LightWatt",           "INTEGER NULL");
        EnsureColumn(connection, "Tents", "ExhaustFanCount",     "INTEGER NULL");
        EnsureColumn(connection, "Tents", "ExhaustM3h",          "INTEGER NULL");
        EnsureColumn(connection, "Tents", "CirculationFanCount", "INTEGER NULL");
        EnsureColumn(connection, "Tents", "Co2Type",             "TEXT NULL");
        EnsureColumn(connection, "Tents", "Co2TargetPpm",        "INTEGER NULL");
        // Group D — GrowSystems
        EnsureColumn(connection, "Grows", "SystemId", "INTEGER NULL");
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS GrowSystems (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Name            TEXT    NOT NULL,
                HydroStyle      TEXT    NOT NULL,
                PotCount        INTEGER NULL,
                PotSizeLiters   REAL    NULL,
                ReservoirLiters REAL    NULL,
                Notes           TEXT    NULL,
                DisplayOrder    INTEGER NOT NULL DEFAULT 99,
                CreatedAtUtc    TEXT    NOT NULL
            );
        """;
        command.ExecuteNonQuery();
    }

    private void SeedDefaults()
    {
        using var connection = OpenConnection();
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Tents;";
        var tentCount = Convert.ToInt32((long)(countCommand.ExecuteScalar() ?? 0L));
        if (tentCount == 0)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Tents (Name, Kind, Notes, DisplayOrder, AccentColor)
                VALUES
                    ('Hauptzelt', 'Blüte / Hauptlauf', 'Dein großes AC Infinity Zelt für Hauptgrows.', 1, '#7dd3a6'),
                    ('Anzuchtzelt', 'Anzucht / Jungpflanzen', 'Kleines Zelt für Keimung, Stecklinge und frühe Veg.', 2, '#79c3ff');
            """;
            insert.ExecuteNonQuery();
        }

        // Entferne nicht-Hydro-Templates – App ist RDWC/DWC-only
        using var deleteNonHydro = connection.CreateCommand();
        deleteNonHydro.CommandText = "DELETE FROM GrowTemplates WHERE MediumType != 'Hydro';";
        deleteNonHydro.ExecuteNonQuery();

        using var templateCount = connection.CreateCommand();
        templateCount.CommandText = "SELECT COUNT(*) FROM GrowTemplates;";
        var growTemplateCount = Convert.ToInt32((long)(templateCount.ExecuteScalar() ?? 0L));
        if (growTemplateCount == 0)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO GrowTemplates (Name, Description, MediumType, FeedingStyle, HydroStyle, MediumDetail, Environment, SuggestedTentKind, Light, ContainerSize, ReservoirSize, IrrigationStyle, Nutrients, Notes, AccentColor)
                VALUES
                    ('RDWC Standard', 'Für rezirkulierende Hydro-Runs mit Reservoir-Tracking, Addback und Kamera-Überblick.', 'Hydro', 'None', 'RDWC', 'RDWC', 'Indoor', 'Blüte / Hauptlauf', 'LED Vollspektrum', 'Netztopf / RDWC Site', '60 L Reservoir', null, 'Athena / Hydroponic Research', 'Ideal für dein Hauptzelt mit Home-Assistant-Monitoring.', '#7dd3a6');
            """;
            insert.ExecuteNonQuery();
        }
    }

    private void AutoAssignExistingGrowsToTents()
    {
        using var connection = OpenConnection();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT Id, Name, Notes FROM Grows WHERE TentId IS NULL;";

        var mainTentId = GetTentId(connection, "Hauptzelt");
        var seedTentId = GetTentId(connection, "Anzuchtzelt");
        if (mainTentId == 0 || seedTentId == 0)
        {
            return;
        }

        using var reader = select.ExecuteReader();
        var rawItems = new List<(int id, string name, string notes)>();
        while (reader.Read())
        {
            rawItems.Add((
                Convert.ToInt32((long)reader["Id"]),
                reader["Name"]?.ToString() ?? string.Empty,
                reader["Notes"]?.ToString() ?? string.Empty));
        }
        reader.Close();

        var pending = new List<(int id, string name, string notes, int tentId)>();
        foreach (var raw in rawItems)
        {
            var stage = GetLatestStage(connection, raw.id)?.ToLowerInvariant() ?? string.Empty;
            var combined = $"{raw.name} {raw.notes} {stage}".ToLowerInvariant();
            var tentId = combined.Contains("easyplug") || combined.Contains("anzucht") || stage is "seedling" or "clone"
                ? seedTentId
                : mainTentId;
            pending.Add((raw.id, raw.name, raw.notes, tentId));
        }

        foreach (var item in pending)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Grows SET TentId = $tentId WHERE Id = $id;";
            update.Parameters.AddWithValue("$tentId", item.tentId);
            update.Parameters.AddWithValue("$id", item.id);
            update.ExecuteNonQuery();
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({table});";
        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
            {
                reader.Close();
                return;
            }
        }
        reader.Close();

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    private static int GetTentId(SqliteConnection connection, string tentName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Tents WHERE Name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tentName);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    private static string? GetLatestStage(SqliteConnection connection, int growId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Stage FROM Measurements WHERE GrowId = $growId ORDER BY TakenAt DESC, Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$growId", growId);
        return command.ExecuteScalar()?.ToString();
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
