using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

/// <summary>
/// Gläser im Aushärten und die Ablesungen daran.
/// </summary>
/// <remarks>
/// Alle Zeitspalten heißen „…Utc" und werden mit
/// <see cref="RepositoryBase.ParseStoredUtcDateTime"/> gelesen — die Falle aus
/// beta.20 (UTC-Spalte als Ortszeit gelesen) fällt auf einem CI-Rechner in UTC
/// nie auf, wohl aber beim Nutzer.
/// </remarks>
public sealed class CuringRepository : RepositoryBase
{
    public CuringRepository(AppPaths paths) : base(paths)
    {
    }

    // ---------- Gläser ----------

    public IReadOnlyList<CuringJar> GetJarsForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM CuringJars WHERE GrowId = $growId
            ORDER BY FinishedAtUtc IS NOT NULL, FilledAtUtc DESC, Id DESC;
            """;
        command.Parameters.AddWithValue("$growId", growId);
        return Lies(command);
    }

    /// <summary>Alle Gläser, die noch aushärten — über alle Grows hinweg.</summary>
    public IReadOnlyList<CuringJar> GetOpenJars()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM CuringJars WHERE FinishedAtUtc IS NULL ORDER BY FilledAtUtc;";
        return Lies(command);
    }

    public CuringJar? GetJar(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM CuringJars WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return Lies(command).FirstOrDefault();
    }

    public int CreateJar(CuringJar jar)
    {
        ArgumentNullException.ThrowIfNull(jar);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CuringJars
                (GrowId, Label, StrainId, FilledAtUtc, WeightG, HasHumidityPack, FinishedAtUtc, Notes, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($growId, $label, $strainId, $filledAtUtc, $weightG, $hasPack, $finishedAtUtc, $notes, $createdAtUtc, $updatedAtUtc);
            SELECT last_insert_rowid();
            """;
        var jetzt = DateTime.UtcNow;
        Binde(command, jar);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(jetzt));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(jetzt));
        return Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L));
    }

    public void UpdateJar(CuringJar jar)
    {
        ArgumentNullException.ThrowIfNull(jar);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE CuringJars SET
                Label = $label, StrainId = $strainId, FilledAtUtc = $filledAtUtc,
                WeightG = $weightG, HasHumidityPack = $hasPack, FinishedAtUtc = $finishedAtUtc,
                Notes = $notes, UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """;
        Binde(command, jar);
        command.Parameters.AddWithValue("$id", jar.Id);
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
        command.ExecuteNonQuery();
    }

    public void DeleteJar(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CuringJars WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // ---------- Ablesungen ----------

    public IReadOnlyList<CuringReading> GetReadings(int jarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM CuringReadings WHERE JarId = $jarId ORDER BY ReadAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$jarId", jarId);
        using var reader = command.ExecuteReader();
        var liste = new List<CuringReading>();
        while (reader.Read())
        {
            liste.Add(LiesAblesung(reader));
        }

        return liste;
    }

    /// <summary>
    /// Wann zuletzt wirklich <b>gelüftet</b> wurde — eine reine Feuchte-Ablesung
    /// zählt hier nicht, sonst würde Hinsehen als Erledigen durchgehen.
    /// </summary>
    public DateTime? GetLastBurp(int jarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ReadAtUtc FROM CuringReadings
            WHERE JarId = $jarId AND BurpedMinutes IS NOT NULL
            ORDER BY ReadAtUtc DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$jarId", jarId);
        return ParseStoredUtcDateTime(NullString(command.ExecuteScalar()));
    }

    public CuringReading? GetLatestReading(int jarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM CuringReadings
            WHERE JarId = $jarId AND HumidityPercent IS NOT NULL
            ORDER BY ReadAtUtc DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$jarId", jarId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? LiesAblesung(reader) : null;
    }

    public int CreateReading(CuringReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CuringReadings (JarId, ReadAtUtc, HumidityPercent, BurpedMinutes, Note, Source, CreatedAtUtc)
            VALUES ($jarId, $readAtUtc, $humidity, $burped, $note, $source, $createdAtUtc);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$jarId", reading.JarId);
        command.Parameters.AddWithValue("$readAtUtc", ToStorageUtc(reading.ReadAtUtc));
        AddNullable(command, "$humidity", reading.HumidityPercent);
        command.Parameters.AddWithValue("$burped", (object?)reading.BurpedMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$note", (object?)NormalizeOptional(reading.Note) ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", reading.Source.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(DateTime.UtcNow));
        return Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L));
    }

    // ---------- Abbildung ----------

    private static void Binde(SqliteCommand command, CuringJar jar)
    {
        command.Parameters.AddWithValue("$growId", jar.GrowId);
        command.Parameters.AddWithValue("$label", jar.Label);
        command.Parameters.AddWithValue("$strainId", (object?)jar.StrainId ?? DBNull.Value);
        command.Parameters.AddWithValue("$filledAtUtc", ToStorageUtc(jar.FilledAtUtc));
        AddNullable(command, "$weightG", jar.WeightG);
        command.Parameters.AddWithValue("$hasPack", jar.HasHumidityPack ? 1 : 0);
        command.Parameters.AddWithValue("$finishedAtUtc",
            jar.FinishedAtUtc.HasValue ? ToStorageUtc(jar.FinishedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)NormalizeOptional(jar.Notes) ?? DBNull.Value);
    }

    private static List<CuringJar> Lies(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var liste = new List<CuringJar>();
        while (reader.Read())
        {
            liste.Add(new CuringJar
            {
                Id = Convert.ToInt32(reader["Id"]),
                GrowId = Convert.ToInt32(reader["GrowId"]),
                Label = reader["Label"]?.ToString() ?? string.Empty,
                StrainId = reader["StrainId"] is DBNull ? null : Convert.ToInt32(reader["StrainId"]),
                FilledAtUtc = ParseStoredUtcDateTime(NullString(reader["FilledAtUtc"])) ?? DateTime.UtcNow,
                WeightG = NullableDouble(reader["WeightG"]),
                HasHumidityPack = reader["HasHumidityPack"] is not DBNull && Convert.ToInt32(reader["HasHumidityPack"]) == 1,
                FinishedAtUtc = ParseStoredUtcDateTime(NullString(reader["FinishedAtUtc"])),
                Notes = NullString(reader["Notes"]),
                CreatedAtUtc = ParseStoredUtcDateTime(NullString(reader["CreatedAtUtc"])) ?? DateTime.UtcNow,
                UpdatedAtUtc = ParseStoredUtcDateTime(NullString(reader["UpdatedAtUtc"])) ?? DateTime.UtcNow,
            });
        }

        return liste;
    }

    private static CuringReading LiesAblesung(SqliteDataReader reader) => new()
    {
        Id = Convert.ToInt32(reader["Id"]),
        JarId = Convert.ToInt32(reader["JarId"]),
        ReadAtUtc = ParseStoredUtcDateTime(NullString(reader["ReadAtUtc"])) ?? DateTime.UtcNow,
        HumidityPercent = NullableDouble(reader["HumidityPercent"]),
        BurpedMinutes = reader["BurpedMinutes"] is DBNull ? null : Convert.ToInt32(reader["BurpedMinutes"]),
        Note = NullString(reader["Note"]),
        Source = ParseEnum(NullString(reader["Source"]), CuringReadingSource.Manual),
        CreatedAtUtc = ParseStoredUtcDateTime(NullString(reader["CreatedAtUtc"])) ?? DateTime.UtcNow,
    };
}
