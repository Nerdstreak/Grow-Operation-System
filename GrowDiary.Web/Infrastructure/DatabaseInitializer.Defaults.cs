using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;

public sealed partial class DatabaseInitializer
{
    private void SeedDefaults()
    {
        using var connection = OpenConnection();
        // Keine Standard-Zelte seeden: Grow OS startet bewusst leer.
        // Nutzer legen ihre Zelte unter /zelte selbst an.

        // Entferne nicht-Hydro-Templates – App ist RDWC/DWC-only
        using var deleteNonHydro = connection.CreateCommand();
        deleteNonHydro.CommandText = "DELETE FROM GrowTemplates WHERE MediumType != 'Hydro';";
        deleteNonHydro.ExecuteNonQuery();

        using var templateCount = connection.CreateCommand();
        templateCount.CommandText = "SELECT COUNT(*) FROM GrowTemplates;";
        var growTemplateCount = Convert.ToInt32((long)(templateCount.ExecuteScalar() ?? 0L));
        if (growTemplateCount == 0)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO GrowTemplates (Name, Description, MediumType, FeedingStyle, HydroStyle, MediumDetail, Environment, SuggestedTentKind, Light, ContainerSize, ReservoirSize, IrrigationStyle, Nutrients, Notes, AccentColor)
                VALUES
                    ('RDWC Standard', 'Für rezirkulierende Hydro-Runs mit Reservoir-Tracking, Addback und Kamera-Überblick.', 'Hydro', 'None', 'RDWC', 'RDWC', 'Indoor', 'Blüte / Hauptlauf', 'LED Vollspektrum', 'Netztopf / RDWC Site', '60 L Reservoir', null, 'Athena / Hydroponic Research', 'Ideal für dein Hauptzelt mit Home-Assistant-Monitoring.', '#7dd3a6');
            """;
            insert.ExecuteNonQuery();
        }
    }

}
