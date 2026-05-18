using System.Globalization;
using Microsoft.Data.Sqlite;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Infrastructure;

public abstract class RepositoryBase
{
    protected readonly AppPaths Paths;

    protected RepositoryBase(AppPaths paths)
    {
        Paths = paths;
    }

    protected SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = Paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    protected static string? NullString(object? value)
        => value is DBNull or null ? null : value.ToString();

    protected static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    protected static double? NullableDouble(object? value)
        => value is DBNull or null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    protected static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(raw, out var parsed) ? parsed : fallback;

    protected static bool HasColumn(SqliteDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected static DateTime? ParseStoredDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    protected static DateTime? ParseStoredUtcDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    protected static DateTime? ParseStoredDateTimeIfColumn(SqliteDataReader reader, string columnName)
    {
        if (!HasColumn(reader, columnName) || reader[columnName] is DBNull)
        {
            return null;
        }

        var text = reader[columnName]?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result)
            ? result
            : null;
    }

    protected static DateTime? ParseStoredDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result) ? result.Date : null;

    protected static string ToStorage(DateTime value)
        => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    protected static string ToStorageUtc(DateTime value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    protected static void AddNullable(SqliteCommand command, string name, double? value)
        => command.Parameters.AddWithValue(name, value.HasValue ? value.Value : DBNull.Value);
}
