using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class SensorReadingRepository
{
    private readonly AppPaths _paths;

    public SensorReadingRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public void AddReading(TentSensorReading reading)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TentSensorReadings (TentId, MetricKey, Value, Unit, CapturedAtUtc)
            VALUES ($tentId, $metricKey, $value, $unit, $capturedAtUtc);
            """;
        cmd.Parameters.AddWithValue("$tentId", reading.TentId);
        cmd.Parameters.AddWithValue("$metricKey", reading.MetricKey);
        cmd.Parameters.AddWithValue("$value", reading.Value);
        cmd.Parameters.AddWithValue("$unit", (object?)reading.Unit ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$capturedAtUtc", reading.CapturedAtUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<TentSensorReading> GetReadings(
        int tentId, string metricKey, DateTime fromUtc, DateTime toUtc)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, TentId, MetricKey, Value, Unit, CapturedAtUtc
            FROM TentSensorReadings
            WHERE TentId = $tentId
              AND MetricKey = $metricKey
              AND CapturedAtUtc >= $from
              AND CapturedAtUtc <= $to
            ORDER BY CapturedAtUtc ASC;
            """;
        cmd.Parameters.AddWithValue("$tentId", tentId);
        cmd.Parameters.AddWithValue("$metricKey", metricKey);
        cmd.Parameters.AddWithValue("$from", fromUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$to", toUtc.ToString("O"));
        return ReadReadings(cmd);
    }

    public IReadOnlyList<TentSensorReading> GetReadingsForDay(
        int tentId, string metricKey, DateOnly date)
    {
        var from = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        return GetReadings(tentId, metricKey, from, to);
    }

    public void DeleteOlderThan(DateTime cutoffUtc)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM TentSensorReadings WHERE CapturedAtUtc < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", cutoffUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void UpsertDailyStat(TentSensorDailyStat stat)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TentSensorDailyStats
                (TentId, MetricKey, Date, Min, Max, Median, P5, P95, Avg, Count, Unit)
            VALUES
                ($tentId, $metricKey, $date, $min, $max, $median, $p5, $p95, $avg, $count, $unit)
            ON CONFLICT(TentId, MetricKey, Date) DO UPDATE SET
                Min    = excluded.Min,
                Max    = excluded.Max,
                Median = excluded.Median,
                P5     = excluded.P5,
                P95    = excluded.P95,
                Avg    = excluded.Avg,
                Count  = excluded.Count,
                Unit   = excluded.Unit;
            """;
        cmd.Parameters.AddWithValue("$tentId",    stat.TentId);
        cmd.Parameters.AddWithValue("$metricKey", stat.MetricKey);
        cmd.Parameters.AddWithValue("$date",      stat.Date.ToString("O"));
        cmd.Parameters.AddWithValue("$min",       stat.Min);
        cmd.Parameters.AddWithValue("$max",       stat.Max);
        cmd.Parameters.AddWithValue("$median",    stat.Median);
        cmd.Parameters.AddWithValue("$p5",        stat.P5);
        cmd.Parameters.AddWithValue("$p95",       stat.P95);
        cmd.Parameters.AddWithValue("$avg",       stat.Avg);
        cmd.Parameters.AddWithValue("$count",     stat.Count);
        cmd.Parameters.AddWithValue("$unit",      (object?)stat.Unit ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<TentSensorDailyStat> GetDailyStats(
        int tentId, string metricKey, DateOnly from, DateOnly to)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, TentId, MetricKey, Date, Min, Max, Median, P5, P95, Avg, Count, Unit
            FROM TentSensorDailyStats
            WHERE TentId = $tentId
              AND MetricKey = $metricKey
              AND Date >= $from
              AND Date <= $to
            ORDER BY Date ASC;
            """;
        cmd.Parameters.AddWithValue("$tentId",    tentId);
        cmd.Parameters.AddWithValue("$metricKey", metricKey);
        cmd.Parameters.AddWithValue("$from",      from.ToString("O"));
        cmd.Parameters.AddWithValue("$to",        to.ToString("O"));

        using var reader = cmd.ExecuteReader();
        var list = new List<TentSensorDailyStat>();
        while (reader.Read())
        {
            list.Add(new TentSensorDailyStat
            {
                Id        = Convert.ToInt32(reader["Id"]),
                TentId    = Convert.ToInt32(reader["TentId"]),
                MetricKey = reader["MetricKey"].ToString()!,
                Date      = DateOnly.Parse(reader["Date"].ToString()!),
                Min       = Convert.ToDouble(reader["Min"]),
                Max       = Convert.ToDouble(reader["Max"]),
                Median    = Convert.ToDouble(reader["Median"]),
                P5        = Convert.ToDouble(reader["P5"]),
                P95       = Convert.ToDouble(reader["P95"]),
                Avg       = Convert.ToDouble(reader["Avg"]),
                Count     = Convert.ToInt32(reader["Count"]),
                Unit      = reader["Unit"] as string
            });
        }
        return list;
    }

    // ── Hilfsmethode: Rohdaten lesen ─────────────────────────────────────────

    private static IReadOnlyList<TentSensorReading> ReadReadings(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<TentSensorReading>();
        while (reader.Read())
        {
            list.Add(new TentSensorReading
            {
                Id           = Convert.ToInt32(reader["Id"]),
                TentId       = Convert.ToInt32(reader["TentId"]),
                MetricKey    = reader["MetricKey"].ToString()!,
                Value        = Convert.ToDouble(reader["Value"]),
                Unit         = reader["Unit"] as string,
                CapturedAtUtc = DateTime.Parse(reader["CapturedAtUtc"].ToString()!, null,
                    System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }
        return list;
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
