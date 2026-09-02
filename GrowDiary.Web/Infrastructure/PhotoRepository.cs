using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class PhotoRepository : RepositoryBase
{
    public PhotoRepository(AppPaths paths) : base(paths)
    {
    }

    public List<PhotoAsset> GetPhotosForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE GrowId = $growId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$growId", growId);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public List<PhotoAsset> GetPhotosForMeasurement(int measurementId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE MeasurementId = $measurementId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$measurementId", measurementId);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    /// <summary>
    /// Ein Foto ablegen — und ihm dabei seine Id geben.
    /// </summary>
    /// <remarks>
    /// Die Id wird zurückgeschrieben, weil der Upload-Endpunkt das gespeicherte
    /// Objekt als Antwort ausliefert. Ohne das stand dort <c>"id": 0</c> in
    /// einer <c>201 Created</c>: eine Antwort, die auf eine Ressource verweist
    /// und dabei die falsche Nummer nennt. Wer sie weiterverwendet — etwa um
    /// dem Bild gleich ein Symptom zuzuordnen — griff ins Leere.
    /// </remarks>
    public void AddPhoto(PhotoAsset photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Photos (GrowId, MeasurementId, RelativePath, Caption, Tag, Source, IsReferenceShot, SymptomId, TakenAtUtc)
            VALUES ($growId, $measurementId, $relativePath, $caption, $tag, $source, $isReferenceShot, $symptomId, $takenAtUtc);
            SELECT last_insert_rowid();
        """;
        command.Parameters.AddWithValue("$growId", photo.GrowId);
        command.Parameters.AddWithValue("$measurementId", (object?)photo.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$relativePath", photo.RelativePath);
        command.Parameters.AddWithValue("$caption", (object?)photo.Caption ?? DBNull.Value);
        command.Parameters.AddWithValue("$tag", photo.Tag.ToString());
        command.Parameters.AddWithValue("$source", photo.Source.ToString());
        command.Parameters.AddWithValue("$isReferenceShot", photo.IsReferenceShot ? 1 : 0);
        command.Parameters.AddWithValue("$symptomId", (object?)photo.SymptomId ?? DBNull.Value);
        command.Parameters.AddWithValue("$takenAtUtc", ToStorageUtc(photo.TakenAtUtc));
        photo.Id = Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L), CultureInfo.InvariantCulture);
    }

    public List<PhotoAsset> GetRecentPhotos(int limit = 18)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos ORDER BY TakenAtUtc DESC, Id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    /// <summary>
    /// Ein Bild einem Symptom zuordnen — oder die Zuordnung wieder lösen.
    /// </summary>
    public void SetSymptom(int photoId, string? symptomId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Photos SET SymptomId = $symptomId WHERE Id = $id;";
        command.Parameters.AddWithValue("$symptomId", (object?)NormalizeOptional(symptomId) ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", photoId);
        command.ExecuteNonQuery();
    }

    /// <summary>Die eigenen Aufnahmen zu einem Symptom, neueste zuerst.</summary>
    public IReadOnlyList<PhotoAsset> GetBySymptom(string symptomId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE SymptomId = $symptomId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$symptomId", symptomId);
        using var reader = command.ExecuteReader();
        var liste = new List<PhotoAsset>();
        while (reader.Read())
        {
            liste.Add(MapPhoto(reader));
        }

        return liste;
    }

    public PhotoAsset? GetById(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapPhoto(reader) : null;
    }

    private static PhotoAsset MapPhoto(SqliteDataReader reader)
    {
        return new PhotoAsset
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            GrowId = Convert.ToInt32((long)reader["GrowId"]),
            MeasurementId = reader["MeasurementId"] is DBNull ? null : Convert.ToInt32((long)reader["MeasurementId"]),
            RelativePath = reader["RelativePath"]?.ToString() ?? string.Empty,
            Caption = NullString(reader["Caption"]),
            Tag = ParseEnum(reader["Tag"]?.ToString(), PhotoTag.Overview),
            Source = ParseEnum(reader["Source"]?.ToString(), ValueOrigin.Manual),
            IsReferenceShot = reader["IsReferenceShot"] is not DBNull && Convert.ToInt32(reader["IsReferenceShot"], CultureInfo.InvariantCulture) == 1,
            SymptomId = HasColumn(reader, "SymptomId") && reader["SymptomId"] is not DBNull ? reader["SymptomId"].ToString() : null,
            TakenAtUtc = ParseStoredUtcDateTime(reader["TakenAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }
}
