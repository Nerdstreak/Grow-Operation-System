using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class HarvestRepository
{
    private readonly AppPaths _paths;

    public HarvestRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public int Create(HarvestEntry entry)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO HarvestEntries
                (GrowId, HarvestedAt, WetWeightG, DryWeightG, DryDays, YieldNotes,
                 Rating, FlavorNotes, EffectNotes, NugStructure, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($growId, $harvestedAt, $wetWeightG, $dryWeightG, $dryDays, $yieldNotes,
                 $rating, $flavorNotes, $effectNotes, $nugStructure, $createdAtUtc, $updatedAtUtc);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$growId",       entry.GrowId);
        command.Parameters.AddWithValue("$harvestedAt",  entry.HarvestedAt.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$wetWeightG",   (object?)entry.WetWeightG  ?? DBNull.Value);
        command.Parameters.AddWithValue("$dryWeightG",   (object?)entry.DryWeightG  ?? DBNull.Value);
        command.Parameters.AddWithValue("$dryDays",      (object?)entry.DryDays     ?? DBNull.Value);
        command.Parameters.AddWithValue("$yieldNotes",   (object?)entry.YieldNotes  ?? DBNull.Value);
        command.Parameters.AddWithValue("$rating",       (object?)entry.Rating      ?? DBNull.Value);
        command.Parameters.AddWithValue("$flavorNotes",  (object?)entry.FlavorNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$effectNotes",  (object?)entry.EffectNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$nugStructure", (object?)entry.NugStructure ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        return Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L));
    }

    public HarvestEntry? GetForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM HarvestEntries WHERE GrowId = $growId ORDER BY HarvestedAt DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$growId", growId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return Map(reader);
    }

    public void Update(HarvestEntry entry)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE HarvestEntries SET
                HarvestedAt  = $harvestedAt,
                WetWeightG   = $wetWeightG,
                DryWeightG   = $dryWeightG,
                DryDays      = $dryDays,
                YieldNotes   = $yieldNotes,
                Rating       = $rating,
                FlavorNotes  = $flavorNotes,
                EffectNotes  = $effectNotes,
                NugStructure = $nugStructure,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id",           entry.Id);
        command.Parameters.AddWithValue("$harvestedAt",  entry.HarvestedAt.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$wetWeightG",   (object?)entry.WetWeightG  ?? DBNull.Value);
        command.Parameters.AddWithValue("$dryWeightG",   (object?)entry.DryWeightG  ?? DBNull.Value);
        command.Parameters.AddWithValue("$dryDays",      (object?)entry.DryDays     ?? DBNull.Value);
        command.Parameters.AddWithValue("$yieldNotes",   (object?)entry.YieldNotes  ?? DBNull.Value);
        command.Parameters.AddWithValue("$rating",       (object?)entry.Rating      ?? DBNull.Value);
        command.Parameters.AddWithValue("$flavorNotes",  (object?)entry.FlavorNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$effectNotes",  (object?)entry.EffectNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$nugStructure", (object?)entry.NugStructure ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static HarvestEntry Map(SqliteDataReader reader)
        => new()
        {
            Id           = Convert.ToInt32((long)reader["Id"]),
            GrowId       = Convert.ToInt32((long)reader["GrowId"]),
            HarvestedAt  = DateTime.Parse(reader["HarvestedAt"].ToString()!),
            WetWeightG   = reader["WetWeightG"]  is DBNull ? null : Convert.ToDouble(reader["WetWeightG"]),
            DryWeightG   = reader["DryWeightG"]  is DBNull ? null : Convert.ToDouble(reader["DryWeightG"]),
            DryDays      = reader["DryDays"]     is DBNull ? null : Convert.ToInt32((long)reader["DryDays"]),
            YieldNotes   = reader["YieldNotes"]  is DBNull ? null : reader["YieldNotes"].ToString(),
            Rating       = reader["Rating"]      is DBNull ? null : Convert.ToDouble(reader["Rating"]),
            FlavorNotes  = reader["FlavorNotes"] is DBNull ? null : reader["FlavorNotes"].ToString(),
            EffectNotes  = reader["EffectNotes"] is DBNull ? null : reader["EffectNotes"].ToString(),
            NugStructure = reader["NugStructure"] is DBNull ? null : reader["NugStructure"].ToString(),
            CreatedAtUtc = DateTime.Parse(reader["CreatedAtUtc"].ToString()!),
            UpdatedAtUtc = DateTime.Parse(reader["UpdatedAtUtc"].ToString()!)
        };

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
