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

}
