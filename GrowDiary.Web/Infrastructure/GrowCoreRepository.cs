using System.Globalization;
using System.Text.Json;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class GrowCoreRepository : RepositoryBase
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public GrowCoreRepository(AppPaths paths) : base(paths)
    {
    }

    public DashboardStats GetDashboardStats()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*) AS TotalGrows,
                COALESCE(SUM(CASE WHEN Status IN ('Planning','Running') THEN 1 ELSE 0 END), 0) AS ActiveGrows,
                COALESCE(SUM(CASE WHEN Status IN ('Completed','Aborted') THEN 1 ELSE 0 END), 0) AS ArchivedGrows,
                COALESCE(SUM(CASE WHEN Status = 'Aborted' THEN 1 ELSE 0 END), 0) AS AbortedGrows,
                (SELECT COUNT(*) FROM Measurements) AS Measurements,
                (SELECT COUNT(*) FROM Photos) AS Photos
            FROM Grows;
        """;
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new DashboardStats();
        return new DashboardStats
        {
            TotalGrows = Convert.ToInt32(reader["TotalGrows"]),
            ActiveGrows = Convert.ToInt32(reader["ActiveGrows"]),
            ArchivedGrows = Convert.ToInt32(reader["ArchivedGrows"]),
            AbortedGrows = Convert.ToInt32(reader["AbortedGrows"]),
            Measurements = Convert.ToInt32(reader["Measurements"]),
            Photos = Convert.ToInt32(reader["Photos"])
        };
    }

    public List<GrowRun> GetActiveGrows(string? search = null)
        => GetGrows("WHERE g.Status IN ('Planning','Running')" + SearchClause(search), search);

    public List<GrowRun> GetArchivedGrows(string? search = null)
        => GetGrows("WHERE g.Status IN ('Completed','Aborted')" + SearchClause(search), search);

    public List<GrowRun> GetActiveGrowsForTent(int tentId)
        => GetGrows("WHERE g.TentId = $tentId AND g.Status IN ('Planning','Running')", null, tentId);

    public List<GrowRun> GetArchivedGrowsForTent(int tentId)
        => GetGrows("WHERE g.TentId = $tentId AND g.Status IN ('Completed','Aborted')", null, tentId);

    public List<GrowRun> GetAllGrows()
        => GetGrows(string.Empty, null);

    public GrowRun? GetGrow(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Measurements m WHERE m.GrowId = g.Id) AS MeasurementCount,
                   (SELECT RelativePath FROM Photos p WHERE p.GrowId = g.Id ORDER BY p.TakenAtUtc DESC LIMIT 1) AS LatestPhotoPath
            FROM Grows g
            LEFT JOIN Tents t ON t.Id = g.TentId
            WHERE g.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var grow = MapGrow(reader);
        grow.LatestMeasurement = GetLatestMeasurement(id);
        return grow;
    }

    public Tent? GetTentForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status IN ('Planning','Active')) AS ActiveSetupCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status = 'Archived') AS ArchivedSetupCount
            FROM Tents t
            INNER JOIN Grows g ON g.TentId = t.Id
            WHERE g.Id = $growId
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapTent(reader) : null;
    }

    public int CreateGrow(GrowRun grow)
    {
        grow.CreatedAtUtc = DateTime.UtcNow;
        grow.UpdatedAtUtc = DateTime.UtcNow;
        CaptureGrowSnapshots(grow);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Grows
            (
                TentId, SystemId, SetupId, Name, Strain, Breeder, Status, MediumType, FeedingStyle, HydroStyle, MediumDetail,
                Environment, Light, ContainerSize, ReservoirSize, IrrigationStyle, IrrigationType, WaterSource,
                SeedType, StartMaterial, GerminationMethod, CloneSource, CloneIsRooted,
                BreederFlowerWeeksMin, BreederFlowerWeeksMax, PlantCount, PhenoNumber,
                PropagationMedium, HasChiller, EntryPoint, DaysAlreadyInPhase,
                AutoflowerDaysSinceGermination, FlipDate, GerminatedAt, RootedAt,
                Nutrients, Notes, StartDate, EndDate, CreatedAtUtc, UpdatedAtUtc,
                TentSnapshotJson, HydroSetupSnapshotJson, SnapshotsCapturedAtUtc
            )
            VALUES
            (
                $tentId, $systemId, $setupId, $name, $strain, $breeder, $status, $mediumType, $feedingStyle, $hydroStyle, $mediumDetail,
                $environment, $light, $containerSize, $reservoirSize, $irrigationStyle, $irrigationType, $waterSource,
                $seedType, $startMaterial, $germinationMethod, $cloneSource, $cloneIsRooted,
                $breederFlowerWeeksMin, $breederFlowerWeeksMax, $plantCount, $phenoNumber,
                $propagationMedium, $hasChiller, $entryPoint, $daysAlreadyInPhase,
                $autoflowerDaysSinceGermination, $flipDate, $germinatedAt, $rootedAt,
                $nutrients, $notes, $startDate, $endDate, $createdAtUtc, $updatedAtUtc,
                $tentSnapshotJson, $hydroSetupSnapshotJson, $snapshotsCapturedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddGrowParameters(command, grow);
        AddGrowSnapshotParameters(command, grow);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    public void UpdateGrow(GrowRun grow)
    {
        grow.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Grows
            SET
                TentId = $tentId,
                SystemId = $systemId,
                SetupId = $setupId,
                Name = $name,
                Strain = $strain,
                Breeder = $breeder,
                Status = $status,
                MediumType = $mediumType,
                FeedingStyle = $feedingStyle,
                HydroStyle = $hydroStyle,
                MediumDetail = $mediumDetail,
                Environment = $environment,
                Light = $light,
                ContainerSize = $containerSize,
                ReservoirSize = $reservoirSize,
                IrrigationStyle = $irrigationStyle,
                IrrigationType = $irrigationType,
                WaterSource = $waterSource,
                SeedType = $seedType,
                StartMaterial = $startMaterial,
                GerminationMethod = $germinationMethod,
                CloneSource = $cloneSource,
                CloneIsRooted = $cloneIsRooted,
                BreederFlowerWeeksMin = $breederFlowerWeeksMin,
                BreederFlowerWeeksMax = $breederFlowerWeeksMax,
                PlantCount = $plantCount,
                PhenoNumber = $phenoNumber,
                PropagationMedium = $propagationMedium,
                HasChiller = $hasChiller,
                EntryPoint = $entryPoint,
                DaysAlreadyInPhase = $daysAlreadyInPhase,
                AutoflowerDaysSinceGermination = $autoflowerDaysSinceGermination,
                FlipDate = $flipDate,
                GerminatedAt = $germinatedAt,
                RootedAt = $rootedAt,
                Nutrients = $nutrients,
                Notes = $notes,
                StartDate = $startDate,
                EndDate = $endDate,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddGrowParameters(command, grow);
        command.Parameters.AddWithValue("$id", grow.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteGrow(int id)
    {
        using var connection = OpenConnection();
        using var photoCommand = connection.CreateCommand();
        photoCommand.CommandText = "SELECT RelativePath FROM Photos WHERE GrowId = $id;";
        photoCommand.Parameters.AddWithValue("$id", id);

        var filesToDelete = new List<string>();
        using (var reader = photoCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var relativePath = reader["RelativePath"]?.ToString();
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    filesToDelete.Add(relativePath);
                }
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Grows WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();

        foreach (var relativePath in filesToDelete)
        {
            if (TryResolveUploadPath(relativePath, out var physicalPath) && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
    }

    private List<GrowRun> GetGrows(string whereClause, string? search, int? tentId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT g.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Measurements m WHERE m.GrowId = g.Id) AS MeasurementCount,
                   (SELECT RelativePath FROM Photos p WHERE p.GrowId = g.Id ORDER BY p.TakenAtUtc DESC LIMIT 1) AS LatestPhotoPath
            FROM Grows g
            LEFT JOIN Tents t ON t.Id = g.TentId
            {whereClause}
            ORDER BY g.StartDate DESC, g.Id DESC;
        """;

        if (!string.IsNullOrWhiteSpace(search))
        {
            command.Parameters.AddWithValue("$search", $"%{search.Trim()}%");
        }
        if (tentId.HasValue)
        {
            command.Parameters.AddWithValue("$tentId", tentId.Value);
        }

        var items = new List<GrowRun>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                items.Add(MapGrow(reader));
            }
        }

        if (items.Count > 0)
        {
            var latestMeasurements = GetLatestMeasurementsBatch(connection, items.Select(g => g.Id));
            foreach (var grow in items)
            {
                grow.LatestMeasurement = latestMeasurements.GetValueOrDefault(grow.Id);
            }
        }

        return items;
    }

    private Measurement? GetLatestMeasurement(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Measurements WHERE GrowId = $growId ORDER BY TakenAt DESC, Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$growId", growId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MeasurementRepository.MapMeasurement(reader) : null;
    }

    private static Dictionary<int, Measurement> GetLatestMeasurementsBatch(SqliteConnection connection, IEnumerable<int> growIds)
    {
        var ids = growIds.ToList();
        if (ids.Count == 0) return [];

        var placeholders = string.Join(", ", ids.Select((_, i) => $"$p{i}"));
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH ranked AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY GrowId ORDER BY TakenAt DESC, Id DESC) AS rn
                FROM Measurements
                WHERE GrowId IN ({placeholders})
            )
            SELECT * FROM ranked WHERE rn = 1;
        """;
        for (var i = 0; i < ids.Count; i++)
        {
            command.Parameters.AddWithValue($"$p{i}", ids[i]);
        }

        var result = new Dictionary<int, Measurement>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var m = MeasurementRepository.MapMeasurement(reader);
            result[m.GrowId] = m;
        }
        return result;
    }

    private static string SearchClause(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return string.Empty;
        }
        return " AND (g.Name LIKE $search OR g.Strain LIKE $search OR g.Breeder LIKE $search OR g.Nutrients LIKE $search OR t.Name LIKE $search)";
    }

    private void CaptureGrowSnapshots(GrowRun grow)
    {
        var capturedAtUtc = DateTime.UtcNow;
        var capturedAny = !string.IsNullOrWhiteSpace(grow.TentSnapshotJson)
                          || !string.IsNullOrWhiteSpace(grow.HydroSetupSnapshotJson);

        if (grow.TentId.HasValue && string.IsNullOrWhiteSpace(grow.TentSnapshotJson))
        {
            var tent = GetTentForSnapshot(grow.TentId.Value);
            if (tent is not null)
            {
                grow.TentSnapshotJson = JsonSerializer.Serialize(ToGrowTentSnapshot(tent), SnapshotJsonOptions);
                capturedAny = true;
            }
        }

        if (grow.SystemId.HasValue && string.IsNullOrWhiteSpace(grow.HydroSetupSnapshotJson))
        {
            var hydroSetup = GetHydroSetupForSnapshot(grow.SystemId.Value);
            if (hydroSetup is not null)
            {
                grow.HydroSetupSnapshotJson = JsonSerializer.Serialize(ToGrowHydroSetupSnapshot(hydroSetup), SnapshotJsonOptions);
                capturedAny = true;
            }
        }

        grow.SnapshotsCapturedAtUtc = capturedAny
            ? grow.SnapshotsCapturedAtUtc ?? capturedAtUtc
            : null;
    }

    private Tent? GetTentForSnapshot(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status IN ('Planning','Active')) AS ActiveSetupCount,
                   (SELECT COUNT(*) FROM Setups s WHERE s.TentId = t.Id AND s.Status = 'Archived') AS ArchivedSetupCount
            FROM Tents t
            WHERE t.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var tent = MapTent(reader);
        tent.Sensors = GetTentSensors(id);
        return tent;
    }

    private List<TentSensor> GetTentSensors(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM TentSensors WHERE TentId = $tentId ORDER BY Id;";
        command.Parameters.AddWithValue("$tentId", tentId);
        var list = new List<TentSensor>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapTentSensor(reader));
        }
        return list;
    }

    private GrowSystem? GetHydroSetupForSnapshot(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Grows g WHERE g.SystemId = s.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount
            FROM GrowSystems s
            LEFT JOIN Tents t ON t.Id = s.TentId
            WHERE s.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapGrowSystem(reader) : null;
    }

    private static GrowTentSnapshot ToGrowTentSnapshot(Tent tent)
        => new(
            Id: tent.Id,
            Name: tent.Name,
            Kind: tent.Kind,
            TentType: tent.TentType,
            Status: tent.Status,
            Notes: tent.Notes,
            DisplayOrder: tent.DisplayOrder,
            AccentColor: tent.AccentColor,
            WidthCm: tent.WidthCm,
            DepthCm: tent.DepthCm,
            TentHeightCm: tent.TentHeightCm,
            LightType: tent.LightType,
            LightWatt: tent.LightWatt,
            LightController: tent.LightController,
            LightControllerEntityId: tent.LightControllerEntityId,
            ExhaustFanCount: tent.ExhaustFanCount,
            ExhaustM3h: tent.ExhaustM3h,
            CirculationFanCount: tent.CirculationFanCount,
            HvacController: tent.HvacController,
            HvacControllerEntityId: tent.HvacControllerEntityId,
            Co2Available: tent.Co2Available,
            CameraEntityId: tent.CameraEntityId,
            Sensors: tent.Sensors.Select(sensor => new GrowTentSensorSnapshot(
                Id: sensor.Id,
                MetricType: sensor.MetricType,
                HaEntityId: sensor.HaEntityId,
                DisplayLabel: sensor.DisplayLabel,
                IsActive: sensor.IsActive)).ToList());

    private static GrowHydroSetupSnapshot ToGrowHydroSetupSnapshot(GrowSystem hydroSetup)
        => new(
            Id: hydroSetup.Id,
            TentId: hydroSetup.TentId,
            TentName: hydroSetup.TentName,
            Name: hydroSetup.Name,
            HydroStyle: hydroSetup.HydroStyle,
            PotCount: hydroSetup.PotCount,
            PotSizeLiters: hydroSetup.PotSizeLiters,
            ReservoirLiters: hydroSetup.ReservoirLiters,
            TotalVolumeLiters: CalculateHydroSetupTotalVolumeLiters(hydroSetup.PotCount, hydroSetup.PotSizeLiters, hydroSetup.ReservoirLiters),
            Status: hydroSetup.Status,
            LayoutType: hydroSetup.LayoutType,
            ReservoirPosition: hydroSetup.ReservoirPosition,
            HasCirculationPump: hydroSetup.HasCirculationPump,
            CirculationPumpNotes: hydroSetup.CirculationPumpNotes,
            HasAirPump: hydroSetup.HasAirPump,
            AirPumpNotes: hydroSetup.AirPumpNotes,
            AirStoneCount: hydroSetup.AirStoneCount,
            HasChiller: hydroSetup.HasChiller,
            HasUvSterilizer: hydroSetup.HasUvSterilizer,
            Notes: hydroSetup.Notes,
            DisplayOrder: hydroSetup.DisplayOrder,
            CreatedAtUtc: hydroSetup.CreatedAtUtc,
            UpdatedAtUtc: hydroSetup.UpdatedAtUtc);

    private static double? CalculateHydroSetupTotalVolumeLiters(int? potCount, double? potSizeLiters, double? reservoirLiters)
    {
        var total = (potCount ?? 0) * (potSizeLiters ?? 0) + (reservoirLiters ?? 0);
        return total > 0 ? Math.Round(total, 2) : null;
    }

    private static GrowRun MapGrow(SqliteDataReader reader)
    {
        return new GrowRun
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            TentId = reader["TentId"] is DBNull ? null : Convert.ToInt32((long)reader["TentId"]),
            SystemId = reader["SystemId"] is DBNull or null ? null : Convert.ToInt32((long)reader["SystemId"]),
            SetupId = reader["SetupId"] is DBNull or null ? null : Convert.ToInt32((long)reader["SetupId"]),
            TentName = NullString(reader["TentName"]),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Strain = NullString(reader["Strain"]),
            Breeder = NullString(reader["Breeder"]),
            Status = ParseEnum(reader["Status"]?.ToString(), GrowStatus.Planning),
            MediumType = ParseEnum(reader["MediumType"]?.ToString(), MediumType.Hydro),
            FeedingStyle = ParseEnum(reader["FeedingStyle"]?.ToString(), FeedingStyle.None),
            HydroStyle = ParseEnum(reader["HydroStyle"]?.ToString(), HydroStyle.None),
            MediumDetail = NullString(reader["MediumDetail"]),
            Environment = ParseEnum(reader["Environment"]?.ToString(), GrowEnvironment.Indoor),
            Light = NullString(reader["Light"]),
            ContainerSize = NullString(reader["ContainerSize"]),
            ReservoirSize = NullString(reader["ReservoirSize"]),
            IrrigationStyle = NullString(reader["IrrigationStyle"]),
            IrrigationType = ParseEnum(reader["IrrigationType"]?.ToString(), IrrigationType.ActiveHydro),
            WaterSource = ParseEnum(reader["WaterSource"]?.ToString(), WaterSource.Tap),
            SeedType = ParseEnum(reader["SeedType"]?.ToString(), SeedType.Feminized),
            StartMaterial = ParseEnum(reader["StartMaterial"]?.ToString(), StartMaterial.Seed),
            GerminationMethod = reader["GerminationMethod"] is DBNull or null ? null : Enum.TryParse<GerminationMethod>(reader["GerminationMethod"]?.ToString(), out var gm) ? gm : null,
            CloneSource = NullString(reader["CloneSource"]),
            CloneIsRooted = reader["CloneIsRooted"] is long cr && cr == 1,
            BreederFlowerWeeksMin = reader["BreederFlowerWeeksMin"] is DBNull or null ? null : Convert.ToInt32(reader["BreederFlowerWeeksMin"], CultureInfo.InvariantCulture),
            BreederFlowerWeeksMax = reader["BreederFlowerWeeksMax"] is DBNull or null ? null : Convert.ToInt32(reader["BreederFlowerWeeksMax"], CultureInfo.InvariantCulture),
            PlantCount = reader["PlantCount"] is DBNull or null ? null : Convert.ToInt32(reader["PlantCount"], CultureInfo.InvariantCulture),
            PhenoNumber = reader["PhenoNumber"] is DBNull or null ? null : Convert.ToInt32(reader["PhenoNumber"], CultureInfo.InvariantCulture),
            PropagationMedium = reader["PropagationMedium"] is DBNull or null ? null : Enum.TryParse<PropagationMedium>(reader["PropagationMedium"]?.ToString(), out var pm) ? pm : null,
            HasChiller = reader["HasChiller"] is long hc && hc == 1,
            EntryPoint = ParseEnum(reader["EntryPoint"]?.ToString(), GrowEntryPoint.Germination),
            DaysAlreadyInPhase = reader["DaysAlreadyInPhase"] is DBNull or null ? null : Convert.ToInt32(reader["DaysAlreadyInPhase"], CultureInfo.InvariantCulture),
            AutoflowerDaysSinceGermination = reader["AutoflowerDaysSinceGermination"] is DBNull or null ? null : Convert.ToInt32(reader["AutoflowerDaysSinceGermination"], CultureInfo.InvariantCulture),
            FlipDate = ParseStoredDate(reader["FlipDate"]?.ToString()),
            GerminatedAt = reader["GerminatedAt"] is DBNull or null ? null : (DateTime.TryParse(reader["GerminatedAt"]?.ToString(), out var ga) ? ga : (DateTime?)null),
            RootedAt = reader["RootedAt"] is DBNull or null ? null : (DateTime.TryParse(reader["RootedAt"]?.ToString(), out var ra) ? ra : (DateTime?)null),
            Nutrients = NullString(reader["Nutrients"]),
            Notes = NullString(reader["Notes"]),
            StartDate = ParseStoredDate(reader["StartDate"]?.ToString()) ?? DateTime.Today,
            EndDate = ParseStoredDate(reader["EndDate"]?.ToString()),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            TentSnapshotJson = HasColumn(reader, "TentSnapshotJson") ? NullString(reader["TentSnapshotJson"]) : null,
            HydroSetupSnapshotJson = HasColumn(reader, "HydroSetupSnapshotJson") ? NullString(reader["HydroSetupSnapshotJson"]) : null,
            SnapshotsCapturedAtUtc = ParseStoredDateTimeIfColumn(reader, "SnapshotsCapturedAtUtc"),
            MeasurementCount = reader["MeasurementCount"] is DBNull ? 0 : Convert.ToInt32(reader["MeasurementCount"], CultureInfo.InvariantCulture),
            LatestPhotoPath = NullString(reader["LatestPhotoPath"])
        };
    }

    private static Tent MapTent(SqliteDataReader reader)
    {
        return new Tent
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Kind = reader["Kind"]?.ToString() ?? "Grow Tent",
            TentType = ParseEnum(NullString(reader["TentType"]), TentType.MultiPurpose),
            Status = ParseEnum(NullString(reader["Status"]), TentStatus.Active),
            Notes = NullString(reader["Notes"]),
            DisplayOrder = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
            AccentColor = reader["AccentColor"]?.ToString() ?? "#69b578",
            WidthCm = reader["WidthCm"] is DBNull or null ? null : Convert.ToInt32(reader["WidthCm"], CultureInfo.InvariantCulture),
            DepthCm = reader["DepthCm"] is DBNull or null ? null : Convert.ToInt32(reader["DepthCm"], CultureInfo.InvariantCulture),
            TentHeightCm = reader["TentHeightCm"] is DBNull or null ? null : Convert.ToInt32(reader["TentHeightCm"], CultureInfo.InvariantCulture),
            LightType = NullString(reader["LightType"]),
            LightWatt = reader["LightWatt"] is DBNull or null ? null : Convert.ToInt32(reader["LightWatt"], CultureInfo.InvariantCulture),
            LightController = Enum.TryParse<LightControllerType>(NullString(reader["LightController"]), out var lc) ? lc : (LightControllerType?)null,
            LightControllerEntityId = NullString(reader["LightControllerEntityId"]),
            ExhaustFanCount = reader["ExhaustFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustFanCount"], CultureInfo.InvariantCulture),
            ExhaustM3h = reader["ExhaustM3h"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustM3h"], CultureInfo.InvariantCulture),
            CirculationFanCount = reader["CirculationFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["CirculationFanCount"], CultureInfo.InvariantCulture),
            HvacController = Enum.TryParse<HvacControllerType>(NullString(reader["HvacController"]), out var hc) ? hc : (HvacControllerType?)null,
            HvacControllerEntityId = NullString(reader["HvacControllerEntityId"]),
            Co2Available = reader["Co2Available"] is not DBNull and not null && Convert.ToInt32(reader["Co2Available"], CultureInfo.InvariantCulture) == 1,
            CameraEntityId = NullString(reader["CameraEntityId"]),
            ActiveGrowCount = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture),
            ArchivedGrowCount = reader["ArchivedGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedGrowCount"], CultureInfo.InvariantCulture),
            ActiveSetupCount = reader["ActiveSetupCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveSetupCount"], CultureInfo.InvariantCulture),
            ArchivedSetupCount = reader["ArchivedSetupCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedSetupCount"], CultureInfo.InvariantCulture)
        };
    }

    private static TentSensor MapTentSensor(SqliteDataReader reader)
    {
        return new TentSensor
        {
            Id = Convert.ToInt32((long)reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32((long)reader["TentId"], CultureInfo.InvariantCulture),
            MetricType = ParseEnum(reader["MetricType"]?.ToString(), SensorMetricType.AirTemperature),
            HaEntityId = reader["HaEntityId"]?.ToString() ?? string.Empty,
            DisplayLabel = NullString(reader["DisplayLabel"]),
            IsActive = reader["IsActive"] is not DBNull and not null && Convert.ToInt32(reader["IsActive"], CultureInfo.InvariantCulture) == 1,
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
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

    private static void AddGrowParameters(SqliteCommand command, GrowRun grow)
    {
        command.Parameters.AddWithValue("$tentId", (object?)grow.TentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$systemId", (object?)grow.SystemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$setupId", (object?)grow.SetupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", grow.Name);
        command.Parameters.AddWithValue("$strain", (object?)grow.Strain ?? DBNull.Value);
        command.Parameters.AddWithValue("$breeder", (object?)grow.Breeder ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", grow.Status.ToString());
        command.Parameters.AddWithValue("$mediumType", grow.MediumType.ToString());
        command.Parameters.AddWithValue("$feedingStyle", grow.FeedingStyle.ToString());
        command.Parameters.AddWithValue("$hydroStyle", grow.HydroStyle.ToString());
        command.Parameters.AddWithValue("$mediumDetail", (object?)grow.MediumDetail ?? DBNull.Value);
        command.Parameters.AddWithValue("$environment", grow.Environment.ToString());
        command.Parameters.AddWithValue("$light", (object?)grow.Light ?? DBNull.Value);
        command.Parameters.AddWithValue("$containerSize", (object?)grow.ContainerSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirSize", (object?)grow.ReservoirSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$irrigationStyle", (object?)grow.IrrigationStyle ?? DBNull.Value);
        command.Parameters.AddWithValue("$irrigationType", grow.IrrigationType.ToString());
        command.Parameters.AddWithValue("$waterSource", grow.WaterSource.ToString());
        command.Parameters.AddWithValue("$seedType", grow.SeedType.ToString());
        command.Parameters.AddWithValue("$startMaterial", grow.StartMaterial.ToString());
        command.Parameters.AddWithValue("$germinationMethod", (object?)grow.GerminationMethod?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$cloneSource", (object?)grow.CloneSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$cloneIsRooted", grow.CloneIsRooted ? 1 : 0);
        command.Parameters.AddWithValue("$breederFlowerWeeksMin", (object?)grow.BreederFlowerWeeksMin ?? DBNull.Value);
        command.Parameters.AddWithValue("$breederFlowerWeeksMax", (object?)grow.BreederFlowerWeeksMax ?? DBNull.Value);
        command.Parameters.AddWithValue("$plantCount", (object?)grow.PlantCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$phenoNumber", (object?)grow.PhenoNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$propagationMedium", (object?)grow.PropagationMedium?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$hasChiller", grow.HasChiller ? 1 : 0);
        command.Parameters.AddWithValue("$entryPoint", grow.EntryPoint.ToString());
        command.Parameters.AddWithValue("$daysAlreadyInPhase", (object?)grow.DaysAlreadyInPhase ?? DBNull.Value);
        command.Parameters.AddWithValue("$autoflowerDaysSinceGermination", (object?)grow.AutoflowerDaysSinceGermination ?? DBNull.Value);
        command.Parameters.AddWithValue("$flipDate", grow.FlipDate.HasValue ? ToStorage(grow.FlipDate.Value.Date) : DBNull.Value);
        command.Parameters.AddWithValue("$germinatedAt", grow.GerminatedAt.HasValue ? ToStorageUtc(grow.GerminatedAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$rootedAt", grow.RootedAt.HasValue ? ToStorageUtc(grow.RootedAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nutrients", (object?)grow.Nutrients ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)grow.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$startDate", ToStorage(grow.StartDate.Date));
        command.Parameters.AddWithValue("$endDate", grow.EndDate.HasValue ? ToStorage(grow.EndDate.Value.Date) : DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(grow.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(grow.UpdatedAtUtc));
    }

    private static void AddGrowSnapshotParameters(SqliteCommand command, GrowRun grow)
    {
        command.Parameters.AddWithValue("$tentSnapshotJson", (object?)grow.TentSnapshotJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$hydroSetupSnapshotJson", (object?)grow.HydroSetupSnapshotJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$snapshotsCapturedAtUtc", grow.SnapshotsCapturedAtUtc.HasValue ? ToStorageUtc(grow.SnapshotsCapturedAtUtc.Value) : DBNull.Value);
    }

    private bool TryResolveUploadPath(string relativePath, out string physicalPath)
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

        var uploadsRoot = Path.GetFullPath(Path.Combine(Paths.ContentRootPath, "wwwroot", "uploads"));
        var candidatePath = Path.GetFullPath(Path.Combine(Paths.ContentRootPath, "wwwroot", normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!candidatePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        physicalPath = candidatePath;
        return true;
    }
}
