using System.Globalization;
using System.Text.Json;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge.Schema;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class SopRepository : RepositoryBase
{
    public SopRepository(AppPaths paths) : base(paths)
    {
    }

    public SopInstance StartSopInstance(
        int growId,
        SopDefinition sopDefinition,
        SopStartSource source,
        string? sourceRecommendationKey,
        string? treatmentRecommendationStableKey,
        string? notes)
    {
        var now = DateTime.UtcNow;

        var isRecurring = string.Equals(sopDefinition.Type, "Recurring", StringComparison.OrdinalIgnoreCase);
        var recurrenceIntervalDays = sopDefinition.Triggers
            .FirstOrDefault(t => string.Equals(t.Type, "Schedule", StringComparison.OrdinalIgnoreCase))
            ?.IntervalDays ?? sopDefinition.IntervalDays;
        DateTime? instanceDueAt = string.Equals(sopDefinition.Type, "MultiDay", StringComparison.OrdinalIgnoreCase) && sopDefinition.DurationDays.HasValue
            ? now.AddDays(sopDefinition.DurationDays.Value)
            : isRecurring && recurrenceIntervalDays.HasValue
                ? now.AddDays(recurrenceIntervalDays.Value)
                : null;

        var orderedStepDefs = sopDefinition.Steps.OrderBy(s => s.Order).ToList();
        DateTime? nextStepDue = null;
        for (var i = 0; i < orderedStepDefs.Count; i++)
        {
            DateTime? stepDue = orderedStepDefs[i].WaitMinutes.HasValue
                ? now.AddMinutes(orderedStepDefs[i].WaitMinutes!.Value)
                : i == 0 ? now : null;
            if (stepDue.HasValue && (nextStepDue is null || stepDue.Value < nextStepDue.Value))
                nextStepDue = stepDue;
        }

        var instance = new SopInstance
        {
            GrowId = growId,
            SopId = sopDefinition.Id,
            SopName = sopDefinition.Name,
            SopType = sopDefinition.Type,
            Status = SopInstanceStatus.Active,
            Source = source,
            SourceRecommendationKey = NormalizeOptional(sourceRecommendationKey),
            TreatmentRecommendationStableKey = NormalizeOptional(treatmentRecommendationStableKey),
            StartedAtUtc = now,
            DueAtUtc = instanceDueAt,
            NextStepDueAtUtc = nextStepDue,
            IsRecurring = isRecurring,
            RecurrenceIntervalDays = recurrenceIntervalDays,
            Notes = NormalizeOptional(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = """
                SELECT COUNT(*)
                FROM SopInstances
                WHERE GrowId = $growId
                  AND SopId = $sopId
                  AND Status = $status;
            """;
            duplicateCommand.Parameters.AddWithValue("$growId", growId);
            duplicateCommand.Parameters.AddWithValue("$sopId", sopDefinition.Id);
            duplicateCommand.Parameters.AddWithValue("$status", SopInstanceStatus.Active.ToString());
            if (Convert.ToInt32(duplicateCommand.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
            {
                throw new InvalidOperationException("An active SOP instance already exists for this grow and sopId.");
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO SopInstances (
                    GrowId, SopId, SopName, SopType, Status, Source, SourceRecommendationKey,
                    TreatmentRecommendationStableKey, StartedAtUtc, CompletedAtUtc, CancelledAtUtc,
                    DueAtUtc, NextStepDueAtUtc, RecurrenceIntervalDays, IsRecurring,
                    Notes, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $growId, $sopId, $sopName, $sopType, $status, $source, $sourceRecommendationKey,
                    $treatmentRecommendationStableKey, $startedAtUtc, $completedAtUtc, $cancelledAtUtc,
                    $dueAtUtc, $nextStepDueAtUtc, $recurrenceIntervalDays, $isRecurring,
                    $notes, $createdAtUtc, $updatedAtUtc
                );
                SELECT last_insert_rowid();
            """;
            AddSopInstanceParameters(insertCommand, instance);
            instance.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);
        }

        for (var idx = 0; idx < orderedStepDefs.Count; idx++)
        {
            var stepDefinition = orderedStepDefs[idx];

            DateTime? stepDueAt;
            DateTime? stepAvailableAt;
            if (stepDefinition.WaitMinutes.HasValue)
            {
                stepDueAt = now.AddMinutes(stepDefinition.WaitMinutes.Value);
                stepAvailableAt = stepDueAt;
            }
            else
            {
                stepDueAt = idx == 0 ? now : null;
                stepAvailableAt = null;
            }

            var step = new SopStepInstance
            {
                SopInstanceId = instance.Id,
                StepId = stepDefinition.Id,
                Order = stepDefinition.Order,
                Title = stepDefinition.Title,
                Description = NormalizeOptional(stepDefinition.Description),
                StepType = stepDefinition.StepType,
                Status = SopStepInstanceStatus.Pending,
                WaitMinutes = stepDefinition.WaitMinutes,
                SubSopId = NormalizeOptional(stepDefinition.SubSopId),
                ExpectedInputsJson = stepDefinition.ExpectedInputs is { Count: > 0 }
                    ? JsonSerializer.Serialize(stepDefinition.ExpectedInputs)
                    : null,
                PhotoRequired = stepDefinition.PhotoRequired,
                PhotoRecommended = stepDefinition.PhotoRecommended,
                DueAtUtc = stepDueAt,
                AvailableAtUtc = stepAvailableAt,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            using var stepCommand = connection.CreateCommand();
            stepCommand.Transaction = transaction;
            stepCommand.CommandText = """
                INSERT INTO SopStepInstances (
                    SopInstanceId, StepId, "Order", Title, Description, StepType, Status,
                    WaitMinutes, SubSopId, ExpectedInputsJson, PhotoRequired, PhotoRecommended,
                    DueAtUtc, AvailableAtUtc, ReminderTaskId,
                    StartedAtUtc, CompletedAtUtc, SkippedAtUtc, Notes, MeasurementId, JournalEntryId,
                    PhotoAssetId, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $sopInstanceId, $stepId, $order, $title, $description, $stepType, $status,
                    $waitMinutes, $subSopId, $expectedInputsJson, $photoRequired, $photoRecommended,
                    $dueAtUtc, $availableAtUtc, $reminderTaskId,
                    $startedAtUtc, $completedAtUtc, $skippedAtUtc, $notes, $measurementId, $journalEntryId,
                    $photoAssetId, $createdAtUtc, $updatedAtUtc
                );
            """;
            AddSopStepInstanceParameters(stepCommand, step);
            stepCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        instance.StepCount = sopDefinition.Steps.Count;
        return instance;
    }

    public SopInstance? GetSopInstance(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT si.*, COUNT(ssi.Id) AS StepCount
            FROM SopInstances si
            LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
            WHERE si.Id = $id
            GROUP BY si.Id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSopInstance(reader) : null;
    }

    public List<SopInstance> GetSopInstancesByGrow(int growId)
        => GetSopInstancesByGrow(growId, activeOnly: false);

    public List<SopInstance> GetActiveSopInstancesByGrow(int growId)
        => GetSopInstancesByGrow(growId, activeOnly: true);

    public List<SopStepInstance> GetSopStepInstances(int sopInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM SopStepInstances
            WHERE SopInstanceId = $sopInstanceId
            ORDER BY "Order", Id;
        """;
        command.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);

        var steps = new List<SopStepInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            steps.Add(MapSopStepInstance(reader));
        }

        return steps;
    }

    public SopStepInstance? GetSopStepInstance(int stepInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM SopStepInstances
            WHERE Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", stepInstanceId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSopStepInstance(reader) : null;
    }

    public SopStepInstance UpdateSopStepInstance(
        int stepInstanceId,
        SopStepInstanceStatus status,
        string? notes,
        int? measurementId,
        int? journalEntryId,
        int? photoAssetId)
    {
        var now = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        SopStepInstance step;
        SopInstance instance;
        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT ssi.*
                FROM SopStepInstances ssi
                WHERE ssi.Id = $id
                LIMIT 1;
            """;
            selectCommand.Parameters.AddWithValue("$id", stepInstanceId);
            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"SOP step instance with id {stepInstanceId} does not exist.");
            }

            step = MapSopStepInstance(reader);
        }

        using (var instanceCommand = connection.CreateCommand())
        {
            instanceCommand.Transaction = transaction;
            instanceCommand.CommandText = """
                SELECT si.*, COUNT(ssi.Id) AS StepCount
                FROM SopInstances si
                LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
                WHERE si.Id = $id
                GROUP BY si.Id
                LIMIT 1;
            """;
            instanceCommand.Parameters.AddWithValue("$id", step.SopInstanceId);
            using var reader = instanceCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"SOP instance with id {step.SopInstanceId} does not exist.");
            }

            instance = MapSopInstance(reader);
        }

        if (instance.Status != SopInstanceStatus.Active)
        {
            throw new InvalidOperationException("SOP instance is not active.");
        }

        step.Status = status;
        step.Notes = NormalizeOptional(notes);
        step.MeasurementId = measurementId;
        step.JournalEntryId = journalEntryId;
        step.PhotoAssetId = photoAssetId;
        step.UpdatedAtUtc = now;

        switch (status)
        {
            case SopStepInstanceStatus.Pending:
                step.StartedAtUtc = null;
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.InProgress:
                step.StartedAtUtc ??= now;
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.Done:
                step.StartedAtUtc ??= now;
                step.CompletedAtUtc = now;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.Skipped:
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = now;
                break;
        }

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE SopStepInstances
                SET Status = $status,
                    StartedAtUtc = $startedAtUtc,
                    CompletedAtUtc = $completedAtUtc,
                    SkippedAtUtc = $skippedAtUtc,
                    Notes = $notes,
                    MeasurementId = $measurementId,
                    JournalEntryId = $journalEntryId,
                    PhotoAssetId = $photoAssetId,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id;
            """;
            updateCommand.Parameters.AddWithValue("$id", step.Id);
            updateCommand.Parameters.AddWithValue("$status", step.Status.ToString());
            updateCommand.Parameters.AddWithValue("$startedAtUtc", step.StartedAtUtc.HasValue ? ToStorageUtc(step.StartedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$completedAtUtc", step.CompletedAtUtc.HasValue ? ToStorageUtc(step.CompletedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$skippedAtUtc", step.SkippedAtUtc.HasValue ? ToStorageUtc(step.SkippedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$notes", (object?)step.Notes ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$measurementId", (object?)step.MeasurementId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$journalEntryId", (object?)step.JournalEntryId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$photoAssetId", (object?)step.PhotoAssetId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(step.UpdatedAtUtc));
            updateCommand.ExecuteNonQuery();
        }

        RecalculateSopInstanceStatus(connection, transaction, step.SopInstanceId, now);
        transaction.Commit();

        return GetSopStepInstance(step.Id)!;
    }

    public void RecalculateSopInstanceStatus(int sopInstanceId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        RecalculateSopInstanceStatus(connection, transaction, sopInstanceId, DateTime.UtcNow);
        transaction.Commit();
    }

    public void UpdateSopStepReminderTaskId(int stepId, int taskId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE SopStepInstances
            SET ReminderTaskId = $taskId,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        command.Parameters.AddWithValue("$id", stepId);
        command.Parameters.AddWithValue("$taskId", taskId);
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
        command.ExecuteNonQuery();
    }

    private List<SopInstance> GetSopInstancesByGrow(int growId, bool activeOnly)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var statusFilter = activeOnly ? "AND si.Status = $status" : string.Empty;
        command.CommandText = $"""
            SELECT si.*, COUNT(ssi.Id) AS StepCount
            FROM SopInstances si
            LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
            WHERE si.GrowId = $growId {statusFilter}
            GROUP BY si.Id
            ORDER BY si.StartedAtUtc DESC, si.Id DESC;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        if (activeOnly)
        {
            command.Parameters.AddWithValue("$status", SopInstanceStatus.Active.ToString());
        }

        var instances = new List<SopInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            instances.Add(MapSopInstance(reader));
        }
        return instances;
    }

    private static void RecalculateSopInstanceStatus(SqliteConnection connection, SqliteTransaction transaction, int sopInstanceId, DateTime now)
    {
        int totalSteps;
        int openSteps;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = """
                SELECT
                    COUNT(*) AS TotalSteps,
                    COALESCE(SUM(CASE WHEN Status IN ('Done', 'Skipped') THEN 0 ELSE 1 END), 0) AS OpenSteps
                FROM SopStepInstances
                WHERE SopInstanceId = $sopInstanceId;
            """;
            countCommand.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);
            using var reader = countCommand.ExecuteReader();
            if (!reader.Read())
                return;
            totalSteps = Convert.ToInt32(reader["TotalSteps"], CultureInfo.InvariantCulture);
            openSteps = Convert.ToInt32(reader["OpenSteps"], CultureInfo.InvariantCulture);
        }

        if (totalSteps == 0)
            return;

        if (openSteps == 0)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE SopInstances
                SET Status = $status,
                    CompletedAtUtc = $completedAtUtc,
                    NextStepDueAtUtc = NULL,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id
                  AND Status = $activeStatus;
            """;
            updateCommand.Parameters.AddWithValue("$id", sopInstanceId);
            updateCommand.Parameters.AddWithValue("$status", SopInstanceStatus.Completed.ToString());
            updateCommand.Parameters.AddWithValue("$activeStatus", SopInstanceStatus.Active.ToString());
            updateCommand.Parameters.AddWithValue("$completedAtUtc", ToStorageUtc(now));
            updateCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
            updateCommand.ExecuteNonQuery();
        }
        else
        {
            string? minDueRaw;
            using (var dueCommand = connection.CreateCommand())
            {
                dueCommand.Transaction = transaction;
                dueCommand.CommandText = """
                    SELECT MIN(COALESCE(DueAtUtc, AvailableAtUtc)) AS MinDue
                    FROM SopStepInstances
                    WHERE SopInstanceId = $sopInstanceId
                      AND Status NOT IN ('Done', 'Skipped');
                """;
                dueCommand.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);
                var raw = dueCommand.ExecuteScalar();
                minDueRaw = raw is DBNull or null ? null : raw.ToString();
            }

            using var updateNextCommand = connection.CreateCommand();
            updateNextCommand.Transaction = transaction;
            updateNextCommand.CommandText = """
                UPDATE SopInstances
                SET NextStepDueAtUtc = $nextStepDueAtUtc,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id;
            """;
            updateNextCommand.Parameters.AddWithValue("$id", sopInstanceId);
            updateNextCommand.Parameters.AddWithValue("$nextStepDueAtUtc", minDueRaw is not null ? (object)minDueRaw : DBNull.Value);
            updateNextCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
            updateNextCommand.ExecuteNonQuery();
        }
    }

    private static SopInstance MapSopInstance(SqliteDataReader reader)
    {
        return new SopInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            SopId = reader["SopId"]?.ToString() ?? string.Empty,
            SopName = reader["SopName"]?.ToString() ?? string.Empty,
            SopType = reader["SopType"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), SopInstanceStatus.Active),
            Source = ParseEnum(reader["Source"]?.ToString(), SopStartSource.Manual),
            SourceRecommendationKey = NullString(reader["SourceRecommendationKey"]),
            TreatmentRecommendationStableKey = NullString(reader["TreatmentRecommendationStableKey"]),
            StartedAtUtc = ParseStoredDateTime(reader["StartedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            CompletedAtUtc = ParseStoredDateTime(reader["CompletedAtUtc"]?.ToString()),
            CancelledAtUtc = ParseStoredDateTime(reader["CancelledAtUtc"]?.ToString()),
            DueAtUtc = ParseStoredDateTimeIfColumn(reader, "DueAtUtc"),
            NextStepDueAtUtc = ParseStoredDateTimeIfColumn(reader, "NextStepDueAtUtc"),
            RecurrenceIntervalDays = HasColumn(reader, "RecurrenceIntervalDays") && reader["RecurrenceIntervalDays"] is not DBNull
                ? Convert.ToInt32(reader["RecurrenceIntervalDays"], CultureInfo.InvariantCulture)
                : null,
            IsRecurring = HasColumn(reader, "IsRecurring") && reader["IsRecurring"] is not DBNull
                && Convert.ToInt32(reader["IsRecurring"], CultureInfo.InvariantCulture) == 1,
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            StepCount = HasColumn(reader, "StepCount") && reader["StepCount"] is not DBNull
                ? Convert.ToInt32(reader["StepCount"], CultureInfo.InvariantCulture)
                : 0
        };
    }

    private static SopStepInstance MapSopStepInstance(SqliteDataReader reader)
    {
        return new SopStepInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            SopInstanceId = Convert.ToInt32(reader["SopInstanceId"], CultureInfo.InvariantCulture),
            StepId = reader["StepId"]?.ToString() ?? string.Empty,
            Order = Convert.ToInt32(reader["Order"], CultureInfo.InvariantCulture),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            Description = NullString(reader["Description"]),
            StepType = reader["StepType"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), SopStepInstanceStatus.Pending),
            WaitMinutes = reader["WaitMinutes"] is DBNull or null ? null : Convert.ToInt32(reader["WaitMinutes"], CultureInfo.InvariantCulture),
            SubSopId = NullString(reader["SubSopId"]),
            ExpectedInputsJson = NullString(reader["ExpectedInputsJson"]),
            PhotoRequired = reader["PhotoRequired"] is not DBNull and not null && Convert.ToInt32(reader["PhotoRequired"], CultureInfo.InvariantCulture) == 1,
            PhotoRecommended = reader["PhotoRecommended"] is not DBNull and not null && Convert.ToInt32(reader["PhotoRecommended"], CultureInfo.InvariantCulture) == 1,
            DueAtUtc = ParseStoredDateTimeIfColumn(reader, "DueAtUtc"),
            AvailableAtUtc = ParseStoredDateTimeIfColumn(reader, "AvailableAtUtc"),
            ReminderTaskId = HasColumn(reader, "ReminderTaskId") && reader["ReminderTaskId"] is not DBNull
                ? Convert.ToInt32(reader["ReminderTaskId"], CultureInfo.InvariantCulture)
                : null,
            StartedAtUtc = ParseStoredDateTime(reader["StartedAtUtc"]?.ToString()),
            CompletedAtUtc = ParseStoredDateTime(reader["CompletedAtUtc"]?.ToString()),
            SkippedAtUtc = ParseStoredDateTime(reader["SkippedAtUtc"]?.ToString()),
            Notes = NullString(reader["Notes"]),
            MeasurementId = reader["MeasurementId"] is DBNull or null ? null : Convert.ToInt32(reader["MeasurementId"], CultureInfo.InvariantCulture),
            JournalEntryId = reader["JournalEntryId"] is DBNull or null ? null : Convert.ToInt32(reader["JournalEntryId"], CultureInfo.InvariantCulture),
            PhotoAssetId = reader["PhotoAssetId"] is DBNull or null ? null : Convert.ToInt32(reader["PhotoAssetId"], CultureInfo.InvariantCulture),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddSopInstanceParameters(SqliteCommand command, SopInstance instance)
    {
        command.Parameters.AddWithValue("$growId", instance.GrowId);
        command.Parameters.AddWithValue("$sopId", instance.SopId);
        command.Parameters.AddWithValue("$sopName", instance.SopName);
        command.Parameters.AddWithValue("$sopType", instance.SopType);
        command.Parameters.AddWithValue("$status", instance.Status.ToString());
        command.Parameters.AddWithValue("$source", instance.Source.ToString());
        command.Parameters.AddWithValue("$sourceRecommendationKey", (object?)instance.SourceRecommendationKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$treatmentRecommendationStableKey", (object?)instance.TreatmentRecommendationStableKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", ToStorageUtc(instance.StartedAtUtc));
        command.Parameters.AddWithValue("$completedAtUtc", instance.CompletedAtUtc.HasValue ? ToStorageUtc(instance.CompletedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$cancelledAtUtc", instance.CancelledAtUtc.HasValue ? ToStorageUtc(instance.CancelledAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", instance.DueAtUtc.HasValue ? ToStorageUtc(instance.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nextStepDueAtUtc", instance.NextStepDueAtUtc.HasValue ? ToStorageUtc(instance.NextStepDueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$recurrenceIntervalDays", (object?)instance.RecurrenceIntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$isRecurring", instance.IsRecurring ? 1 : 0);
        command.Parameters.AddWithValue("$notes", (object?)instance.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(instance.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(instance.UpdatedAtUtc));
    }

    private static void AddSopStepInstanceParameters(SqliteCommand command, SopStepInstance step)
    {
        command.Parameters.AddWithValue("$sopInstanceId", step.SopInstanceId);
        command.Parameters.AddWithValue("$stepId", step.StepId);
        command.Parameters.AddWithValue("$order", step.Order);
        command.Parameters.AddWithValue("$title", step.Title);
        command.Parameters.AddWithValue("$description", (object?)step.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$stepType", step.StepType);
        command.Parameters.AddWithValue("$status", step.Status.ToString());
        command.Parameters.AddWithValue("$waitMinutes", (object?)step.WaitMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$subSopId", (object?)step.SubSopId ?? DBNull.Value);
        command.Parameters.AddWithValue("$expectedInputsJson", (object?)step.ExpectedInputsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$photoRequired", step.PhotoRequired ? 1 : 0);
        command.Parameters.AddWithValue("$photoRecommended", step.PhotoRecommended ? 1 : 0);
        command.Parameters.AddWithValue("$dueAtUtc", step.DueAtUtc.HasValue ? ToStorageUtc(step.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$availableAtUtc", step.AvailableAtUtc.HasValue ? ToStorageUtc(step.AvailableAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$reminderTaskId", (object?)step.ReminderTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", step.StartedAtUtc.HasValue ? ToStorageUtc(step.StartedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$completedAtUtc", step.CompletedAtUtc.HasValue ? ToStorageUtc(step.CompletedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$skippedAtUtc", step.SkippedAtUtc.HasValue ? ToStorageUtc(step.SkippedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)step.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$measurementId", (object?)step.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$journalEntryId", (object?)step.JournalEntryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$photoAssetId", (object?)step.PhotoAssetId ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(step.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(step.UpdatedAtUtc));
    }
}
