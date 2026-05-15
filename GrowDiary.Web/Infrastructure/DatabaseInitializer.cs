using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;

public sealed class DatabaseInitializer
{
    public const string CurrentSchemaVersion = "backend-core.v0.16-candidate";
    public const string CurrentSchemaAppSettingKey = "backend:schemaVersion";
    public const string LastMigrationUtcAppSettingKey = "backend:lastMigrationUtc";

    public static readonly IReadOnlyList<SchemaMigrationDescriptor> RequiredMigrations = new[]
    {
        new SchemaMigrationDescriptor("0001-core-schema", "Core schema baseline", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0002-zero-tent-startup", "Zero-tent startup and explicit test data", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0003-tent-aggregate", "Tent aggregate details and archive/delete rules", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0004-hydro-setup-aggregate", "DWC/RDWC HydroSetup aggregate", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0005-grow-hydro-setup-link", "New grows require HydroSetup", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0006-hardware-hydro-setup-link", "Hardware linked to HydroSetups", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0007-addback-volume-logs", "HydroSetup volume, Addback and Changeout logs", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0008-export-backup-hardening", "Grow export, backup validation and release readiness", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0009-security-guardrails", "Local-only admin and remote guardrails", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0010-import-readiness", "Export integrity and import validation preflight", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0011-upgrade-preflight", "Migration status and upgrade preflight", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0012-restore-plan", "Backup restore dry-run and restore readiness", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0013-grow-import-plan", "Grow export import planning dry-run", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0014-system-audit-events", "System audit events for critical backend operations", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0015-api-error-format", "Uniform API error contract for backend endpoints", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0016-legacy-mvc-containment", "Legacy MVC endpoint containment for backup/export/camera routes", CurrentSchemaVersion),
        new SchemaMigrationDescriptor("0017-product-api-remote-guard", "Product API remote access guardrails", CurrentSchemaVersion)
    };

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
                Status TEXT NOT NULL DEFAULT 'Active',
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

            CREATE TABLE IF NOT EXISTS AppliedSchemaMigrations (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                RequiredForSchemaVersion TEXT NOT NULL,
                AppliedAtUtc TEXT NOT NULL
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



            CREATE TABLE IF NOT EXISTS SystemAuditEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventType TEXT NOT NULL,
                Action TEXT NOT NULL,
                Summary TEXT NOT NULL,
                Severity TEXT NOT NULL,
                Source TEXT NOT NULL,
                RemoteAddress TEXT NULL,
                RelatedGrowId INTEGER NULL,
                RelatedFileName TEXT NULL,
                Success INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (RelatedGrowId) REFERENCES Grows (Id) ON DELETE SET NULL
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

            CREATE TABLE IF NOT EXISTS HardwareItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                Status TEXT NOT NULL,
                Criticality TEXT NOT NULL,
                TentId INTEGER NULL,
                SetupId INTEGER NULL,
                HydroSetupId INTEGER NULL,
                GrowId INTEGER NULL,
                WearTemplateId TEXT NULL,
                TentSensorId INTEGER NULL,
                HaEntityId TEXT NULL,
                Manufacturer TEXT NULL,
                Model TEXT NULL,
                SerialNumber TEXT NULL,
                InstalledAtUtc TEXT NULL,
                RetiredAtUtc TEXT NULL,
                ExpectedLifespanDays INTEGER NULL,
                InspectionIntervalDays INTEGER NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MaintenanceEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HardwareItemId INTEGER NOT NULL,
                EventType TEXT NOT NULL,
                Status TEXT NOT NULL,
                Result TEXT NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NULL,
                DueAtUtc TEXT NULL,
                PerformedAtUtc TEXT NULL,
                NextDueAtUtc TEXT NULL,
                GrowTaskId INTEGER NULL,
                SopInstanceId INTEGER NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CalibrationEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                HardwareItemId INTEGER NOT NULL,
                CalibrationType TEXT NOT NULL,
                Status TEXT NOT NULL,
                Result TEXT NOT NULL,
                Title TEXT NOT NULL,
                ReferenceSolution TEXT NULL,
                ReferenceValue REAL NULL,
                BeforeValue REAL NULL,
                AfterValue REAL NULL,
                TemperatureC REAL NULL,
                DueAtUtc TEXT NULL,
                PerformedAtUtc TEXT NULL,
                NextDueAtUtc TEXT NULL,
                GrowTaskId INTEGER NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS RiskEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventType TEXT NOT NULL,
                Severity TEXT NOT NULL,
                Status TEXT NOT NULL,
                Source TEXT NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NULL,
                HardwareItemId INTEGER NULL,
                TentId INTEGER NULL,
                GrowId INTEGER NULL,
                TentSensorId INTEGER NULL,
                HaEntityId TEXT NULL,
                SopInstanceId INTEGER NULL,
                GrowTaskId INTEGER NULL,
                StartedAtUtc TEXT NOT NULL,
                LastSeenAtUtc TEXT NULL,
                ResolvedAtUtc TEXT NULL,
                AcknowledgedAtUtc TEXT NULL,
                DedupeKey TEXT NULL,
                RawValue TEXT NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AddbackLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                HydroSetupId INTEGER NULL,
                Kind TEXT NOT NULL DEFAULT 'Addback',
                PerformedAtUtc TEXT NOT NULL,
                ReservoirLiters REAL NULL,
                EcBefore REAL NULL,
                EcTarget REAL NULL,
                EcStock REAL NULL,
                EcAfter REAL NULL,
                PhBefore REAL NULL,
                PhAfter REAL NULL,
                LitersAdded REAL NULL,
                NewReservoirVolumeLiters REAL NULL,
                UsedHydroSetupVolume INTEGER NOT NULL DEFAULT 0,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE,
                FOREIGN KEY (HydroSetupId) REFERENCES GrowSystems (Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS ChangeoutEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GrowId INTEGER NOT NULL,
                HydroSetupId INTEGER NULL,
                Kind TEXT NOT NULL DEFAULT 'Partial',
                PerformedAtUtc TEXT NOT NULL,
                VolumeChangedLiters REAL NULL,
                PercentChanged REAL NULL,
                EcBefore REAL NULL,
                EcAfter REAL NULL,
                PhBefore REAL NULL,
                PhAfter REAL NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (GrowId) REFERENCES Grows (Id) ON DELETE CASCADE,
                FOREIGN KEY (HydroSetupId) REFERENCES GrowSystems (Id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Measurements_GrowId_TakenAt ON Measurements(GrowId, TakenAt DESC);
            CREATE INDEX IF NOT EXISTS IX_AddbackLogs_GrowId_PerformedAt ON AddbackLogs(GrowId, PerformedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_AddbackLogs_HydroSetupId ON AddbackLogs(HydroSetupId);
            CREATE INDEX IF NOT EXISTS IX_ChangeoutEntries_GrowId_PerformedAt ON ChangeoutEntries(GrowId, PerformedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_ChangeoutEntries_HydroSetupId ON ChangeoutEntries(HydroSetupId);
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
            CREATE INDEX IF NOT EXISTS IX_SystemAuditEvents_CreatedAtUtc ON SystemAuditEvents(CreatedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_SystemAuditEvents_EventType_CreatedAtUtc ON SystemAuditEvents(EventType, CreatedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_SystemAuditEvents_RelatedGrowId_CreatedAtUtc ON SystemAuditEvents(RelatedGrowId, CreatedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_AutoMeasurementConfigs_GrowId ON AutoMeasurementConfigs(GrowId);
            CREATE INDEX IF NOT EXISTS IX_AutoMeasurementFieldMappings_ConfigId ON AutoMeasurementFieldMappings(ConfigId);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_AutoMeasurementRuns_ConfigTriggerSchedule ON AutoMeasurementRuns(ConfigId, TriggerKind, ScheduledForUtc);
            CREATE INDEX IF NOT EXISTS IX_AutoMeasurementRuns_GrowId ON AutoMeasurementRuns(GrowId);
            CREATE INDEX IF NOT EXISTS IX_LightSchedules_TentId ON LightSchedules(TentId);
            CREATE INDEX IF NOT EXISTS IX_LightSchedules_TentActive ON LightSchedules(TentId, IsActive);
            CREATE INDEX IF NOT EXISTS IX_LightTransitionEvents_TentKindOccurred ON LightTransitionEvents(TentId, Kind, OccurredAtUtc);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_TentId ON HardwareItems(TentId);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_SetupId ON HardwareItems(SetupId);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_HydroSetupId ON HardwareItems(HydroSetupId);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_GrowId ON HardwareItems(GrowId);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_WearTemplateId ON HardwareItems(WearTemplateId);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_Status ON HardwareItems(Status);
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_TentSensorId ON HardwareItems(TentSensorId);
            CREATE INDEX IF NOT EXISTS IX_MaintenanceEvents_HardwareItemId ON MaintenanceEvents(HardwareItemId);
            CREATE INDEX IF NOT EXISTS IX_MaintenanceEvents_Status ON MaintenanceEvents(Status);
            CREATE INDEX IF NOT EXISTS IX_MaintenanceEvents_DueAtUtc ON MaintenanceEvents(DueAtUtc);
            CREATE INDEX IF NOT EXISTS IX_MaintenanceEvents_NextDueAtUtc ON MaintenanceEvents(NextDueAtUtc);
            CREATE INDEX IF NOT EXISTS IX_MaintenanceEvents_GrowTaskId ON MaintenanceEvents(GrowTaskId);
            CREATE INDEX IF NOT EXISTS IX_MaintenanceEvents_SopInstanceId ON MaintenanceEvents(SopInstanceId);
            CREATE INDEX IF NOT EXISTS IX_CalibrationEvents_HardwareItemId ON CalibrationEvents(HardwareItemId);
            CREATE INDEX IF NOT EXISTS IX_CalibrationEvents_Status ON CalibrationEvents(Status);
            CREATE INDEX IF NOT EXISTS IX_CalibrationEvents_DueAtUtc ON CalibrationEvents(DueAtUtc);
            CREATE INDEX IF NOT EXISTS IX_CalibrationEvents_NextDueAtUtc ON CalibrationEvents(NextDueAtUtc);
            CREATE INDEX IF NOT EXISTS IX_CalibrationEvents_GrowTaskId ON CalibrationEvents(GrowTaskId);
            CREATE INDEX IF NOT EXISTS IX_CalibrationEvents_CalibrationType ON CalibrationEvents(CalibrationType);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_Status ON RiskEvents(Status);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_Severity ON RiskEvents(Severity);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_EventType ON RiskEvents(EventType);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_HardwareItemId ON RiskEvents(HardwareItemId);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_TentId ON RiskEvents(TentId);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_GrowId ON RiskEvents(GrowId);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_TentSensorId ON RiskEvents(TentSensorId);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_DedupeKey_Status ON RiskEvents(DedupeKey, Status);
            CREATE INDEX IF NOT EXISTS IX_RiskEvents_StartedAtUtc ON RiskEvents(StartedAtUtc);
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
                TentId          INTEGER NULL,
                Name            TEXT    NOT NULL,
                HydroStyle      TEXT    NOT NULL,
                PotCount        INTEGER NULL,
                PotSizeLiters   REAL    NULL,
                ReservoirLiters REAL    NULL,
                Status          TEXT    NOT NULL DEFAULT 'Active',
                LayoutType      TEXT    NOT NULL DEFAULT 'SingleBucket',
                ReservoirPosition TEXT  NOT NULL DEFAULT 'None',
                HasCirculationPump INTEGER NOT NULL DEFAULT 0,
                CirculationPumpNotes TEXT NULL,
                HasAirPump      INTEGER NOT NULL DEFAULT 0,
                AirPumpNotes    TEXT NULL,
                AirStoneCount   INTEGER NULL,
                HasChiller      INTEGER NOT NULL DEFAULT 0,
                HasUvSterilizer INTEGER NOT NULL DEFAULT 0,
                Notes           TEXT    NULL,
                DisplayOrder    INTEGER NOT NULL DEFAULT 99,
                CreatedAtUtc    TEXT    NOT NULL,
                UpdatedAtUtc    TEXT    NULL
            );
        """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "HardwareItems", "HydroSetupId", "INTEGER NULL");
        EnsureColumn(connection, "Tents", "Status", "TEXT NOT NULL DEFAULT 'Active'");
        EnsureColumn(connection, "GrowSystems", "TentId", "INTEGER NULL");
        EnsureColumn(connection, "GrowSystems", "Status", "TEXT NOT NULL DEFAULT 'Active'");
        EnsureColumn(connection, "GrowSystems", "LayoutType", "TEXT NOT NULL DEFAULT 'SingleBucket'");
        EnsureColumn(connection, "GrowSystems", "ReservoirPosition", "TEXT NOT NULL DEFAULT 'None'");
        EnsureColumn(connection, "GrowSystems", "HasCirculationPump", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "GrowSystems", "CirculationPumpNotes", "TEXT NULL");
        EnsureColumn(connection, "GrowSystems", "HasAirPump", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "GrowSystems", "AirPumpNotes", "TEXT NULL");
        EnsureColumn(connection, "GrowSystems", "AirStoneCount", "INTEGER NULL");
        EnsureColumn(connection, "GrowSystems", "HasChiller", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "GrowSystems", "HasUvSterilizer", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "GrowSystems", "UpdatedAtUtc", "TEXT NULL");
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_GrowSystems_TentId ON GrowSystems(TentId);
            CREATE INDEX IF NOT EXISTS IX_GrowSystems_Status ON GrowSystems(Status);
            CREATE INDEX IF NOT EXISTS IX_GrowSystems_HydroStyle ON GrowSystems(HydroStyle);
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

        RecordSchemaVersion(connection);
    }

    private static void RecordSchemaVersion(SqliteConnection connection)
    {
        UpsertAppSetting(connection, CurrentSchemaAppSettingKey, CurrentSchemaVersion);
        UpsertAppSetting(connection, LastMigrationUtcAppSettingKey, DateTime.UtcNow.ToString("O"));
        RecordAppliedSchemaMigrations(connection);
    }

    private static void RecordAppliedSchemaMigrations(SqliteConnection connection)
    {
        if (!TableExists(connection, "AppliedSchemaMigrations"))
        {
            return;
        }

        foreach (var migration in RequiredMigrations)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AppliedSchemaMigrations (Id, Name, RequiredForSchemaVersion, AppliedAtUtc)
                VALUES ($id, $name, $requiredForSchemaVersion, $appliedAtUtc)
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    RequiredForSchemaVersion = excluded.RequiredForSchemaVersion;
            """;
            command.Parameters.AddWithValue("$id", migration.Id);
            command.Parameters.AddWithValue("$name", migration.Name);
            command.Parameters.AddWithValue("$requiredForSchemaVersion", migration.RequiredForSchemaVersion);
            command.Parameters.AddWithValue("$appliedAtUtc", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
    }

    private static void UpsertAppSetting(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
        """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private void SeedDefaults()
    {
        using var connection = OpenConnection();
        // Keine Standard-Zelte seeden: Grow OS startet bewusst leer.
        // Nutzer legen ihre Zelte unter /zelte selbst an.

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

public sealed record SchemaMigrationDescriptor(string Id, string Name, string RequiredForSchemaVersion);
