using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class HardwareRepository
{
    private bool RowExists(string tableName, int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture) > 0;
    }


    private (bool exists, int? tentId) GetHydroSetupTentId(int hydroSetupId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TentId FROM GrowSystems WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", hydroSetupId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return (false, null);
        }

        return (true, reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Räumt die Erinnerungen weg, die zu gelöschten Vorgängen gehörten.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (02.09.2026).</b> Eine geplante Kalibrierung oder
    /// Wartung legt eine Aufgabe an (<c>GrowTaskId ??= TryCreate…</c>). Beim
    /// Löschen verschwand nur die Zeile: die Erinnerung blieb in der
    /// Aufgabenliste stehen, hängte an nichts mehr und war über die Oberfläche
    /// nicht mehr erreichbar. Wer sich vertippt hatte, wurde sie nie los.</para>
    ///
    /// <para>Am schwersten wog das beim Löschen eines <b>Geräts</b>: dabei gehen
    /// alle seine Vorgänge auf einmal — und mit ihnen blieben alle ihre
    /// Erinnerungen zurück.</para>
    ///
    /// <para><b>Nur OFFENE.</b> Was der Nutzer abgehakt hat, gehört in seine
    /// Historie und verschwindet nicht, weil darunter aufgeräumt wird.</para>
    /// </remarks>
    /// <param name="wo">
    /// Die WHERE-Bedingung auf der Vorgangstabelle, mit <c>$id</c> als Parameter
    /// — dieselbe, die gleich löscht.
    /// </param>
    private static void LoescheOffeneErinnerungen(
        SqliteConnection connection, SqliteTransaction? transaction, string tabelle, string wo, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            DELETE FROM GrowTasks
            WHERE Status = 'Open'
              AND Id IN (SELECT GrowTaskId FROM {tabelle} WHERE {wo} AND GrowTaskId IS NOT NULL);
        """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
}
