using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class GrowRepository
{
    private readonly AppPaths _paths;

    public GrowRepository(AppPaths paths)
    {
        _paths = paths;
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

    public List<Tent> GetTents()
    {
        using var connection = OpenConnection();
        using var tentCommand = connection.CreateCommand();
        tentCommand.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount
            FROM Tents t
            ORDER BY t.DisplayOrder, t.Name;
        """;

        var tents = new List<Tent>();
        using (var reader = tentCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                tents.Add(MapTent(reader));
            }
        }

        if (tents.Count == 0) return tents;

        var tentPlaceholders = string.Join(", ", tents.Select((_, i) => $"$t{i}"));
        using var growCommand = connection.CreateCommand();
        growCommand.CommandText = $"""
            SELECT g.*, t.Name AS TentName,
                   (SELECT COUNT(*) FROM Measurements m WHERE m.GrowId = g.Id) AS MeasurementCount,
                   (SELECT RelativePath FROM Photos p WHERE p.GrowId = g.Id ORDER BY p.TakenAtUtc DESC LIMIT 1) AS LatestPhotoPath
            FROM Grows g
            LEFT JOIN Tents t ON t.Id = g.TentId
            WHERE g.TentId IN ({tentPlaceholders}) AND g.Status IN ('Planning','Running')
            ORDER BY g.StartDate DESC, g.Id DESC;
        """;
        for (var i = 0; i < tents.Count; i++)
        {
            growCommand.Parameters.AddWithValue($"$t{i}", tents[i].Id);
        }

        var allGrows = new List<GrowRun>();
        using (var growReader = growCommand.ExecuteReader())
        {
            while (growReader.Read())
            {
                allGrows.Add(MapGrow(growReader));
            }
        }

        if (allGrows.Count > 0)
        {
            var latestMeasurements = GetLatestMeasurementsBatch(connection, allGrows.Select(g => g.Id));
            foreach (var grow in allGrows)
            {
                grow.LatestMeasurement = latestMeasurements.GetValueOrDefault(grow.Id);
            }
        }

        var growsByTentId = allGrows
            .Where(g => g.TentId.HasValue)
            .GroupBy(g => g.TentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var tent in tents)
        {
            tent.ActiveGrows = growsByTentId.TryGetValue(tent.Id, out var grows) ? grows : [];
        }

        return tents;
    }

    public Tent? GetTent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.*,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Planning','Running')) AS ActiveGrowCount,
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount
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
        tent.ActiveGrows = GetActiveGrowsForTent(tent.Id);
        return tent;
    }

    public void UpdateTent(Tent tent)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Tents SET
                Name = $name,
                Kind = $kind,
                Notes = $notes,
                DisplayOrder = $displayOrder,
                AccentColor = $accentColor,
                TemperatureEntityId = $temperatureEntityId,
                HumidityEntityId = $humidityEntityId,
                VpdEntityId = $vpdEntityId,
                ReservoirPhEntityId = $reservoirPhEntityId,
                ReservoirEcEntityId = $reservoirEcEntityId,
                ReservoirLevelEntityId = $reservoirLevelEntityId,
                ReservoirTempEntityId = $reservoirTempEntityId,
                OrpEntityId = $orpEntityId,
                DissolvedOxygenEntityId = $dissolvedOxygenEntityId,
                Co2EntityId = $co2EntityId,
                LightEntityId = $lightEntityId,
                CameraEntityId = $cameraEntityId,
                LightCycle = $lightCycle,
                PpfdEntityId = $ppfdEntityId,
                PpfdTarget = $ppfdTarget,
                WidthCm = $widthCm,
                DepthCm = $depthCm,
                TentHeightCm = $tentHeightCm,
                LightType = $lightType,
                LightWatt = $lightWatt,
                ExhaustFanCount = $exhaustFanCount,
                ExhaustM3h = $exhaustM3h,
                CirculationFanCount = $circulationFanCount,
                Co2Type = $co2Type,
                Co2TargetPpm = $co2TargetPpm
            WHERE Id = $id;
        """;
        AddTentParameters(command, tent);
        command.Parameters.AddWithValue("$id", tent.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteTent(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tents WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public Tent CreateTent(string name)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tents (Name, Kind, Notes, DisplayOrder, AccentColor)
            VALUES ($name, 'Grow Tent', NULL, 99, '#69b578');
            SELECT last_insert_rowid();
        """;
        command.Parameters.AddWithValue("$name", name);
        var id = Convert.ToInt32((long)(command.ExecuteScalar() ?? 0L));
        return new Tent { Id = id, Name = name };
    }

    public List<GrowSystem> GetSystems()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM GrowSystems ORDER BY DisplayOrder, Name;
        """;
        var list = new List<GrowSystem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add(MapGrowSystem(reader));
        return list;
    }

    public GrowSystem? GetSystem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM GrowSystems WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapGrowSystem(reader) : null;
    }

    public GrowSystem CreateSystem(GrowSystem system)
    {
        system.CreatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO GrowSystems (Name, HydroStyle, PotCount, PotSizeLiters, ReservoirLiters, Notes, DisplayOrder, CreatedAtUtc)
            VALUES ($name, $hydroStyle, $potCount, $potSizeLiters, $reservoirLiters, $notes, $displayOrder, $createdAtUtc);
            SELECT last_insert_rowid();
        """;
        AddGrowSystemParameters(command, system);
        system.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return system;
    }

    public void UpdateSystem(GrowSystem system)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE GrowSystems SET
                Name            = $name,
                HydroStyle      = $hydroStyle,
                PotCount        = $potCount,
                PotSizeLiters   = $potSizeLiters,
                ReservoirLiters = $reservoirLiters,
                Notes           = $notes,
                DisplayOrder    = $displayOrder
            WHERE Id = $id;
        """;
        AddGrowSystemParameters(command, system);
        command.Parameters.AddWithValue("$id", system.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteSystem(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GrowSystems WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
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
                   (SELECT COUNT(*) FROM Grows g WHERE g.TentId = t.Id AND g.Status IN ('Completed','Aborted')) AS ArchivedGrowCount
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

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Grows
            (
                TentId, Name, Strain, Breeder, Status, MediumType, FeedingStyle, HydroStyle, MediumDetail,
                Environment, Light, ContainerSize, ReservoirSize, IrrigationStyle, IrrigationType, WaterSource,
                SeedType, StartMaterial, GerminationMethod, CloneSource, CloneIsRooted,
                BreederFlowerWeeksMin, BreederFlowerWeeksMax, PlantCount, PhenoNumber,
                PropagationMedium, HasChiller, EntryPoint, DaysAlreadyInPhase,
                AutoflowerDaysSinceGermination, FlipDate, GerminatedAt, RootedAt,
                Nutrients, Notes, StartDate, EndDate, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES
            (
                $tentId, $name, $strain, $breeder, $status, $mediumType, $feedingStyle, $hydroStyle, $mediumDetail,
                $environment, $light, $containerSize, $reservoirSize, $irrigationStyle, $irrigationType, $waterSource,
                $seedType, $startMaterial, $germinationMethod, $cloneSource, $cloneIsRooted,
                $breederFlowerWeeksMin, $breederFlowerWeeksMax, $plantCount, $phenoNumber,
                $propagationMedium, $hasChiller, $entryPoint, $daysAlreadyInPhase,
                $autoflowerDaysSinceGermination, $flipDate, $germinatedAt, $rootedAt,
                $nutrients, $notes, $startDate, $endDate, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddGrowParameters(command, grow);
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

    public List<Measurement> GetMeasurementsForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM Measurements
            WHERE GrowId = $growId
            ORDER BY TakenAt DESC, Id DESC;
        """;
        command.Parameters.AddWithValue("$growId", growId);

        var items = new List<Measurement>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapMeasurement(reader));
        }

        return items;
    }

    public List<Measurement> GetMeasurementsForTent(int tentId, int limit = 200)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.*
            FROM Measurements m
            INNER JOIN Grows g ON g.Id = m.GrowId
            WHERE g.TentId = $tentId
            ORDER BY m.TakenAt DESC, m.Id DESC
            LIMIT $limit;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<Measurement>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapMeasurement(reader));
        }
        return items;
    }

    public Measurement? GetMeasurement(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Measurements WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMeasurement(reader) : null;
    }

    public Measurement? GetLatestMeasurement(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Measurements WHERE GrowId = $growId ORDER BY TakenAt DESC, Id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$growId", growId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMeasurement(reader) : null;
    }

    public Measurement? GetPreviousMeasurement(int growId, DateTime beforeTakenAt, int currentMeasurementId = 0)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM Measurements
            WHERE GrowId = $growId
              AND TakenAt < $beforeTakenAt
              AND Id <> $currentMeasurementId
            ORDER BY TakenAt DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        command.Parameters.AddWithValue("$beforeTakenAt", ToStorage(beforeTakenAt));
        command.Parameters.AddWithValue("$currentMeasurementId", currentMeasurementId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMeasurement(reader) : null;
    }

    public int CreateMeasurement(Measurement measurement)
    {
        measurement.CreatedAtUtc = DateTime.UtcNow;
        measurement.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Measurements
            (
                GrowId, TakenAt, Stage, Source, Notes,
                AirTemperatureC, HumidityPercent, HeightCm,
                WaterAmountMl, RunoffAmountMl, IrrigationPh, IrrigationEc, DrainPh, DrainEc,
                ReservoirPh, ReservoirEc, ReservoirWaterTempC, ReservoirLevelCm, ReservoirLevelLiters,
                DissolvedOxygenMgL, OrpMv, TopOffLiters, AddbackEc, SolutionChange,
                PpfdMol, Co2Ppm, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES
            (
                $growId, $takenAt, $stage, $source, $notes,
                $airTemperatureC, $humidityPercent, $heightCm,
                $waterAmountMl, $runoffAmountMl, $irrigationPh, $irrigationEc, $drainPh, $drainEc,
                $reservoirPh, $reservoirEc, $reservoirWaterTempC, $reservoirLevelCm, $reservoirLevelLiters,
                $dissolvedOxygenMgL, $orpMv, $topOffLiters, $addbackEc, $solutionChange,
                $ppfdMol, $co2Ppm, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddMeasurementParameters(command, measurement);
        return Convert.ToInt32((long)command.ExecuteScalar()!);
    }

    public void UpdateMeasurement(Measurement measurement)
    {
        measurement.UpdatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Measurements SET
                TakenAt = $takenAt,
                Stage = $stage,
                Source = $source,
                Notes = $notes,
                AirTemperatureC = $airTemperatureC,
                HumidityPercent = $humidityPercent,
                HeightCm = $heightCm,
                WaterAmountMl = $waterAmountMl,
                RunoffAmountMl = $runoffAmountMl,
                IrrigationPh = $irrigationPh,
                IrrigationEc = $irrigationEc,
                DrainPh = $drainPh,
                DrainEc = $drainEc,
                ReservoirPh = $reservoirPh,
                ReservoirEc = $reservoirEc,
                ReservoirWaterTempC = $reservoirWaterTempC,
                ReservoirLevelCm = $reservoirLevelCm,
                ReservoirLevelLiters = $reservoirLevelLiters,
                DissolvedOxygenMgL = $dissolvedOxygenMgL,
                OrpMv = $orpMv,
                TopOffLiters = $topOffLiters,
                AddbackEc = $addbackEc,
                SolutionChange = $solutionChange,
                PpfdMol = $ppfdMol,
                Co2Ppm = $co2Ppm,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddMeasurementParameters(command, measurement);
        command.Parameters.AddWithValue("$id", measurement.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteMeasurement(int id)
    {
        using var connection = OpenConnection();
        using var photoCommand = connection.CreateCommand();
        photoCommand.CommandText = "SELECT RelativePath FROM Photos WHERE MeasurementId = $id;";
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
        command.CommandText = "DELETE FROM Measurements WHERE Id = $id;";
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

    public List<PhotoAsset> GetPhotosForGrow(int growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE GrowId = $growId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$growId", growId);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public List<PhotoAsset> GetPhotosForMeasurement(int measurementId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos WHERE MeasurementId = $measurementId ORDER BY TakenAtUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$measurementId", measurementId);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public void AddPhoto(PhotoAsset photo)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Photos (GrowId, MeasurementId, RelativePath, Caption, Tag, Source, IsReferenceShot, TakenAtUtc)
            VALUES ($growId, $measurementId, $relativePath, $caption, $tag, $source, $isReferenceShot, $takenAtUtc);
        """;
        command.Parameters.AddWithValue("$growId", photo.GrowId);
        command.Parameters.AddWithValue("$measurementId", (object?)photo.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$relativePath", photo.RelativePath);
        command.Parameters.AddWithValue("$caption", (object?)photo.Caption ?? DBNull.Value);
        command.Parameters.AddWithValue("$tag", photo.Tag.ToString());
        command.Parameters.AddWithValue("$source", photo.Source.ToString());
        command.Parameters.AddWithValue("$isReferenceShot", photo.IsReferenceShot ? 1 : 0);
        command.Parameters.AddWithValue("$takenAtUtc", ToStorageUtc(photo.TakenAtUtc));
        command.ExecuteNonQuery();
    }


    public List<PhotoAsset> GetRecentPhotos(int limit = 18)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Photos ORDER BY TakenAtUtc DESC, Id DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<PhotoAsset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapPhoto(reader));
        }
        return items;
    }

    public HomeAssistantSettings GetHomeAssistantSettings()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM AppSettings WHERE Key IN ('ha:baseUrl','ha:accessToken','ha:enabled');";
        using var reader = command.ExecuteReader();

        var settings = new HomeAssistantSettings();
        while (reader.Read())
        {
            var key = reader["Key"]?.ToString();
            var value = reader["Value"]?.ToString();
            switch (key)
            {
                case "ha:baseUrl":
                    settings.BaseUrl = value;
                    break;
                case "ha:accessToken":
                    settings.AccessToken = value;
                    break;
                case "ha:enabled":
                    settings.Enabled = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }
        return settings;
    }

    public void SaveHomeAssistantSettings(HomeAssistantSettings settings)
    {
        using var connection = OpenConnection();
        UpsertSetting(connection, "ha:baseUrl", settings.BaseUrl);
        UpsertSetting(connection, "ha:accessToken", settings.AccessToken);
        UpsertSetting(connection, "ha:enabled", settings.Enabled ? "1" : "0");
    }

    public void AddTentSensorSnapshot(TentSensorSnapshot snapshot, TimeSpan? dedupeWindow = null)
    {
        var window = dedupeWindow ?? TimeSpan.FromMinutes(4);
        using var connection = OpenConnection();
        using var dedupe = connection.CreateCommand();
        dedupe.CommandText = """
            SELECT COUNT(*) FROM TentSensorSnapshots
            WHERE TentId = $tentId AND MetricKey = $metricKey AND CapturedAtUtc >= $threshold;
        """;
        dedupe.Parameters.AddWithValue("$tentId", snapshot.TentId);
        dedupe.Parameters.AddWithValue("$metricKey", snapshot.MetricKey);
        dedupe.Parameters.AddWithValue("$threshold", ToStorageUtc(snapshot.CapturedAtUtc.Subtract(window)));
        var recentCount = Convert.ToInt32(dedupe.ExecuteScalar() ?? 0);
        if (recentCount > 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TentSensorSnapshots (TentId, MetricKey, Value, Unit, CapturedAtUtc)
            VALUES ($tentId, $metricKey, $value, $unit, $capturedAtUtc);
        """;
        command.Parameters.AddWithValue("$tentId", snapshot.TentId);
        command.Parameters.AddWithValue("$metricKey", snapshot.MetricKey);
        command.Parameters.AddWithValue("$value", snapshot.Value);
        command.Parameters.AddWithValue("$unit", (object?)snapshot.Unit ?? DBNull.Value);
        command.Parameters.AddWithValue("$capturedAtUtc", ToStorageUtc(snapshot.CapturedAtUtc));
        command.ExecuteNonQuery();
    }

    public List<TentSensorSnapshot> GetTentSensorSnapshots(int tentId, IEnumerable<string>? metricKeys = null, int limitPerMetric = 48)
    {
        var keys = metricKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                   ?? ["temperature", "humidity", "vpd", "reservoir-ph", "reservoir-ec", "reservoir-level", "reservoir-temp"];
        if (keys.Count == 0)
        {
            return [];
        }

        using var connection = OpenConnection();
        var placeholders = string.Join(", ", keys.Select((_, i) => $"$k{i}"));
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            WITH ranked AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY MetricKey ORDER BY CapturedAtUtc DESC) AS rn
                FROM TentSensorSnapshots
                WHERE TentId = $tentId AND MetricKey IN ({placeholders})
            )
            SELECT * FROM ranked WHERE rn <= $limit;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$limit", limitPerMetric);
        for (var i = 0; i < keys.Count; i++)
        {
            command.Parameters.AddWithValue($"$k{i}", keys[i]);
        }

        var items = new List<TentSensorSnapshot>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(MapTentSensorSnapshot(reader));
        }
        return items;
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
            var m = MapMeasurement(reader);
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

    private static void UpsertSetting(SqliteConnection connection, string key, string? value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
        """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static GrowRun MapGrow(SqliteDataReader reader)
    {
        return new GrowRun
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            TentId = reader["TentId"] is DBNull ? null : Convert.ToInt32((long)reader["TentId"]),
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
            Notes = NullString(reader["Notes"]),
            DisplayOrder = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
            AccentColor = reader["AccentColor"]?.ToString() ?? "#69b578",
            TemperatureEntityId = NullString(reader["TemperatureEntityId"]),
            HumidityEntityId = NullString(reader["HumidityEntityId"]),
            VpdEntityId = NullString(reader["VpdEntityId"]),
            ReservoirPhEntityId = NullString(reader["ReservoirPhEntityId"]),
            ReservoirEcEntityId = NullString(reader["ReservoirEcEntityId"]),
            ReservoirLevelEntityId = NullString(reader["ReservoirLevelEntityId"]),
            ReservoirTempEntityId = NullString(reader["ReservoirTempEntityId"]),
            OrpEntityId             = NullString(reader["OrpEntityId"]),
            DissolvedOxygenEntityId = NullString(reader["DissolvedOxygenEntityId"]),
            Co2EntityId             = NullString(reader["Co2EntityId"]),
            LightEntityId = NullString(reader["LightEntityId"]),
            CameraEntityId = NullString(reader["CameraEntityId"]),
            LightCycle = NullString(reader["LightCycle"]),
            PpfdEntityId = NullString(reader["PpfdEntityId"]),
            PpfdTarget = NullString(reader["PpfdTarget"]),
            WidthCm             = reader["WidthCm"] is DBNull or null ? null : Convert.ToInt32(reader["WidthCm"], CultureInfo.InvariantCulture),
            DepthCm             = reader["DepthCm"] is DBNull or null ? null : Convert.ToInt32(reader["DepthCm"], CultureInfo.InvariantCulture),
            TentHeightCm        = reader["TentHeightCm"] is DBNull or null ? null : Convert.ToInt32(reader["TentHeightCm"], CultureInfo.InvariantCulture),
            LightType           = NullString(reader["LightType"]),
            LightWatt           = reader["LightWatt"] is DBNull or null ? null : Convert.ToInt32(reader["LightWatt"], CultureInfo.InvariantCulture),
            ExhaustFanCount     = reader["ExhaustFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustFanCount"], CultureInfo.InvariantCulture),
            ExhaustM3h          = reader["ExhaustM3h"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustM3h"], CultureInfo.InvariantCulture),
            CirculationFanCount = reader["CirculationFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["CirculationFanCount"], CultureInfo.InvariantCulture),
            Co2Type             = NullString(reader["Co2Type"]),
            Co2TargetPpm        = reader["Co2TargetPpm"] is DBNull or null ? null : Convert.ToInt32(reader["Co2TargetPpm"], CultureInfo.InvariantCulture),
            ActiveGrowCount = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture),
            ArchivedGrowCount = reader["ArchivedGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedGrowCount"], CultureInfo.InvariantCulture)
        };
    }

    private static Measurement MapMeasurement(SqliteDataReader reader)
    {
        return new Measurement
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            GrowId = Convert.ToInt32((long)reader["GrowId"]),
            TakenAt = ParseStoredDateTime(reader["TakenAt"]?.ToString()) ?? DateTime.Now,
            Stage = ParseEnum(reader["Stage"]?.ToString(), GrowStage.Veg),
            Source = ParseEnum(reader["Source"]?.ToString(), ValueOrigin.Manual),
            Notes = NullString(reader["Notes"]),
            AirTemperatureC = NullableDouble(reader["AirTemperatureC"]),
            HumidityPercent = NullableDouble(reader["HumidityPercent"]),
            HeightCm = NullableDouble(reader["HeightCm"]),
            WaterAmountMl = NullableDouble(reader["WaterAmountMl"]),
            RunoffAmountMl = NullableDouble(reader["RunoffAmountMl"]),
            IrrigationPh = NullableDouble(reader["IrrigationPh"]),
            IrrigationEc = NullableDouble(reader["IrrigationEc"]),
            DrainPh = NullableDouble(reader["DrainPh"]),
            DrainEc = NullableDouble(reader["DrainEc"]),
            ReservoirPh = NullableDouble(reader["ReservoirPh"]),
            ReservoirEc = NullableDouble(reader["ReservoirEc"]),
            ReservoirWaterTempC = NullableDouble(reader["ReservoirWaterTempC"]),
            ReservoirLevelCm = NullableDouble(reader["ReservoirLevelCm"]),
            ReservoirLevelLiters = NullableDouble(reader["ReservoirLevelLiters"]),
            DissolvedOxygenMgL = NullableDouble(reader["DissolvedOxygenMgL"]),
            OrpMv = NullableDouble(reader["OrpMv"]),
            TopOffLiters = NullableDouble(reader["TopOffLiters"]),
            AddbackEc = NullableDouble(reader["AddbackEc"]),
            SolutionChange = reader["SolutionChange"] is not DBNull && Convert.ToInt32(reader["SolutionChange"], CultureInfo.InvariantCulture) == 1,
            PpfdMol = NullableDouble(reader["PpfdMol"]),
            Co2Ppm = NullableDouble(reader["Co2Ppm"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static PhotoAsset MapPhoto(SqliteDataReader reader)
    {
        return new PhotoAsset
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            GrowId = Convert.ToInt32((long)reader["GrowId"]),
            MeasurementId = reader["MeasurementId"] is DBNull ? null : Convert.ToInt32((long)reader["MeasurementId"]),
            RelativePath = reader["RelativePath"]?.ToString() ?? string.Empty,
            Caption = NullString(reader["Caption"]),
            Tag = ParseEnum(reader["Tag"]?.ToString(), PhotoTag.Overview),
            Source = ParseEnum(reader["Source"]?.ToString(), ValueOrigin.Manual),
            IsReferenceShot = reader["IsReferenceShot"] is not DBNull && Convert.ToInt32(reader["IsReferenceShot"], CultureInfo.InvariantCulture) == 1,
            TakenAtUtc = ParseStoredDateTime(reader["TakenAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static TentSensorSnapshot MapTentSensorSnapshot(SqliteDataReader reader)
    {
        return new TentSensorSnapshot
        {
            Id = Convert.ToInt32((long)reader["Id"]),
            TentId = Convert.ToInt32((long)reader["TentId"]),
            MetricKey = reader["MetricKey"]?.ToString() ?? string.Empty,
            Value = Convert.ToDouble(reader["Value"], CultureInfo.InvariantCulture),
            Unit = NullString(reader["Unit"]),
            CapturedAtUtc = ParseStoredDateTime(reader["CapturedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddGrowParameters(SqliteCommand command, GrowRun grow)
    {
        command.Parameters.AddWithValue("$tentId", (object?)grow.TentId ?? DBNull.Value);
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

    private static void AddTentParameters(SqliteCommand command, Tent tent)
    {
        command.Parameters.AddWithValue("$name", tent.Name);
        command.Parameters.AddWithValue("$kind", tent.Kind);
        command.Parameters.AddWithValue("$notes", (object?)tent.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$displayOrder", tent.DisplayOrder);
        command.Parameters.AddWithValue("$accentColor", tent.AccentColor);
        command.Parameters.AddWithValue("$temperatureEntityId", (object?)tent.TemperatureEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$humidityEntityId", (object?)tent.HumidityEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$vpdEntityId", (object?)tent.VpdEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirPhEntityId", (object?)tent.ReservoirPhEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirEcEntityId", (object?)tent.ReservoirEcEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirLevelEntityId", (object?)tent.ReservoirLevelEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirTempEntityId", (object?)tent.ReservoirTempEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$orpEntityId", (object?)tent.OrpEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$dissolvedOxygenEntityId", (object?)tent.DissolvedOxygenEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$co2EntityId", (object?)tent.Co2EntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$lightEntityId", (object?)tent.LightEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$cameraEntityId", (object?)tent.CameraEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$lightCycle", (object?)tent.LightCycle ?? DBNull.Value);
        command.Parameters.AddWithValue("$ppfdEntityId", (object?)tent.PpfdEntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$ppfdTarget", (object?)tent.PpfdTarget ?? DBNull.Value);
        command.Parameters.AddWithValue("$widthCm", (object?)tent.WidthCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$depthCm", (object?)tent.DepthCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$tentHeightCm", (object?)tent.TentHeightCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$lightType", (object?)tent.LightType ?? DBNull.Value);
        command.Parameters.AddWithValue("$lightWatt", (object?)tent.LightWatt ?? DBNull.Value);
        command.Parameters.AddWithValue("$exhaustFanCount", (object?)tent.ExhaustFanCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$exhaustM3h", (object?)tent.ExhaustM3h ?? DBNull.Value);
        command.Parameters.AddWithValue("$circulationFanCount", (object?)tent.CirculationFanCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$co2Type", (object?)tent.Co2Type ?? DBNull.Value);
        command.Parameters.AddWithValue("$co2TargetPpm", (object?)tent.Co2TargetPpm ?? DBNull.Value);
    }

    private static GrowSystem MapGrowSystem(SqliteDataReader reader)
    {
        return new GrowSystem
        {
            Id              = Convert.ToInt32((long)reader["Id"]),
            Name            = reader["Name"]?.ToString() ?? string.Empty,
            HydroStyle      = reader["HydroStyle"]?.ToString() ?? string.Empty,
            PotCount        = reader["PotCount"] is DBNull or null ? null : Convert.ToInt32(reader["PotCount"], CultureInfo.InvariantCulture),
            PotSizeLiters   = reader["PotSizeLiters"] is DBNull or null ? null : Convert.ToDouble(reader["PotSizeLiters"], CultureInfo.InvariantCulture),
            ReservoirLiters = reader["ReservoirLiters"] is DBNull or null ? null : Convert.ToDouble(reader["ReservoirLiters"], CultureInfo.InvariantCulture),
            Notes           = NullString(reader["Notes"]),
            DisplayOrder    = Convert.ToInt32(reader["DisplayOrder"], CultureInfo.InvariantCulture),
            CreatedAtUtc    = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddGrowSystemParameters(SqliteCommand command, GrowSystem system)
    {
        command.Parameters.AddWithValue("$name", system.Name);
        command.Parameters.AddWithValue("$hydroStyle", system.HydroStyle);
        command.Parameters.AddWithValue("$potCount", (object?)system.PotCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$potSizeLiters", (object?)system.PotSizeLiters ?? DBNull.Value);
        command.Parameters.AddWithValue("$reservoirLiters", (object?)system.ReservoirLiters ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)system.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$displayOrder", system.DisplayOrder);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(system.CreatedAtUtc));
    }

    private static void AddMeasurementParameters(SqliteCommand command, Measurement measurement)
    {
        command.Parameters.AddWithValue("$growId", measurement.GrowId);
        command.Parameters.AddWithValue("$takenAt", ToStorage(measurement.TakenAt));
        command.Parameters.AddWithValue("$stage", measurement.Stage.ToString());
        command.Parameters.AddWithValue("$source", measurement.Source.ToString());
        command.Parameters.AddWithValue("$notes", (object?)measurement.Notes ?? DBNull.Value);
        AddNullable(command, "$airTemperatureC", measurement.AirTemperatureC);
        AddNullable(command, "$humidityPercent", measurement.HumidityPercent);
        AddNullable(command, "$heightCm", measurement.HeightCm);
        AddNullable(command, "$waterAmountMl", measurement.WaterAmountMl);
        AddNullable(command, "$runoffAmountMl", measurement.RunoffAmountMl);
        AddNullable(command, "$irrigationPh", measurement.IrrigationPh);
        AddNullable(command, "$irrigationEc", measurement.IrrigationEc);
        AddNullable(command, "$drainPh", measurement.DrainPh);
        AddNullable(command, "$drainEc", measurement.DrainEc);
        AddNullable(command, "$reservoirPh", measurement.ReservoirPh);
        AddNullable(command, "$reservoirEc", measurement.ReservoirEc);
        AddNullable(command, "$reservoirWaterTempC", measurement.ReservoirWaterTempC);
        AddNullable(command, "$reservoirLevelCm", measurement.ReservoirLevelCm);
        AddNullable(command, "$reservoirLevelLiters", measurement.ReservoirLevelLiters);
        AddNullable(command, "$dissolvedOxygenMgL", measurement.DissolvedOxygenMgL);
        AddNullable(command, "$orpMv", measurement.OrpMv);
        AddNullable(command, "$topOffLiters", measurement.TopOffLiters);
        AddNullable(command, "$addbackEc", measurement.AddbackEc);
        command.Parameters.AddWithValue("$solutionChange", measurement.SolutionChange ? 1 : 0);
        AddNullable(command, "$ppfdMol", measurement.PpfdMol);
        AddNullable(command, "$co2Ppm", measurement.Co2Ppm);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(measurement.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(measurement.UpdatedAtUtc));
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

        var uploadsRoot = Path.GetFullPath(Path.Combine(_paths.ContentRootPath, "wwwroot", "uploads"));
        var candidatePath = Path.GetFullPath(Path.Combine(_paths.ContentRootPath, "wwwroot", normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (!candidatePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        physicalPath = candidatePath;
        return true;
    }

    private static void AddNullable(SqliteCommand command, string name, double? value)
        => command.Parameters.AddWithValue(name, value.HasValue ? value.Value : DBNull.Value);

    private static string? NullString(object value)
        => value is DBNull ? null : value?.ToString();

    private static double? NullableDouble(object value)
        => value is DBNull ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(raw, out var parsed) ? parsed : fallback;

    private static DateTime? ParseStoredDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    private static DateTime? ParseStoredDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result) ? result.Date : null;

    private static string ToStorage(DateTime value)
        => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static string ToStorageUtc(DateTime value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
