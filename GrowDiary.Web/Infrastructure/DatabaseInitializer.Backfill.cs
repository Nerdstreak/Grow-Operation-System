using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class DatabaseInitializer
{
    private void AutoAssignExistingGrowsToTents()
    {
        using var connection = OpenConnection();
        var mainTentId = GetTentId(connection, "Hauptzelt");
        if (mainTentId == 0) return;

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT Id FROM Grows WHERE TentId IS NULL;";
        using var reader = select.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
            ids.Add(Convert.ToInt32((long)reader["Id"]));
        reader.Close();

        foreach (var id in ids)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Grows SET TentId = $tentId WHERE Id = $id;";
            update.Parameters.AddWithValue("$tentId", mainTentId);
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
    }


    private static int GetTentId(SqliteConnection connection, string tentName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Tents WHERE Name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tentName);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

}
