using System.Globalization;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class HydroSetupRepository : RepositoryBase
{
    private readonly TentRepository _tentRepository;

    public HydroSetupRepository(AppPaths paths, TentRepository tentRepository) : base(paths)
    {
        _tentRepository = tentRepository;
    }

    public List<GrowSystem> GetSystems(bool includeArchived = true)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Grows g WHERE g.SystemId = s.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount
            FROM GrowSystems s
            LEFT JOIN Tents t ON t.Id = s.TentId
            WHERE ($includeArchived = 1 OR s.Status <> 'Archived')
            ORDER BY s.DisplayOrder, s.Name;
        """;
        command.Parameters.AddWithValue("$includeArchived", includeArchived ? 1 : 0);

        var list = new List<GrowSystem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapGrowSystem(reader));
        }
        return list;
    }

    public GrowSystem? GetSystem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Grows g WHERE g.SystemId = s.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount
            FROM GrowSystems s
            LEFT JOIN Tents t ON t.Id = s.TentId
            WHERE s.Id = $id LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapGrowSystem(reader) : null;
    }

    public List<GrowSystem> GetHydroSetups(bool includeArchived = false)
        => GetSystems(includeArchived);

    public GrowSystem? GetHydroSetup(int id)
        => GetSystem(id);

    public List<GrowSystem> GetHydroSetupsByTent(int tentId, bool includeArchived = false)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Grows g WHERE g.SystemId = s.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount
            FROM GrowSystems s
            LEFT JOIN Tents t ON t.Id = s.TentId
            WHERE s.TentId = $tentId AND ($includeArchived = 1 OR s.Status <> 'Archived')
            ORDER BY s.DisplayOrder, s.Name;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$includeArchived", includeArchived ? 1 : 0);

        var list = new List<GrowSystem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapGrowSystem(reader));
        }
        return list;
    }

    public GrowSystem CreateSystem(GrowSystem system)
    {
        system.CreatedAtUtc = DateTime.UtcNow;
        system.UpdatedAtUtc = system.CreatedAtUtc;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GrowSystems (
                TentId, Name, HydroStyle, PotCount, PotSizeLiters, ReservoirLiters,
                Status, LayoutType, ReservoirPosition,
                HasCirculationPump, CirculationPumpNotes, HasAirPump, AirPumpNotes, AirStoneCount,
                HasChiller, HasUvSterilizer, Notes, DisplayOrder, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $tentId, $name, $hydroStyle, $potCount, $potSizeLiters, $reservoirLiters,
                $status, $layoutType, $reservoirPosition,
                $hasCirculationPump, $circulationPumpNotes, $hasAirPump, $airPumpNotes, $airStoneCount,
                $hasChiller, $hasUvSterilizer, $notes, $displayOrder, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddGrowSystemParameters(command, system);
        system.Id = Convert.ToInt32((long)command.ExecuteScalar()!, CultureInfo.InvariantCulture);
        return GetSystem(system.Id) ?? system;
    }

    public GrowSystem CreateHydroSetup(GrowSystem system)
    {
        NormalizeHydroSetup(system);
        ValidateHydroSetup(system, requireTent: true);
        return CreateSystem(system);
    }

    public void UpdateSystem(GrowSystem system)
    {
        system.UpdatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE GrowSystems SET
                TentId = $tentId,
                Name = $name,
                HydroStyle = $hydroStyle,
                PotCount = $potCount,
                PotSizeLiters = $potSizeLiters,
                ReservoirLiters = $reservoirLiters,
                Status = $status,
                LayoutType = $layoutType,
                ReservoirPosition = $reservoirPosition,
                HasCirculationPump = $hasCirculationPump,
                CirculationPumpNotes = $circulationPumpNotes,
                HasAirPump = $hasAirPump,
                AirPumpNotes = $airPumpNotes,
                AirStoneCount = $airStoneCount,
                HasChiller = $hasChiller,
                HasUvSterilizer = $hasUvSterilizer,
                Notes = $notes,
                DisplayOrder = $displayOrder,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddGrowSystemParameters(command, system);
        command.Parameters.AddWithValue("$id", system.Id);
        command.ExecuteNonQuery();
    }

    public void UpdateHydroSetup(GrowSystem system)
    {
        NormalizeHydroSetup(system);
        ValidateHydroSetup(system, requireTent: true);
        UpdateSystem(system);
    }

    public void ArchiveHydroSetup(int id)
    {
        var system = GetSystem(id);
        if (system is null)
        {
            throw new InvalidOperationException($"HydroSetup with id {id} does not exist.");
        }

        system.Status = HydroSetupStatus.Archived;
        UpdateSystem(system);
    }

    public void DeleteSystem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GrowSystems WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private void NormalizeHydroSetup(GrowSystem system)
    {
        system.Name = system.Name.Trim();
        system.Notes = NormalizeOptional(system.Notes);
        system.CirculationPumpNotes = NormalizeOptional(system.CirculationPumpNotes);
        system.AirPumpNotes = NormalizeOptional(system.AirPumpNotes);

        if (Enum.TryParse<HydroStyle>(system.HydroStyle, out var hydroStyle) && hydroStyle == HydroStyle.DWC)
        {
            system.PotCount ??= 1;
            system.LayoutType = HydroSetupLayoutType.SingleBucket;
            system.ReservoirPosition = ReservoirPosition.None;
        }
    }

    private void ValidateHydroSetup(GrowSystem system, bool requireTent)
    {
        if (string.IsNullOrWhiteSpace(system.Name))
        {
            throw new InvalidOperationException("HydroSetup name must not be empty.");
        }

        if (!Enum.TryParse<HydroStyle>(system.HydroStyle, out var hydroStyle) || hydroStyle is not (HydroStyle.DWC or HydroStyle.RDWC))
        {
            throw new InvalidOperationException("HydroSetup supports only DWC or RDWC.");
        }

        if (requireTent && !system.TentId.HasValue)
        {
            throw new InvalidOperationException("HydroSetup tent is required.");
        }

        if (system.TentId.HasValue && _tentRepository.GetTent(system.TentId.Value) is null)
        {
            throw new InvalidOperationException($"Tent with id {system.TentId.Value} does not exist.");
        }

        if (system.PotCount.HasValue && system.PotCount.Value < 1)
        {
            throw new InvalidOperationException("HydroSetup pot count must be positive.");
        }

        if (system.PotSizeLiters.HasValue && system.PotSizeLiters.Value < 0)
        {
            throw new InvalidOperationException("HydroSetup pot size must not be negative.");
        }

        if (system.ReservoirLiters.HasValue && system.ReservoirLiters.Value < 0)
        {
            throw new InvalidOperationException("HydroSetup reservoir volume must not be negative.");
        }

        if (system.AirStoneCount.HasValue && system.AirStoneCount.Value < 0)
        {
            throw new InvalidOperationException("HydroSetup air stone count must not be negative.");
        }

        if (hydroStyle == HydroStyle.DWC && system.PotSizeLiters is not > 0 && system.ReservoirLiters is not > 0)
        {
            throw new InvalidOperationException("DWC HydroSetup needs pot or reservoir volume.");
        }

        if (!Enum.IsDefined(system.LayoutType))
        {
            throw new InvalidOperationException("HydroSetup layout type is invalid.");
        }

        if (!Enum.IsDefined(system.ReservoirPosition))
        {
            throw new InvalidOperationException("HydroSetup reservoir position is invalid.");
        }

        if (!Enum.IsDefined(system.Status))
        {
            throw new InvalidOperationException("HydroSetup status is invalid.");
        }

        if (system.DisplayOrder < 0)
        {
            throw new InvalidOperationException("HydroSetup display order must not be negative.");
        }

        if (hydroStyle == HydroStyle.RDWC)
        {
            if (system.PotCount is null or < 2)
            {
                throw new InvalidOperationException("RDWC HydroSetup needs at least two sites.");
            }

            if (system.PotSizeLiters is not > 0)
            {
                throw new InvalidOperationException("RDWC HydroSetup needs pot volume.");
            }

            if (system.LayoutType == HydroSetupLayoutType.SingleBucket)
            {
                throw new InvalidOperationException("RDWC HydroSetup needs an RDWC layout.");
            }

            if (system.ReservoirPosition == ReservoirPosition.None)
            {
                throw new InvalidOperationException("RDWC HydroSetup needs a reservoir position.");
            }
        }
    }

    private static GrowSystem MapGrowSystem(SqliteDataReader reader)
    {
        return new GrowSystem
        {
            Id = Convert.ToInt32((long)reader["Id"], CultureInfo.InvariantCulture),
            TentId = reader["TentId"] is DBNull or null ? null : Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            TentName = HasColumn(reader, "TentName") ? NullString(reader["TentName"]) : null,
            Name = reader["Name"]?.ToString() ?? string.Empty,
            HydroStyle = reader["HydroStyle"]?.ToString() ?? string.Empty,
            PotCount = reader["PotCount"] is DBNull or null ? null : Convert.ToInt32(reader["PotCount"], CultureInfo.InvariantCulture),
            PotSizeLiters = reader["PotSizeLiters"] is DBNull or null ? null : Convert.ToDouble(reader["PotSizeLiters"], CultureInfo.InvariantCulture),
            ReservoirLiters = reader["ReservoirLiters"] is DBNull or null ? null : Convert.ToDouble(reader["ReservoirLiters"], CultureInfo.InvariantCulture),
            Status = ParseEnum(NullString(reader["Status"]), HydroSetupStatus.Active),
            LayoutType = ParseEnum(NullString(reader["LayoutType"]), HydroSetupLayoutType.SingleBucket),
            ReservoirPosition = ParseEnum(NullString(reader["ReservoirPosition"]), ReservoirPosition.None),
            HasCirculationPump = Convert.ToInt32(reader["HasCirculationPump"], CultureInfo.InvariantCulture) != 0,
            CirculationPumpNotes = NullString(reader["CirculationPumpNotes"]),
            HasAirPump = Convert.ToInt32(reader["HasAirPump"], CultureInfo.InvariantCulture) != 0,
            AirPumpNotes = NullString(reader["AirPumpNotes"]),
            AirStoneCount = reader["AirStoneCount"] is DBNull or null ? null : Convert.ToInt32(reader["AirStoneCount"], CultureInfo.InvariantCulture),
            HasChiller = Convert.ToInt32(reader["HasChiller"], CultureInfo.InvariantCulture) != 0,
            HasUvSterilizer = Convert.ToInt32(reader["HasUvSterilizer"], CultureInfo.InvariantCulture) != 0,
            Notes = NullString(reader["Notes"]),
            DisplayOrder = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(NullString(reader["UpdatedAtUtc"])) ?? ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            ActiveGrowCount = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture)
        };
    }

    private static void AddGrowSystemParameters(SqliteCommand command, GrowSystem system)
    {
        command.Parameters.AddWithValue("$tentId", (object?)system.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", system.Name);
        command.Parameters.AddWithValue("$hydroStyle", system.HydroStyle);
        command.Parameters.AddWithValue("$potCount", (object?)system.PotCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$potSizeLiters", (object?)system.PotSizeLiters ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirLiters", (object?)system.ReservoirLiters ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", system.Status.ToString());
        command.Parameters.AddWithValue("$layoutType", system.LayoutType.ToString());
        command.Parameters.AddWithValue("$reservoirPosition", system.ReservoirPosition.ToString());
        command.Parameters.AddWithValue("$hasCirculationPump", system.HasCirculationPump ? 1 : 0);
        command.Parameters.AddWithValue("$circulationPumpNotes", (object?)system.CirculationPumpNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$hasAirPump", system.HasAirPump ? 1 : 0);
        command.Parameters.AddWithValue("$airPumpNotes", (object?)system.AirPumpNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$airStoneCount", (object?)system.AirStoneCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$hasChiller", system.HasChiller ? 1 : 0);
        command.Parameters.AddWithValue("$hasUvSterilizer", system.HasUvSterilizer ? 1 : 0);
        command.Parameters.AddWithValue("$notes", (object?)system.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$displayOrder", system.DisplayOrder);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(system.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(system.UpdatedAtUtc));
    }
}
