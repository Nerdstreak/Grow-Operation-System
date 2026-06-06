using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class DatabaseInitializer
{
    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();

        command.CommandText = CoreSchemaSql;
        command.ExecuteNonQuery();

        EnsureSchemaMigrationMetadataColumns(connection);

        EnsureColumn(connection, "Setups", "CloneCounterTotal", "INTEGER NULL");
        EnsureColumn(connection, "Setups", "LastCloneCutAt", "TEXT NULL");
        EnsureColumn(connection, "Setups", "MotherHealthStatus", "TEXT NULL");
        EnsureColumn(connection, "Setups", "QuarantineStartedAt", "TEXT NULL");
        EnsureColumn(connection, "Setups", "QuarantinePlannedEndAt", "TEXT NULL");
        EnsureColumn(connection, "Setups", "QuarantineResult", "TEXT NULL");
        EnsureColumn(connection, "Grows", "TentId", "INTEGER NULL");
        EnsureColumn(connection, "Grows", "SetupId", "INTEGER NULL");
        command.CommandText = GrowIndexSql;
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
        EnsureColumn(connection, "Grows", "TentSnapshotJson", "TEXT NULL");
        EnsureColumn(connection, "Grows", "HydroSetupSnapshotJson", "TEXT NULL");
        EnsureColumn(connection, "Grows", "SnapshotsCapturedAtUtc", "TEXT NULL");
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
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_HardwareItems_HydroSetupId ON HardwareItems(HydroSetupId);
        """;
        command.ExecuteNonQuery();
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

        EnsureSchemaMigrationMetadataColumns(connection);
        var now = DateTime.UtcNow.ToString("O");

        foreach (var migration in RequiredMigrations)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AppliedSchemaMigrations (
                    Id, Name, RequiredForSchemaVersion, AppliedAtUtc,
                    Status, StartedAtUtc, CompletedAtUtc, Error,
                    RequiresBackup, IsDestructive, Checksum, EngineVersion)
                VALUES (
                    $id, $name, $requiredForSchemaVersion, $appliedAtUtc,
                    'Applied', $startedAtUtc, $completedAtUtc, NULL,
                    $requiresBackup, $isDestructive, $checksum, 'migration-engine.v1')
                ON CONFLICT(Id) DO UPDATE SET
                    Name = excluded.Name,
                    RequiredForSchemaVersion = excluded.RequiredForSchemaVersion,
                    Status = 'Applied',
                    CompletedAtUtc = COALESCE(AppliedSchemaMigrations.CompletedAtUtc, excluded.CompletedAtUtc),
                    RequiresBackup = excluded.RequiresBackup,
                    IsDestructive = excluded.IsDestructive,
                    Checksum = excluded.Checksum,
                    EngineVersion = excluded.EngineVersion;
            """;
            command.Parameters.AddWithValue("$id", migration.Id);
            command.Parameters.AddWithValue("$name", migration.Name);
            command.Parameters.AddWithValue("$requiredForSchemaVersion", migration.RequiredForSchemaVersion);
            command.Parameters.AddWithValue("$appliedAtUtc", now);
            command.Parameters.AddWithValue("$startedAtUtc", now);
            command.Parameters.AddWithValue("$completedAtUtc", now);
            command.Parameters.AddWithValue("$requiresBackup", migration.RequiresBackup ? 1 : 0);
            command.Parameters.AddWithValue("$isDestructive", migration.IsDestructive ? 1 : 0);
            command.Parameters.AddWithValue("$checksum", migration.Checksum ?? string.Empty);
            command.ExecuteNonQuery();
        }
    }


    private static void EnsureSchemaMigrationMetadataColumns(SqliteConnection connection)
    {
        if (!TableExists(connection, "AppliedSchemaMigrations"))
        {
            return;
        }

        EnsureColumn(connection, "AppliedSchemaMigrations", "Status", "TEXT NOT NULL DEFAULT 'Applied'");
        EnsureColumn(connection, "AppliedSchemaMigrations", "StartedAtUtc", "TEXT NULL");
        EnsureColumn(connection, "AppliedSchemaMigrations", "CompletedAtUtc", "TEXT NULL");
        EnsureColumn(connection, "AppliedSchemaMigrations", "Error", "TEXT NULL");
        EnsureColumn(connection, "AppliedSchemaMigrations", "RequiresBackup", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "AppliedSchemaMigrations", "IsDestructive", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "AppliedSchemaMigrations", "Checksum", "TEXT NULL");
        EnsureColumn(connection, "AppliedSchemaMigrations", "EngineVersion", "TEXT NOT NULL DEFAULT 'migration-engine.v1'");
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
