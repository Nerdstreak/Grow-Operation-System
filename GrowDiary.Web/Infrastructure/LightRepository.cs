using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class LightRepository : RepositoryBase
{
    public LightRepository(AppPaths paths) : base(paths)
    {
    }

    public LightSchedule CreateLightSchedule(LightSchedule schedule)
    {
        schedule.CreatedAtUtc = DateTime.UtcNow;
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LightSchedules (
                TentId, Name, IsActive, LightsOnTime, LightsOffTime, TimeZoneId, Source,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $tentId, $name, $isActive, $lightsOnTime, $lightsOffTime, $timeZoneId, $source,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddLightScheduleParameters(command, schedule);
        schedule.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return schedule;
    }

    public void UpdateLightSchedule(LightSchedule schedule)
    {
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE LightSchedules SET
                TentId = $tentId,
                Name = $name,
                IsActive = $isActive,
                LightsOnTime = $lightsOnTime,
                LightsOffTime = $lightsOffTime,
                TimeZoneId = $timeZoneId,
                Source = $source,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddLightScheduleParameters(command, schedule);
        command.Parameters.AddWithValue("$id", schedule.Id);
        command.ExecuteNonQuery();
    }

    public LightSchedule? GetLightSchedule(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM LightSchedules WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightSchedule(reader) : null;
    }

    public List<LightSchedule> GetLightSchedulesByTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightSchedules
            WHERE TentId = $tentId
            ORDER BY IsActive DESC, Name, Id;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var schedules = new List<LightSchedule>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            schedules.Add(MapLightSchedule(reader));
        }
        return schedules;
    }

    public LightSchedule? GetActiveLightScheduleForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightSchedules
            WHERE TentId = $tentId AND IsActive = 1
            ORDER BY UpdatedAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightSchedule(reader) : null;
    }

    public LightTransitionEvent CreateLightTransitionIfNotDuplicate(LightTransitionEvent transition)
    {
        transition.OccurredAtUtc = transition.OccurredAtUtc.ToUniversalTime();
        transition.CreatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = """
                SELECT *
                FROM LightTransitionEvents
                WHERE TentId = $tentId
                  AND Kind = $kind
                  AND OccurredAtUtc >= $fromUtc
                  AND OccurredAtUtc <= $toUtc
                ORDER BY OccurredAtUtc DESC, Id DESC
                LIMIT 1;
            """;
            duplicateCommand.Parameters.AddWithValue("$tentId", transition.TentId);
            duplicateCommand.Parameters.AddWithValue("$kind", transition.Kind.ToString());
            duplicateCommand.Parameters.AddWithValue("$fromUtc", ToStorageUtc(transition.OccurredAtUtc.AddMinutes(-2)));
            duplicateCommand.Parameters.AddWithValue("$toUtc", ToStorageUtc(transition.OccurredAtUtc.AddMinutes(2)));
            using var reader = duplicateCommand.ExecuteReader();
            if (reader.Read())
            {
                var existing = MapLightTransitionEvent(reader);
                transaction.Commit();
                return existing;
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO LightTransitionEvents (
                    TentId, Kind, OccurredAtUtc, Source, RawState, CreatedAtUtc
                )
                VALUES (
                    $tentId, $kind, $occurredAtUtc, $source, $rawState, $createdAtUtc
                );
                SELECT last_insert_rowid();
            """;
            AddLightTransitionParameters(insertCommand, transition);
            transition.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);
        }

        transaction.Commit();
        return transition;
    }

    public List<LightTransitionEvent> GetLightTransitionsByTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
            ORDER BY OccurredAtUtc DESC, Id DESC;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var transitions = new List<LightTransitionEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transitions.Add(MapLightTransitionEvent(reader));
        }
        return transitions;
    }

    public List<LightTransitionEvent> GetLightTransitionsByTentAndKindSince(int tentId, LightTransitionKind kind, DateTime sinceUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
              AND Kind = $kind
              AND OccurredAtUtc >= $sinceUtc
            ORDER BY OccurredAtUtc ASC, Id ASC;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$sinceUtc", ToStorageUtc(sinceUtc));

        var transitions = new List<LightTransitionEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transitions.Add(MapLightTransitionEvent(reader));
        }
        return transitions;
    }

    public LightTransitionEvent? GetLatestLightTransitionForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
            ORDER BY OccurredAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightTransitionEvent(reader) : null;
    }

    public LightTransitionEvent? GetLatestLightTransitionForTentAndKind(int tentId, LightTransitionKind kind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
              AND Kind = $kind
            ORDER BY OccurredAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightTransitionEvent(reader) : null;
    }

    private static LightSchedule MapLightSchedule(SqliteDataReader reader)
    {
        return new LightSchedule
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            IsActive = reader["IsActive"] is not DBNull and not null && Convert.ToInt32(reader["IsActive"], CultureInfo.InvariantCulture) == 1,
            LightsOnTime = reader["LightsOnTime"]?.ToString() ?? string.Empty,
            LightsOffTime = reader["LightsOffTime"]?.ToString() ?? string.Empty,
            TimeZoneId = NullString(reader["TimeZoneId"]),
            Source = ParseEnum(reader["Source"]?.ToString(), LightSource.Manual),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static LightTransitionEvent MapLightTransitionEvent(SqliteDataReader reader)
    {
        return new LightTransitionEvent
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Kind = ParseEnum(reader["Kind"]?.ToString(), LightTransitionKind.LightOn),
            OccurredAtUtc = ParseStoredDateTime(reader["OccurredAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            Source = ParseEnum(reader["Source"]?.ToString(), LightSource.HomeAssistant),
            RawState = NullString(reader["RawState"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddLightScheduleParameters(SqliteCommand command, LightSchedule schedule)
    {
        command.Parameters.AddWithValue("$tentId", schedule.TentId);
        command.Parameters.AddWithValue("$name", schedule.Name);
        command.Parameters.AddWithValue("$isActive", schedule.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$lightsOnTime", schedule.LightsOnTime);
        command.Parameters.AddWithValue("$lightsOffTime", schedule.LightsOffTime);
        command.Parameters.AddWithValue("$timeZoneId", (object?)schedule.TimeZoneId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", schedule.Source.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(schedule.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(schedule.UpdatedAtUtc));
    }

    private static void AddLightTransitionParameters(SqliteCommand command, LightTransitionEvent transition)
    {
        command.Parameters.AddWithValue("$tentId", transition.TentId);
        command.Parameters.AddWithValue("$kind", transition.Kind.ToString());
        command.Parameters.AddWithValue("$occurredAtUtc", ToStorageUtc(transition.OccurredAtUtc));
        command.Parameters.AddWithValue("$source", transition.Source.ToString());
        command.Parameters.AddWithValue("$rawState", (object?)transition.RawState ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(transition.CreatedAtUtc));
    }
}
