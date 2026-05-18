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

    public void AddPhoto(PhotoAsset photo)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Photos (GrowId, MeasurementId, RelativePath, Caption, Tag, Source, IsReferenceShot, TakenAtUtc)
            VALUES ($growId, $measurementId, $relativePath, $caption, $tag, $source, $isReferenceShot, $takenAtUtc);
        """;
        command.Parameters.AddWithValue("$growId", photo.GrowId);
        command.Parameters.AddWithValue("$measurementId", (object?)photo.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$relativePath", photo.RelativePath);
        command.Parameters.AddWithValue("$caption", (object?)photo.Caption ?? DBNull.Value);
        command.Parameters.AddWithValue("$tag", photo.Tag.ToString());
        command.Parameters.AddWithValue("$source", photo.Source.ToString());
        command.Parameters.AddWithValue("$isReferenceShot", photo.IsReferenceShot ? 1 : 0);
        command.Parameters.AddWithValue("$takenAtUtc", ToStorageUtc(photo.TakenAtUtc));
        command.ExecuteNonQuery();
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
            TakenAtUtc = ParseStoredDateTime(reader["TakenAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }
}
