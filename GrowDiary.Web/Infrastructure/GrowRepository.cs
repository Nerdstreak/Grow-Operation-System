using System.Globalization;
using System.Text.Json;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge.Schema;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class GrowRepository
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppPaths _paths;
    private readonly TentRepository _tentRepository;
    private readonly HydroSetupRepository _hydroSetupRepository;
    private readonly AddbackRepository _addbackRepository;
    private readonly HardwareRepository _hardwareRepository;
    private readonly SetupRepository _setupRepository;
    private readonly AutoMeasurementRepository _autoMeasurementRepository;
    private readonly LightRepository _lightRepository;
    private readonly SopRepository _sopRepository;
    private readonly PhotoRepository _photoRepository;
    private readonly HomeAssistantSettingsRepository _homeAssistantSettingsRepository;

    public GrowRepository(AppPaths paths)
        : this(paths, new TentRepository(paths))
    {
    }

    private GrowRepository(AppPaths paths, TentRepository tentRepository)
        : this(paths, tentRepository, new HydroSetupRepository(paths, tentRepository), new AddbackRepository(paths), new HardwareRepository(paths), new SetupRepository(paths), new AutoMeasurementRepository(paths), new LightRepository(paths), new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository)
        : this(paths, tentRepository, hydroSetupRepository, new AddbackRepository(paths), new HardwareRepository(paths), new SetupRepository(paths), new AutoMeasurementRepository(paths), new LightRepository(paths), new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, new HardwareRepository(paths), new SetupRepository(paths), new AutoMeasurementRepository(paths), new LightRepository(paths), new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, hardwareRepository, new SetupRepository(paths), new AutoMeasurementRepository(paths), new LightRepository(paths), new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository, SetupRepository setupRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, hardwareRepository, setupRepository, new AutoMeasurementRepository(paths), new LightRepository(paths), new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository, SetupRepository setupRepository, AutoMeasurementRepository autoMeasurementRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, hardwareRepository, setupRepository, autoMeasurementRepository, new LightRepository(paths), new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository, SetupRepository setupRepository, AutoMeasurementRepository autoMeasurementRepository, LightRepository lightRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, hardwareRepository, setupRepository, autoMeasurementRepository, lightRepository, new SopRepository(paths), new PhotoRepository(paths), new HomeAssistantSettingsRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository, SetupRepository setupRepository, AutoMeasurementRepository autoMeasurementRepository, LightRepository lightRepository, SopRepository sopRepository, PhotoRepository photoRepository, HomeAssistantSettingsRepository homeAssistantSettingsRepository)
    {
        _paths = paths;
        _tentRepository = tentRepository;
        _hydroSetupRepository = hydroSetupRepository;
        _addbackRepository = addbackRepository;
        _hardwareRepository = hardwareRepository;
        _setupRepository = setupRepository;
        _autoMeasurementRepository = autoMeasurementRepository;
        _lightRepository = lightRepository;
        _sopRepository = sopRepository;
        _photoRepository = photoRepository;
        _homeAssistantSettingsRepository = homeAssistantSettingsRepository;
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

    public List<Tent> GetTents(bool includeArchived = false)
        => _tentRepository.GetTents(includeArchived);

    public Tent? GetTent(int id)
        => _tentRepository.GetTent(id);

    public void UpdateTent(Tent tent)
        => _tentRepository.UpdateTent(tent);

    public void DeleteTent(int id)
        => _tentRepository.DeleteTent(id);

    public bool HasTentDependencies(int id)
        => _tentRepository.HasTentDependencies(id);

    public void ArchiveTent(int id)
        => _tentRepository.ArchiveTent(id);

    public Tent CreateTent(string name)
        => _tentRepository.CreateTent(name);

    public Tent CreateTent(Tent tent)
        => _tentRepository.CreateTent(tent);

    public List<TentSensor> GetTentSensors(int tentId)
        => _tentRepository.GetTentSensors(tentId);

    public TentSensor AddTentSensor(TentSensor sensor)
        => _tentRepository.AddTentSensor(sensor);

    public void UpdateTentSensor(TentSensor sensor)
        => _tentRepository.UpdateTentSensor(sensor);

    public void ReplaceTentSensors(int tentId, IReadOnlyCollection<TentSensor> sensors)
        => _tentRepository.ReplaceTentSensors(tentId, sensors);

    public void DeleteTentSensor(int sensorId)
        => _tentRepository.DeleteTentSensor(sensorId);

    public TentSensor? GetTentSensor(int id)
        => _tentRepository.GetTentSensor(id);

    public HardwareItem CreateHardwareItem(HardwareItem item)
        => _hardwareRepository.CreateHardwareItem(item);

    public void UpdateHardwareItem(HardwareItem item)
        => _hardwareRepository.UpdateHardwareItem(item);

    public HardwareItem? GetHardwareItem(int id)
        => _hardwareRepository.GetHardwareItem(id);

    public List<HardwareItem> GetHardwareItems()
        => _hardwareRepository.GetHardwareItems();

    public List<HardwareItem> GetHardwareItemsByTent(int tentId)
        => _hardwareRepository.GetHardwareItemsByTent(tentId);

    public List<HardwareItem> GetHardwareItemsByHydroSetup(int hydroSetupId)
        => _hardwareRepository.GetHardwareItemsByHydroSetup(hydroSetupId);

    public List<HardwareItem> GetHardwareItemsByStatus(HardwareItemStatus status)
        => _hardwareRepository.GetHardwareItemsByStatus(status);

    public MaintenanceEvent CreateMaintenanceEvent(MaintenanceEvent item)
        => _hardwareRepository.CreateMaintenanceEvent(item);

    public void UpdateMaintenanceEvent(MaintenanceEvent item)
        => _hardwareRepository.UpdateMaintenanceEvent(item);

    public MaintenanceEvent? GetMaintenanceEvent(int id)
        => _hardwareRepository.GetMaintenanceEvent(id);

    public List<MaintenanceEvent> GetMaintenanceEvents()
        => _hardwareRepository.GetMaintenanceEvents();

    public List<MaintenanceEvent> GetMaintenanceEventsByHardwareItem(int hardwareItemId)
        => _hardwareRepository.GetMaintenanceEventsByHardwareItem(hardwareItemId);

    public List<MaintenanceEvent> GetOpenMaintenanceEventsByHardwareItem(int hardwareItemId)
        => _hardwareRepository.GetOpenMaintenanceEventsByHardwareItem(hardwareItemId);

    public List<MaintenanceEvent> GetDueMaintenanceEvents(DateTime nowUtc)
        => _hardwareRepository.GetDueMaintenanceEvents(nowUtc);

    public CalibrationEvent CreateCalibrationEvent(CalibrationEvent item)
        => _hardwareRepository.CreateCalibrationEvent(item);

    public void UpdateCalibrationEvent(CalibrationEvent item)
        => _hardwareRepository.UpdateCalibrationEvent(item);

    public CalibrationEvent? GetCalibrationEvent(int id)
        => _hardwareRepository.GetCalibrationEvent(id);

    public List<CalibrationEvent> GetCalibrationEvents()
        => _hardwareRepository.GetCalibrationEvents();

    public List<CalibrationEvent> GetCalibrationEventsByHardwareItem(int hardwareItemId)
        => _hardwareRepository.GetCalibrationEventsByHardwareItem(hardwareItemId);

    public List<CalibrationEvent> GetOpenCalibrationEventsByHardwareItem(int hardwareItemId)
        => _hardwareRepository.GetOpenCalibrationEventsByHardwareItem(hardwareItemId);

    public List<CalibrationEvent> GetDueCalibrationEvents(DateTime nowUtc)
        => _hardwareRepository.GetDueCalibrationEvents(nowUtc);

    public CalibrationEvent? GetLatestCompletedCalibrationEvent(int hardwareItemId)
        => _hardwareRepository.GetLatestCompletedCalibrationEvent(hardwareItemId);

    public RiskEvent CreateRiskEvent(RiskEvent item)
        => _hardwareRepository.CreateRiskEvent(item);

    public void UpdateRiskEvent(RiskEvent item)
        => _hardwareRepository.UpdateRiskEvent(item);

    public RiskEvent? GetRiskEvent(int id)
        => _hardwareRepository.GetRiskEvent(id);

    public List<RiskEvent> GetRiskEvents()
        => _hardwareRepository.GetRiskEvents();

    public List<RiskEvent> GetOpenRiskEvents()
        => _hardwareRepository.GetOpenRiskEvents();

    public List<RiskEvent> GetRiskEventsByHardwareItem(int hardwareItemId)
        => _hardwareRepository.GetRiskEventsByHardwareItem(hardwareItemId);

    public List<RiskEvent> GetRiskEventsByTent(int tentId)
        => _hardwareRepository.GetRiskEventsByTent(tentId);

    public List<RiskEvent> GetRiskEventsByGrow(int growId)
        => _hardwareRepository.GetRiskEventsByGrow(growId);

    public List<RiskEvent> GetRiskEventsByStatus(RiskEventStatus status)
        => _hardwareRepository.GetRiskEventsByStatus(status);

    public RiskEvent? FindOpenRiskEventByDedupeKey(string dedupeKey)
        => _hardwareRepository.FindOpenRiskEventByDedupeKey(dedupeKey);

    public RiskEvent ResolveRiskEvent(int id, DateTime resolvedAtUtc, string? notes)
        => _hardwareRepository.ResolveRiskEvent(id, resolvedAtUtc, notes);

    public RiskEvent AcknowledgeRiskEvent(int id, DateTime acknowledgedAtUtc, string? notes)
        => _hardwareRepository.AcknowledgeRiskEvent(id, acknowledgedAtUtc, notes);

    public Setup CreateSetup(Setup setup)
        => _setupRepository.CreateSetup(setup);

    public Setup? GetSetup(int id)
        => _setupRepository.GetSetup(id);

    public List<Setup> GetSetups()
        => _setupRepository.GetSetups();

    public List<Setup> GetSetupsForTent(int tentId)
        => _setupRepository.GetSetupsForTent(tentId);

    public void UpdateSetup(Setup setup)
        => _setupRepository.UpdateSetup(setup);

    public Strain CreateStrain(Strain strain)
        => _setupRepository.CreateStrain(strain);

    public void UpdateStrain(Strain strain)
        => _setupRepository.UpdateStrain(strain);

    public Strain? GetStrain(int id)
        => _setupRepository.GetStrain(id);

    public List<Strain> GetStrains()
        => _setupRepository.GetStrains();

    public PlantInstance CreatePlant(PlantInstance plant)
        => _setupRepository.CreatePlant(plant);

    public PlantInstance CreateCloneFromMother(PlantInstance clone, int? motherSetupId, DateTime cutAt)
        => _setupRepository.CreateCloneFromMother(clone, motherSetupId, cutAt);

    public PlantInstance DecideQuarantinePlant(PlantInstance plant, int quarantineSetupId, string quarantineResult)
        => _setupRepository.DecideQuarantinePlant(plant, quarantineSetupId, quarantineResult);

    public void UpdatePlant(PlantInstance plant)
        => _setupRepository.UpdatePlant(plant);

    public PlantInstance? GetPlant(int id)
        => _setupRepository.GetPlant(id);

    public List<PlantInstance> GetPlants()
        => _setupRepository.GetPlants();

    public List<PlantInstance> GetPlantsBySetup(int setupId)
        => _setupRepository.GetPlantsBySetup(setupId);

    public List<PlantInstance> GetPlantsByGrow(int growId)
        => _setupRepository.GetPlantsByGrow(growId);

    public AutoMeasurementConfig CreateAutoMeasurementConfig(AutoMeasurementConfig config)
        => _autoMeasurementRepository.CreateAutoMeasurementConfig(config);

    public void UpdateAutoMeasurementConfig(AutoMeasurementConfig config)
        => _autoMeasurementRepository.UpdateAutoMeasurementConfig(config);

    public AutoMeasurementConfig? GetAutoMeasurementConfig(int id)
        => _autoMeasurementRepository.GetAutoMeasurementConfig(id);

    public List<AutoMeasurementConfig> GetAutoMeasurementConfigs()
        => _autoMeasurementRepository.GetAutoMeasurementConfigs();

    public List<AutoMeasurementConfig> GetAutoMeasurementConfigsByGrow(int growId)
        => _autoMeasurementRepository.GetAutoMeasurementConfigsByGrow(growId);

    public List<AutoMeasurementConfig> GetEnabledAutoMeasurementConfigs()
        => _autoMeasurementRepository.GetEnabledAutoMeasurementConfigs();

    public void ReplaceAutoMeasurementFieldMappings(int configId, IReadOnlyCollection<AutoMeasurementFieldMapping> mappings)
        => _autoMeasurementRepository.ReplaceAutoMeasurementFieldMappings(configId, mappings);

    public List<AutoMeasurementFieldMapping> GetAutoMeasurementFieldMappings(int configId)
        => _autoMeasurementRepository.GetAutoMeasurementFieldMappings(configId);

    public AutoMeasurementRun CreateAutoMeasurementRunIfNotExists(AutoMeasurementRun run)
        => _autoMeasurementRepository.CreateAutoMeasurementRunIfNotExists(run);

    public List<AutoMeasurementRun> GetAutoMeasurementRunsByConfig(int configId)
        => _autoMeasurementRepository.GetAutoMeasurementRunsByConfig(configId);

    public List<AutoMeasurementRun> GetAutoMeasurementRunsByGrow(int growId)
        => _autoMeasurementRepository.GetAutoMeasurementRunsByGrow(growId);

    public AutoMeasurementRun? GetAutoMeasurementRun(int configId, AutoMeasurementTriggerKind triggerKind, DateTime scheduledForUtc)
        => _autoMeasurementRepository.GetAutoMeasurementRun(configId, triggerKind, scheduledForUtc);

    public void UpdateAutoMeasurementRun(AutoMeasurementRun run)
        => _autoMeasurementRepository.UpdateAutoMeasurementRun(run);

    public LightSchedule CreateLightSchedule(LightSchedule schedule)
        => _lightRepository.CreateLightSchedule(schedule);

    public void UpdateLightSchedule(LightSchedule schedule)
        => _lightRepository.UpdateLightSchedule(schedule);

    public LightSchedule? GetLightSchedule(int id)
        => _lightRepository.GetLightSchedule(id);

    public List<LightSchedule> GetLightSchedulesByTent(int tentId)
        => _lightRepository.GetLightSchedulesByTent(tentId);

    public LightSchedule? GetActiveLightScheduleForTent(int tentId)
        => _lightRepository.GetActiveLightScheduleForTent(tentId);

    public LightTransitionEvent CreateLightTransitionIfNotDuplicate(LightTransitionEvent transition)
        => _lightRepository.CreateLightTransitionIfNotDuplicate(transition);

    public List<LightTransitionEvent> GetLightTransitionsByTent(int tentId)
        => _lightRepository.GetLightTransitionsByTent(tentId);

    public List<LightTransitionEvent> GetLightTransitionsByTentAndKindSince(int tentId, LightTransitionKind kind, DateTime sinceUtc)
        => _lightRepository.GetLightTransitionsByTentAndKindSince(tentId, kind, sinceUtc);

    public LightTransitionEvent? GetLatestLightTransitionForTent(int tentId)
        => _lightRepository.GetLatestLightTransitionForTent(tentId);

    public LightTransitionEvent? GetLatestLightTransitionForTentAndKind(int tentId, LightTransitionKind kind)
        => _lightRepository.GetLatestLightTransitionForTentAndKind(tentId, kind);

    public AddbackLogEntry CreateAddbackLog(AddbackLogEntry entry)
        => _addbackRepository.CreateAddbackLog(entry);

    public List<AddbackLogEntry> GetAddbackLogsForGrow(int growId)
        => _addbackRepository.GetAddbackLogsForGrow(growId);

    public ChangeoutEntry CreateChangeout(ChangeoutEntry entry)
        => _addbackRepository.CreateChangeout(entry);

    public List<ChangeoutEntry> GetChangeoutsForGrow(int growId)
        => _addbackRepository.GetChangeoutsForGrow(growId);

    public SopInstance StartSopInstance(
        int growId,
        SopDefinition sopDefinition,
        SopStartSource source,
        string? sourceRecommendationKey,
        string? treatmentRecommendationStableKey,
        string? notes)
        => _sopRepository.StartSopInstance(growId, sopDefinition, source, sourceRecommendationKey, treatmentRecommendationStableKey, notes);

    public SopInstance? GetSopInstance(int id)
        => _sopRepository.GetSopInstance(id);

    public List<SopInstance> GetSopInstancesByGrow(int growId)
        => _sopRepository.GetSopInstancesByGrow(growId);

    public List<SopInstance> GetActiveSopInstancesByGrow(int growId)
        => _sopRepository.GetActiveSopInstancesByGrow(growId);

    public List<SopStepInstance> GetSopStepInstances(int sopInstanceId)
        => _sopRepository.GetSopStepInstances(sopInstanceId);

    public SopStepInstance? GetSopStepInstance(int stepInstanceId)
        => _sopRepository.GetSopStepInstance(stepInstanceId);

    public SopStepInstance UpdateSopStepInstance(
        int stepInstanceId,
        SopStepInstanceStatus status,
        string? notes,
        int? measurementId,
        int? journalEntryId,
        int? photoAssetId)
        => _sopRepository.UpdateSopStepInstance(stepInstanceId, status, notes, measurementId, journalEntryId, photoAssetId);

    public void RecalculateSopInstanceStatus(int sopInstanceId)
        => _sopRepository.RecalculateSopInstanceStatus(sopInstanceId);

    public void UpdateSopStepReminderTaskId(int stepId, int taskId)
        => _sopRepository.UpdateSopStepReminderTaskId(stepId, taskId);

    public TentSensor? GetTentSensorByMetric(int tentId, SensorMetricType metricType)
        => _tentRepository.GetTentSensorByMetric(tentId, metricType);

    public List<GrowSystem> GetSystems(bool includeArchived = true)
        => _hydroSetupRepository.GetSystems(includeArchived);

    public GrowSystem? GetSystem(int id)
        => _hydroSetupRepository.GetSystem(id);

    public List<GrowSystem> GetHydroSetups(bool includeArchived = false)
        => _hydroSetupRepository.GetHydroSetups(includeArchived);

    public GrowSystem? GetHydroSetup(int id)
        => _hydroSetupRepository.GetHydroSetup(id);

    public List<GrowSystem> GetHydroSetupsByTent(int tentId, bool includeArchived = false)
        => _hydroSetupRepository.GetHydroSetupsByTent(tentId, includeArchived);

    public GrowSystem CreateSystem(GrowSystem system)
        => _hydroSetupRepository.CreateSystem(system);

    public GrowSystem CreateHydroSetup(GrowSystem system)
        => _hydroSetupRepository.CreateHydroSetup(system);

    public void UpdateSystem(GrowSystem system)
        => _hydroSetupRepository.UpdateSystem(system);

    public void UpdateHydroSetup(GrowSystem system)
        => _hydroSetupRepository.UpdateHydroSetup(system);

    public void ArchiveHydroSetup(int id)
        => _hydroSetupRepository.ArchiveHydroSetup(id);

    public void DeleteSystem(int id)
        => _hydroSetupRepository.DeleteSystem(id);

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
        => _photoRepository.GetPhotosForGrow(growId);

    public List<PhotoAsset> GetPhotosForMeasurement(int measurementId)
        => _photoRepository.GetPhotosForMeasurement(measurementId);

    public void AddPhoto(PhotoAsset photo)
        => _photoRepository.AddPhoto(photo);


    public List<PhotoAsset> GetRecentPhotos(int limit = 18)
        => _photoRepository.GetRecentPhotos(limit);

    public HomeAssistantSettings GetHomeAssistantSettings()
        => _homeAssistantSettingsRepository.GetHomeAssistantSettings();

    public void SaveHomeAssistantSettings(HomeAssistantSettings settings)
        => _homeAssistantSettingsRepository.SaveHomeAssistantSettings(settings);

    public void AddTentSensorSnapshot(TentSensorSnapshot snapshot, TimeSpan? dedupeWindow = null)
        => _tentRepository.AddTentSensorSnapshot(snapshot, dedupeWindow);

    public List<TentSensorSnapshot> GetTentSensorSnapshots(int tentId, IEnumerable<string>? metricKeys = null, int limitPerMetric = 48)
        => _tentRepository.GetTentSensorSnapshots(tentId, metricKeys, limitPerMetric);

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

    private void CaptureGrowSnapshots(GrowRun grow)
    {
        var capturedAtUtc = DateTime.UtcNow;
        var capturedAny = !string.IsNullOrWhiteSpace(grow.TentSnapshotJson)
                          || !string.IsNullOrWhiteSpace(grow.HydroSetupSnapshotJson);

        if (grow.TentId.HasValue && string.IsNullOrWhiteSpace(grow.TentSnapshotJson))
        {
            var tent = GetTent(grow.TentId.Value);
            if (tent is not null)
            {
                grow.TentSnapshotJson = JsonSerializer.Serialize(ToGrowTentSnapshot(tent), SnapshotJsonOptions);
                capturedAny = true;
            }
        }

        if (grow.SystemId.HasValue && string.IsNullOrWhiteSpace(grow.HydroSetupSnapshotJson))
        {
            var hydroSetup = GetHydroSetup(grow.SystemId.Value);
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
            WidthCm             = reader["WidthCm"] is DBNull or null ? null : Convert.ToInt32(reader["WidthCm"], CultureInfo.InvariantCulture),
            DepthCm             = reader["DepthCm"] is DBNull or null ? null : Convert.ToInt32(reader["DepthCm"], CultureInfo.InvariantCulture),
            TentHeightCm        = reader["TentHeightCm"] is DBNull or null ? null : Convert.ToInt32(reader["TentHeightCm"], CultureInfo.InvariantCulture),
            LightType           = NullString(reader["LightType"]),
            LightWatt           = reader["LightWatt"] is DBNull or null ? null : Convert.ToInt32(reader["LightWatt"], CultureInfo.InvariantCulture),
            LightController     = Enum.TryParse<LightControllerType>(NullString(reader["LightController"]), out var lc) ? lc : (LightControllerType?)null,
            LightControllerEntityId = NullString(reader["LightControllerEntityId"]),
            ExhaustFanCount     = reader["ExhaustFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustFanCount"], CultureInfo.InvariantCulture),
            ExhaustM3h          = reader["ExhaustM3h"] is DBNull or null ? null : Convert.ToInt32(reader["ExhaustM3h"], CultureInfo.InvariantCulture),
            CirculationFanCount = reader["CirculationFanCount"] is DBNull or null ? null : Convert.ToInt32(reader["CirculationFanCount"], CultureInfo.InvariantCulture),
            HvacController      = Enum.TryParse<HvacControllerType>(NullString(reader["HvacController"]), out var hc) ? hc : (HvacControllerType?)null,
            HvacControllerEntityId = NullString(reader["HvacControllerEntityId"]),
            Co2Available        = reader["Co2Available"] is not DBNull and not null && Convert.ToInt32(reader["Co2Available"], CultureInfo.InvariantCulture) == 1,
            CameraEntityId      = NullString(reader["CameraEntityId"]),
            ActiveGrowCount = reader["ActiveGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveGrowCount"], CultureInfo.InvariantCulture),
            ArchivedGrowCount = reader["ArchivedGrowCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedGrowCount"], CultureInfo.InvariantCulture),
            ActiveSetupCount = reader["ActiveSetupCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveSetupCount"], CultureInfo.InvariantCulture),
            ArchivedSetupCount = reader["ArchivedSetupCount"] is DBNull ? 0 : Convert.ToInt32(reader["ArchivedSetupCount"], CultureInfo.InvariantCulture)
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

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static double? NullableDouble(object value)
        => value is DBNull ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(raw, out var parsed) ? parsed : fallback;

    private static bool HasColumn(SqliteDataReader reader, string name)
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

    private static DateTime? ParseStoredDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    private static DateTime? ParseStoredUtcDateTime(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;

    private static DateTime? ParseStoredDateTimeIfColumn(SqliteDataReader reader, string columnName)
    {
        if (!HasColumn(reader, columnName) || reader[columnName] is DBNull)
            return null;
        var text = reader[columnName]?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
    }

    private static DateTime? ParseStoredDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result) ? result.Date : null;

    private static string ToStorage(DateTime value)
        => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static string ToStorageUtc(DateTime value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
