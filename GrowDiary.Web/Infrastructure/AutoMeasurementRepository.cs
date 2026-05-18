using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class AutoMeasurementRepository : RepositoryBase
{
    public AutoMeasurementRepository(AppPaths paths) : base(paths)
    {
    }

    public AutoMeasurementConfig CreateAutoMeasurementConfig(AutoMeasurementConfig config)
    {
        config.CreatedAtUtc = DateTime.UtcNow;
        config.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AutoMeasurementConfigs (
                GrowId, TentId, Name, Status, TriggerKind, DelayMinutes, WindowMinutes,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $growId, $tentId, $name, $status, $triggerKind, $delayMinutes, $windowMinutes,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddAutoMeasurementConfigParameters(command, config);
        config.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return config;
    }

    public void UpdateAutoMeasurementConfig(AutoMeasurementConfig config)
    {
        config.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AutoMeasurementConfigs SET
                TentId = $tentId,
                Name = $name,
                Status = $status,
                TriggerKind = $triggerKind,
                DelayMinutes = $delayMinutes,
                WindowMinutes = $windowMinutes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddAutoMeasurementConfigParameters(command, config);
        command.Parameters.AddWithValue("$id", config.Id);
        command.ExecuteNonQuery();
    }

    public AutoMeasurementConfig? GetAutoMeasurementConfig(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AutoMeasurementConfigs WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAutoMeasurementConfig(reader) : null;
    }

    public List<AutoMeasurementConfig> GetAutoMeasurementConfigs()
        => GetAutoMeasurementConfigsByWhere(string.Empty, null);

    public List<AutoMeasurementConfig> GetAutoMeasurementConfigsByGrow(int growId)
        => GetAutoMeasurementConfigsByWhere("WHERE GrowId = $growId", growId);

    public List<AutoMeasurementConfig> GetEnabledAutoMeasurementConfigs()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM AutoMeasurementConfigs
            WHERE Status = 'Enabled'
            ORDER BY Id;
        """;

        var configs = new List<AutoMeasurementConfig>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            configs.Add(MapAutoMeasurementConfig(reader));
        }
        return configs;
    }

    public void ReplaceAutoMeasurementFieldMappings(int configId, IReadOnlyCollection<AutoMeasurementFieldMapping> mappings)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM AutoMeasurementFieldMappings WHERE ConfigId = $configId;";
            deleteCommand.Parameters.AddWithValue("$configId", configId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var mapping in mappings)
        {
            var stored = new AutoMeasurementFieldMapping
            {
                ConfigId = configId,
                MeasurementField = mapping.MeasurementField,
                MetricKey = mapping.MetricKey,
                Aggregation = mapping.Aggregation,
                IsRequired = mapping.IsRequired,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO AutoMeasurementFieldMappings (
                    ConfigId, MeasurementField, MetricKey, Aggregation, IsRequired,
                    CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $configId, $measurementField, $metricKey, $aggregation, $isRequired,
                    $createdAtUtc, $updatedAtUtc
                );
            """;
            AddAutoMeasurementFieldMappingParameters(insertCommand, stored);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<AutoMeasurementFieldMapping> GetAutoMeasurementFieldMappings(int configId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM AutoMeasurementFieldMappings
            WHERE ConfigId = $configId
            ORDER BY Id;
        """;
        command.Parameters.AddWithValue("$configId", configId);
        var list = new List<AutoMeasurementFieldMapping>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapAutoMeasurementFieldMapping(reader));
        }
        return list;
    }

    public AutoMeasurementRun CreateAutoMeasurementRunIfNotExists(AutoMeasurementRun run)
    {
        run.CreatedAtUtc = DateTime.UtcNow;
        run.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT OR IGNORE INTO AutoMeasurementRuns (
                    ConfigId, GrowId, TriggerKind, ScheduledForUtc, MeasurementId, Status,
                    ErrorMessage, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $configId, $growId, $triggerKind, $scheduledForUtc, $measurementId, $status,
                    $errorMessage, $createdAtUtc, $updatedAtUtc
                );
            """;
            AddAutoMeasurementRunParameters(insertCommand, run);
            insertCommand.ExecuteNonQuery();
        }

        AutoMeasurementRun stored;
        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT * FROM AutoMeasurementRuns
                WHERE ConfigId = $configId AND TriggerKind = $triggerKind AND ScheduledForUtc = $scheduledForUtc
                LIMIT 1;
            """;
            selectCommand.Parameters.AddWithValue("$configId", run.ConfigId);
            selectCommand.Parameters.AddWithValue("$triggerKind", run.TriggerKind.ToString());
            selectCommand.Parameters.AddWithValue("$scheduledForUtc", ToStorageUtc(run.ScheduledForUtc));
            using var reader = selectCommand.ExecuteReader();
            stored = reader.Read() ? MapAutoMeasurementRun(reader) : run;
        }

        transaction.Commit();
        return stored;
    }

    public List<AutoMeasurementRun> GetAutoMeasurementRunsByConfig(int configId)
        => GetAutoMeasurementRunsByWhere("WHERE ConfigId = $configId", configId, null);

    public List<AutoMeasurementRun> GetAutoMeasurementRunsByGrow(int growId)
        => GetAutoMeasurementRunsByWhere("WHERE GrowId = $growId", null, growId);

    public AutoMeasurementRun? GetAutoMeasurementRun(int configId, AutoMeasurementTriggerKind triggerKind, DateTime scheduledForUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM AutoMeasurementRuns
            WHERE ConfigId = $configId
              AND TriggerKind = $triggerKind
              AND ScheduledForUtc = $scheduledForUtc
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$configId", configId);
        command.Parameters.AddWithValue("$triggerKind", triggerKind.ToString());
        command.Parameters.AddWithValue("$scheduledForUtc", ToStorageUtc(scheduledForUtc));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAutoMeasurementRun(reader) : null;
    }

    public void UpdateAutoMeasurementRun(AutoMeasurementRun run)
    {
        run.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AutoMeasurementRuns SET
                MeasurementId = $measurementId,
                Status = $status,
                ErrorMessage = $errorMessage,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        command.Parameters.AddWithValue("$measurementId", (object?)run.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", run.Status.ToString());
        command.Parameters.AddWithValue("$errorMessage", (object?)run.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(run.UpdatedAtUtc));
        command.Parameters.AddWithValue("$id", run.Id);
        command.ExecuteNonQuery();
    }

    private List<AutoMeasurementConfig> GetAutoMeasurementConfigsByWhere(string whereClause, int? growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM AutoMeasurementConfigs
            {whereClause}
            ORDER BY Name, Id;
        """;

        if (growId.HasValue)
        {
            command.Parameters.AddWithValue("$growId", growId.Value);
        }

        var configs = new List<AutoMeasurementConfig>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            configs.Add(MapAutoMeasurementConfig(reader));
        }
        return configs;
    }

    private List<AutoMeasurementRun> GetAutoMeasurementRunsByWhere(string whereClause, int? configId, int? growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT *
            FROM AutoMeasurementRuns
            {whereClause}
            ORDER BY ScheduledForUtc DESC, Id DESC;
        """;

        if (configId.HasValue)
        {
            command.Parameters.AddWithValue("$configId", configId.Value);
        }
        if (growId.HasValue)
        {
            command.Parameters.AddWithValue("$growId", growId.Value);
        }

        var runs = new List<AutoMeasurementRun>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            runs.Add(MapAutoMeasurementRun(reader));
        }
        return runs;
    }

    private static AutoMeasurementConfig MapAutoMeasurementConfig(SqliteDataReader reader)
    {
        return new AutoMeasurementConfig
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            TentId = reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), AutoMeasurementStatus.Enabled),
            TriggerKind = ParseEnum(reader["TriggerKind"]?.ToString(), AutoMeasurementTriggerKind.Manual),
            DelayMinutes = reader["DelayMinutes"] is DBNull or null ? null : Convert.ToInt32(reader["DelayMinutes"], CultureInfo.InvariantCulture),
            WindowMinutes = Convert.ToInt32(reader["WindowMinutes"], CultureInfo.InvariantCulture),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static AutoMeasurementFieldMapping MapAutoMeasurementFieldMapping(SqliteDataReader reader)
    {
        return new AutoMeasurementFieldMapping
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            ConfigId = Convert.ToInt32(reader["ConfigId"], CultureInfo.InvariantCulture),
            MeasurementField = ParseEnum(reader["MeasurementField"]?.ToString(), AutoMeasurementField.AirTemperatureC),
            MetricKey = reader["MetricKey"]?.ToString() ?? string.Empty,
            Aggregation = ParseEnum(reader["Aggregation"]?.ToString(), AutoMeasurementAggregation.Latest),
            IsRequired = reader["IsRequired"] is not DBNull and not null && Convert.ToInt32(reader["IsRequired"], CultureInfo.InvariantCulture) == 1,
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static AutoMeasurementRun MapAutoMeasurementRun(SqliteDataReader reader)
    {
        return new AutoMeasurementRun
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            ConfigId = Convert.ToInt32(reader["ConfigId"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            TriggerKind = ParseEnum(reader["TriggerKind"]?.ToString(), AutoMeasurementTriggerKind.Manual),
            ScheduledForUtc = ParseStoredDateTime(reader["ScheduledForUtc"]?.ToString()) ?? DateTime.UtcNow,
            MeasurementId = reader["MeasurementId"] is DBNull or null ? null : Convert.ToInt32(reader["MeasurementId"], CultureInfo.InvariantCulture),
            Status = ParseEnum(reader["Status"]?.ToString(), AutoMeasurementRunStatus.Pending),
            ErrorMessage = NullString(reader["ErrorMessage"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddAutoMeasurementConfigParameters(SqliteCommand command, AutoMeasurementConfig config)
    {
        command.Parameters.AddWithValue("$growId", config.GrowId);
        command.Parameters.AddWithValue("$tentId", (object?)config.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", config.Name);
        command.Parameters.AddWithValue("$status", config.Status.ToString());
        command.Parameters.AddWithValue("$triggerKind", config.TriggerKind.ToString());
        command.Parameters.AddWithValue("$delayMinutes", (object?)config.DelayMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$windowMinutes", config.WindowMinutes);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(config.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(config.UpdatedAtUtc));
    }

    private static void AddAutoMeasurementFieldMappingParameters(SqliteCommand command, AutoMeasurementFieldMapping mapping)
    {
        command.Parameters.AddWithValue("$configId", mapping.ConfigId);
        command.Parameters.AddWithValue("$measurementField", mapping.MeasurementField.ToString());
        command.Parameters.AddWithValue("$metricKey", mapping.MetricKey);
        command.Parameters.AddWithValue("$aggregation", mapping.Aggregation.ToString());
        command.Parameters.AddWithValue("$isRequired", mapping.IsRequired ? 1 : 0);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(mapping.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(mapping.UpdatedAtUtc));
    }

    private static void AddAutoMeasurementRunParameters(SqliteCommand command, AutoMeasurementRun run)
    {
        command.Parameters.AddWithValue("$configId", run.ConfigId);
        command.Parameters.AddWithValue("$growId", run.GrowId);
        command.Parameters.AddWithValue("$triggerKind", run.TriggerKind.ToString());
        command.Parameters.AddWithValue("$scheduledForUtc", ToStorageUtc(run.ScheduledForUtc));
        command.Parameters.AddWithValue("$measurementId", (object?)run.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", run.Status.ToString());
        command.Parameters.AddWithValue("$errorMessage", (object?)run.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(run.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(run.UpdatedAtUtc));
    }
}
