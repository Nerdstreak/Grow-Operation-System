using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;

public sealed class DatabaseInitializer
{
    private readonly AppPaths _paths;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppPaths paths, ILogger<DatabaseInitializer> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabasePath)!);
        Directory.CreateDirectory(_paths.UploadRootPath);
        DropLegacyTentSchemaIfNeeded();
        EnsureSchema();
        SeedDefaults();
        AutoAssignExistingGrowsToTents();
    }

    private void DropLegacyTentSchemaIfNeeded()
    {
        using var connection = OpenConnection();

        bool hasLegacyColumn;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COUNT(*) FROM pragma_table_info('Tents')
                WHERE name = 'TemperatureEntityId';";
            hasLegacyColumn = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        if (!hasLegacyColumn)
            return;

        _logger.LogWarning(
            "Legacy Tent-Schema erkannt. Tents und abhängige Daten werden gelöscht " +
            "(einmalig beim ersten Start mit B1a-Schema).");

        foreach (var sql in new[] { "DROP TABLE IF EXISTS TentSensors;", "DROP TABLE IF EXISTS Tents;" })
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        var tablesToClear = new[]
        {
            "Grows", "Measurements", "Photos", "JournalEntries",
            "GrowTasks", "HarvestEntries", "TentSensorReadings",
            "TentSensorSnapshots", "TentSensorDailyStats"
        };

        foreach (var table in tablesToClear)
        {
            if (!TableExists(connection, table)) continue;
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM {table};";
            cmd.ExecuteNonQuery();
        }
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
                SetupId INTEGER NULL,
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
                Kind TEXT NOT NULL DEFAULT 'Grow Tent',
                TentType TEXT NOT NULL DEFAULT 'MultiPurpose',
                Notes TEXT NULL,
                DisplayOrder INTEGER NOT NULL DEFAULT 99,
                AccentColor TEXT NOT NULL DEFAULT '#69b578',
                WidthCm INTEGER NULL,
                DepthCm INTEGER NULL,
                TentHeightCm INTEGER NULL,
                LightType TEXT NULL,
                LightWatt INTEGER NULL,
                LightController TEXT NULL,
                LightControllerEntityId TEXT NULL,
                ExhaustFanCount INTEGER NULL,
                ExhaustM3h INTEGER NULL,
                CirculationFanCount INTEGER NULL,
                HvacController TEXT NULL,
                HvacControllerEntityId TEXT NULL,
                Co2Available INTEGER NOT NULL DEFAULT 0,
                CameraEntityId TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Setups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                SetupType TEXT NOT NULL,
                Status TEXT NOT NULL,
                Notes TEXT NULL,
                CloneCounterTotal INTEGER NULL,
                LastCloneCutAt TEXT NULL,
                MotherHealthStatus TEXT NULL,
                QuarantineStartedAt TEXT NULL,
                QuarantinePlannedEndAt TEXT NULL,
                QuarantineResult TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (TentId) REFERENCES Tents (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Strains (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Breeder TEXT NULL,
                Dominance TEXT NOT NULL DEFAULT 'Unknown',
                FlowerWeeksMin INTEGER NULL,
                FlowerWeeksMax INTEGER NULL,
                Notes TEXT NULL,
                NutrientDemandFactor REAL NULL,
                StretchFactor REAL NULL,
                VpdPreferenceShift REAL NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PlantInstances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StrainId INTEGER NULL,
                SetupId INTEGER NULL,
                GrowId INTEGER NULL,
                ParentPlantId INTEGER NULL,
                Label TEXT NOT NULL,
                PlantRole TEXT NOT NULL,
                PlantStatus TEXT NOT NULL,
                PhenoLabel TEXT NULL,
                StartedAt TEXT NULL,
                EndedAt TEXT NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (StrainId) REFERENCES Strains (Id) ON DELETE SET NULL,
                FOREIGN KEY (SetupId) REFERENCES Setups (Id) ON DELETE SET NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE SET NULL,
                FOREIGN KEY (ParentPlantId) REFERENCES PlantInstances (Id) ON DELETE SET NULL
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

            CREATE TABLE IF NOT EXISTS TentSensors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                MetricType TEXT NOT NULL,
                HaEntityId TEXT NOT NULL,
                DisplayLabel TEXT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (TentId) REFERENCES Tents(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_TentSensors_TentId
                ON TentSensors(TentId);

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

            CREATE TABLE IF NOT EXISTS SopInstances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                SopId TEXT NOT NULL,
                SopName TEXT NOT NULL,
                SopType TEXT NOT NULL,
                Status TEXT NOT NULL,
                Source TEXT NOT NULL,
                SourceRecommendationKey TEXT NULL,
                TreatmentRecommendationStableKey TEXT NULL,
                StartedAtUtc TEXT NOT NULL,
                CompletedAtUtc TEXT NULL,
                CancelledAtUtc TEXT NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS SopStepInstances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SopInstanceId INTEGER NOT NULL,
                StepId TEXT NOT NULL,
                "Order" INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NULL,
                StepType TEXT NOT NULL,
                Status TEXT NOT NULL,
                WaitMinutes INTEGER NULL,
                SubSopId TEXT NULL,
                ExpectedInputsJson TEXT NULL,
                PhotoRequired INTEGER NOT NULL DEFAULT 0,
                PhotoRecommended INTEGER NOT NULL DEFAULT 0,
                StartedAtUtc TEXT NULL,
                CompletedAtUtc TEXT NULL,
                SkippedAtUtc TEXT NULL,
                Notes TEXT NULL,
                MeasurementId INTEGER NULL,
                JournalEntryId INTEGER NULL,
                PhotoAssetId INTEGER NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (SopInstanceId) REFERENCES SopInstances (Id) ON DELETE CASCADE,
                FOREIGN KEY (MeasurementId) REFERENCES Measurements (Id) ON DELETE SET NULL,
                FOREIGN KEY (JournalEntryId) REFERENCES JournalEntries (Id) ON DELETE SET NULL,
                FOREIGN KEY (PhotoAssetId) REFERENCES Photos (Id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_SopInstances_GrowId_Status
                ON SopInstances(GrowId, Status);

            CREATE INDEX IF NOT EXISTS IX_SopInstances_GrowId_SopId_Status
                ON SopInstances(GrowId, SopId, Status);

            CREATE INDEX IF NOT EXISTS IX_SopStepInstances_SopInstanceId
                ON SopStepInstances(SopInstanceId);

            CREATE INDEX IF NOT EXISTS IX_SopStepInstances_SopInstanceId_Order
                ON SopStepInstances(SopInstanceId, "Order");

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

            CREATE TABLE IF NOT EXISTS LightSchedules (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                LightsOnTime TEXT NOT NULL,
                LightsOffTime TEXT NOT NULL,
                TimeZoneId TEXT NULL,
                Source TEXT NOT NULL DEFAULT 'Manual',
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (TentId) REFERENCES Tents (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS LightTransitionEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TentId INTEGER NOT NULL,
                Kind TEXT NOT NULL,
                OccurredAtUtc TEXT NOT NULL,
                Source TEXT NOT NULL DEFAULT 'HomeAssistant',
                RawState TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (TentId) REFERENCES Tents (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AutoMeasurementConfigs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                TentId INTEGER NULL,
                Name TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Enabled',
                TriggerKind TEXT NOT NULL DEFAULT 'Manual',
                DelayMinutes INTEGER NULL,
                WindowMinutes INTEGER NOT NULL DEFAULT 20,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE,
                FOREIGN KEY (TentId) REFERENCES Tents (Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS AutoMeasurementFieldMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ConfigId INTEGER NOT NULL,
                MeasurementField TEXT NOT NULL,
                MetricKey TEXT NOT NULL,
                Aggregation TEXT NOT NULL DEFAULT 'Latest',
                IsRequired INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (ConfigId) REFERENCES AutoMeasurementConfigs (Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AutoMeasurementRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ConfigId INTEGER NOT NULL,
                GrowId INTEGER NOT NULL,
                TriggerKind TEXT NOT NULL,
                ScheduledForUtc TEXT NOT NULL,
                MeasurementId INTEGER NULL,
                Status TEXT NOT NULL DEFAULT 'Pending',
                ErrorMessage TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (ConfigId) REFERENCES AutoMeasurementConfigs (Id) ON DELETE CASCADE,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE,
                FOREIGN KEY (MeasurementId) REFERENCES Measurements (Id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Measurements_GrowId_TakenAt ON Measurements(GrowId, TakenAt DESC);
            CREATE INDEX IF NOT EXISTS IX_Photos_GrowId_TakenAt ON Photos(GrowId, TakenAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_Setups_TentId_Status ON Setups(TentId, Status);
            CREATE INDEX IF NOT EXISTS IX_PlantInstances_SetupId ON PlantInstances(SetupId);
            CREATE INDEX IF NOT EXISTS IX_PlantInstances_GrowId ON PlantInstances(GrowId);
            CREATE INDEX IF NOT EXISTS IX_PlantInstances_ParentPlantId ON PlantInstances(ParentPlantId);
            CREATE INDEX IF NOT EXISTS IX_PlantInstances_StrainId ON PlantInstances(StrainId);
            CREATE INDEX IF NOT EXISTS IX_TentSensorSnapshots_TentId_MetricKey_CapturedAtUtc ON TentSensorSnapshots(TentId, MetricKey, CapturedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_JournalEntries_GrowId_OccurredAtUtc ON JournalEntries(GrowId, OccurredAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_GrowTasks_GrowId_Status_DueAtUtc ON GrowTasks(GrowId, Status, DueAtUtc);
            CREATE INDEX IF NOT EXISTS IX_AuditEntries_GrowId_CreatedAtUtc ON AuditEntries(GrowId, CreatedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_AutoMeasurementConfigs_GrowId ON AutoMeasurementConfigs(GrowId);
            CREATE INDEX IF NOT EXISTS IX_AutoMeasurementFieldMappings_ConfigId ON AutoMeasurementFieldMappings(ConfigId);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_AutoMeasurementRuns_ConfigTriggerSchedule ON AutoMeasurementRuns(ConfigId, TriggerKind, ScheduledForUtc);
            CREATE INDEX IF NOT EXISTS IX_AutoMeasurementRuns_GrowId ON AutoMeasurementRuns(GrowId);
            CREATE INDEX IF NOT EXISTS IX_LightSchedules_TentId ON LightSchedules(TentId);
            CREATE INDEX IF NOT EXISTS IX_LightSchedules_TentActive ON LightSchedules(TentId, IsActive);
            CREATE INDEX IF NOT EXISTS IX_LightTransitionEvents_TentKindOccurred ON LightTransitionEvents(TentId, Kind, OccurredAtUtc);
        """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "Setups", "CloneCounterTotal", "INTEGER NULL");
        EnsureColumn(connection, "Setups", "LastCloneCutAt", "TEXT NULL");
        EnsureColumn(connection, "Setups", "MotherHealthStatus", "TEXT NULL");
        EnsureColumn(connection, "Setups", "QuarantineStartedAt", "TEXT NULL");
        EnsureColumn(connection, "Setups", "QuarantinePlannedEndAt", "TEXT NULL");
        EnsureColumn(connection, "Setups", "QuarantineResult", "TEXT NULL");
        EnsureColumn(connection, "Grows", "TentId", "INTEGER NULL");
        EnsureColumn(connection, "Grows", "SetupId", "INTEGER NULL");
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_Grows_TentId_Status ON Grows(TentId, Status);
            CREATE INDEX IF NOT EXISTS IX_Grows_SetupId ON Grows(SetupId);
        """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "Grows", "MediumDetail", "TEXT NULL");
        EnsureColumn(connection, "Grows", "ReservoirSize", "TEXT NULL");
        EnsureColumn(connection, "GrowTemplates", "MediumDetail", "TEXT NULL");
        EnsureColumn(connection, "GrowTemplates", "ReservoirSize", "TEXT NULL");
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
        // Group D — GrowSystems table first, then Grows FK column
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
        EnsureColumn(connection, "Grows", "SystemId", "INTEGER NULL");
        // Sprint E4 — SOP Scheduling
        EnsureColumn(connection, "SopInstances",     "DueAtUtc",               "TEXT NULL");
        EnsureColumn(connection, "SopInstances",     "NextStepDueAtUtc",       "TEXT NULL");
        EnsureColumn(connection, "SopInstances",     "RecurrenceIntervalDays", "INTEGER NULL");
        EnsureColumn(connection, "SopInstances",     "IsRecurring",            "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "SopStepInstances", "DueAtUtc",               "TEXT NULL");
        EnsureColumn(connection, "SopStepInstances", "AvailableAtUtc",         "TEXT NULL");
        EnsureColumn(connection, "SopStepInstances", "ReminderTaskId",         "INTEGER NULL");
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
                INSERT INTO Tents (Name, Kind, TentType, AccentColor, DisplayOrder,
                                   Co2Available, CreatedAtUtc, UpdatedAtUtc)
                VALUES ('Hauptzelt', 'Grow Tent', 'MultiPurpose', '#69b578', 1,
                        0, datetime('now'), datetime('now'));
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
        var mainTentId = GetTentId(connection, "Hauptzelt");
        if (mainTentId == 0) return;

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT Id FROM Grows WHERE TentId IS NULL;";
        using var reader = select.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
            ids.Add(Convert.ToInt32((long)reader["Id"]));
        reader.Close();

        foreach (var id in ids)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Grows SET TentId = $tentId WHERE Id = $id;";
            update.Parameters.AddWithValue("$tentId", mainTentId);
            update.Parameters.AddWithValue("$id", id);
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

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static int GetTentId(SqliteConnection connection, string tentName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Tents WHERE Name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tentName);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
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
