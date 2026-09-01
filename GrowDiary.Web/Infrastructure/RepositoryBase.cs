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

    /// <summary>
    /// Liest einen Zeitpunkt als <b>Ortszeit</b>.
    /// </summary>
    /// <remarks>
    /// Nur für Spalten, die auch Ortszeit meinen — etwa das Keimdatum, das der
    /// Nutzer am Kalender abliest. <b>Nicht</b> für Spalten, die „…Utc" heißen:
    /// dort steht ein Wert mit „Z", und dieser Parser rechnet ihn in Ortszeit
    /// um. Für die Anzeige fällt das nicht auf, beim Rechnen gegen
    /// <c>DateTime.UtcNow</c> ist das Ergebnis um den Zeitzonen-Versatz
    /// daneben. Dafür gibt es <see cref="ParseStoredUtcDateTime"/>; ein Test
    /// wacht darüber (UtcColumnReadTests).
    /// </remarks>
    protected static DateTime? ParseStoredDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    /// <summary>Liest einen Zeitpunkt als UTC — für alle „…Utc"-Spalten.</summary>
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

    /// <summary>
    /// Der Ort einer hochgeladenen Datei — oder <c>false</c>, wenn der
    /// gespeicherte Pfad nirgends hinzeigt.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (01.09.2026).</b> Diese Auflösung stand zweimal
    /// nebeneinander (<c>GrowCoreRepository</c>, <c>MeasurementRepository</c>),
    /// und <b>beide</b> rechneten gegen
    /// <c>&lt;contentRoot&gt;/wwwroot/uploads</c>. Gespeichert wird aber unter
    /// <see cref="AppPaths.UploadRootPath"/> — dem Datenpfad des Add-ons.
    /// <c>File.Exists</c> war dort immer false, und kein Foto wurde je
    /// gelöscht: die Datenbankzeile verschwand, die JPEG blieb für immer auf
    /// der Platte des Home-Assistant-Hosts liegen.</para>
    ///
    /// <para>Einmal hier, weil zwei Kopien derselben Wegrechnung genau so
    /// auseinanderlaufen — und weil <see cref="Paths"/> ohnehin allen
    /// Ablagen gehört.</para>
    ///
    /// <para><b>Der Ausbruchsschutz bleibt:</b> ein Pfad, der aus dem
    /// Upload-Verzeichnis herausführt, wird abgelehnt. Sonst könnte ein
    /// manipulierter Datenbankeintrag beliebige Dateien löschen.</para>
    ///
    /// <para><b>Verglichen wird bis zur Ordnergrenze</b> (01.09.2026, vom
    /// Prüfer gefunden). Ein blosses <c>StartsWith(uploadsRoot)</c> lässt
    /// jeden Geschwisterordner durch, dessen Name mit denselben Buchstaben
    /// anfängt: <c>uploads-alt</c>, <c>uploads.bak</c>, <c>uploads2</c>.
    /// Nachgestellt hat <c>/uploads/../uploads-alt/geheim.txt</c> beim Löschen
    /// eines Grows die fremde Datei mitgenommen — der schlichte Fall mit
    /// <c>..</c> allein fällt dagegen auf und war abgedeckt.</para>
    /// </remarks>
    protected bool TryResolveUploadPath(string relativePath, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = relativePath.Replace('\\', '/').Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }
        if (!normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var uploadsRoot = Path.GetFullPath(Paths.UploadRootPath);
        var relativ = normalized["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(uploadsRoot, relativ));

        // Mit Trennzeichen: sonst gilt jeder Nachbarordner mit gleichem
        // Namensanfang als "innerhalb".
        var grenze = uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
            ? uploadsRoot
            : uploadsRoot + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(grenze, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        physicalPath = candidatePath;
        return true;
    }
}
