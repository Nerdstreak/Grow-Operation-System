using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class TemplateRepository
{
    private readonly AppPaths _paths;

    public TemplateRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public List<GrowTemplate> GetAll()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM GrowTemplates ORDER BY Name;";
        var items = new List<GrowTemplate>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new GrowTemplate
            {
                Id = Convert.ToInt32((long)reader["Id"]),
                Name = reader["Name"]?.ToString() ?? string.Empty,
                Description = reader["Description"] is DBNull ? null : reader["Description"]?.ToString(),
                MediumType = Enum.TryParse<MediumType>(reader["MediumType"]?.ToString(), out var medium) ? medium : MediumType.Hydro,
                FeedingStyle = Enum.TryParse<FeedingStyle>(reader["FeedingStyle"]?.ToString(), out var feeding) ? feeding : FeedingStyle.None,
                HydroStyle = Enum.TryParse<HydroStyle>(reader["HydroStyle"]?.ToString(), out var hydro) ? hydro : HydroStyle.None,
                MediumDetail = reader["MediumDetail"] is DBNull ? null : reader["MediumDetail"]?.ToString(),
                Environment = Enum.TryParse<GrowEnvironment>(reader["Environment"]?.ToString(), out var env) ? env : GrowEnvironment.Indoor,
                SuggestedTentKind = reader["SuggestedTentKind"] is DBNull ? null : reader["SuggestedTentKind"]?.ToString(),
                Light = reader["Light"] is DBNull ? null : reader["Light"]?.ToString(),
                ContainerSize = reader["ContainerSize"] is DBNull ? null : reader["ContainerSize"]?.ToString(),
                ReservoirSize = reader["ReservoirSize"] is DBNull ? null : reader["ReservoirSize"]?.ToString(),
                IrrigationStyle = reader["IrrigationStyle"] is DBNull ? null : reader["IrrigationStyle"]?.ToString(),
                Nutrients = reader["Nutrients"] is DBNull ? null : reader["Nutrients"]?.ToString(),
                Notes = reader["Notes"] is DBNull ? null : reader["Notes"]?.ToString(),
                AccentColor = reader["AccentColor"]?.ToString() ?? "#79c97f"
            });
        }
        return items;
    }

    public GrowTemplate? Get(int id) => GetAll().FirstOrDefault(x => x.Id == id);

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }
}
