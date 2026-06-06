using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class DatabaseInitializer
{
    private void DropLegacyTentSchemaIfNeeded()
    {
        using var connection = OpenConnection();

        bool hasLegacyColumn;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COUNT(*) FROM pragma_table_info('Tents')
                WHERE name = 'TemperatureEntityId';";
            hasLegacyColumn = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        if (!hasLegacyColumn)
            return;

        var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        _logger.LogWarning(
            "Legacy Tent-Schema erkannt. Tents/TentSensors werden nicht mehr gelöscht, " +
            "sondern als Legacy-Backup-Tabellen archiviert. Suffix: {Suffix}", suffix);

        RenameTableIfExists(connection, "TentSensors", $"LegacyTentSensors_{suffix}");
        RenameTableIfExists(connection, "Tents", $"LegacyTents_{suffix}");

        using var warning = connection.CreateCommand();
        warning.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaMigrationWarnings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WarningKey TEXT NOT NULL,
                Message TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );

            INSERT INTO SchemaMigrationWarnings (WarningKey, Message, CreatedAtUtc)
            VALUES ('legacy-tent-schema-archived', $message, $createdAtUtc);
        """;
        warning.Parameters.AddWithValue("$message", $"Legacy Tent-Schema wurde als LegacyTents_{suffix}/LegacyTentSensors_{suffix} archiviert. Es wurden keine Grow-Daten gelöscht.");
        warning.Parameters.AddWithValue("$createdAtUtc", DateTime.UtcNow.ToString("O"));
        warning.ExecuteNonQuery();
    }


    private static void RenameTableIfExists(SqliteConnection connection, string sourceTable, string targetTable)
    {
        if (!TableExists(connection, sourceTable) || TableExists(connection, targetTable))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE \"{sourceTable}\" RENAME TO \"{targetTable}\";";
        command.ExecuteNonQuery();
    }

}
