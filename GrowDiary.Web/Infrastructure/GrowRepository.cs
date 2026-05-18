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

    public GrowRepository(AppPaths paths)
        : this(paths, new TentRepository(paths))
    {
    }

    private GrowRepository(AppPaths paths, TentRepository tentRepository)
        : this(paths, tentRepository, new HydroSetupRepository(paths, tentRepository), new AddbackRepository(paths), new HardwareRepository(paths), new SetupRepository(paths), new AutoMeasurementRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository)
        : this(paths, tentRepository, hydroSetupRepository, new AddbackRepository(paths), new HardwareRepository(paths), new SetupRepository(paths), new AutoMeasurementRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, new HardwareRepository(paths), new SetupRepository(paths), new AutoMeasurementRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, hardwareRepository, new SetupRepository(paths), new AutoMeasurementRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository, SetupRepository setupRepository)
        : this(paths, tentRepository, hydroSetupRepository, addbackRepository, hardwareRepository, setupRepository, new AutoMeasurementRepository(paths))
    {
    }

    public GrowRepository(AppPaths paths, TentRepository tentRepository, HydroSetupRepository hydroSetupRepository, AddbackRepository addbackRepository, HardwareRepository hardwareRepository, SetupRepository setupRepository, AutoMeasurementRepository autoMeasurementRepository)
    {
        _paths = paths;
        _tentRepository = tentRepository;
        _hydroSetupRepository = hydroSetupRepository;
        _addbackRepository = addbackRepository;
        _hardwareRepository = hardwareRepository;
        _setupRepository = setupRepository;
        _autoMeasurementRepository = autoMeasurementRepository;
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
    {
        schedule.CreatedAtUtc = DateTime.UtcNow;
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LightSchedules (
                TentId, Name, IsActive, LightsOnTime, LightsOffTime, TimeZoneId, Source,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $tentId, $name, $isActive, $lightsOnTime, $lightsOffTime, $timeZoneId, $source,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddLightScheduleParameters(command, schedule);
        schedule.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return schedule;
    }

    public void UpdateLightSchedule(LightSchedule schedule)
    {
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE LightSchedules SET
                TentId = $tentId,
                Name = $name,
                IsActive = $isActive,
                LightsOnTime = $lightsOnTime,
                LightsOffTime = $lightsOffTime,
                TimeZoneId = $timeZoneId,
                Source = $source,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddLightScheduleParameters(command, schedule);
        command.Parameters.AddWithValue("$id", schedule.Id);
        command.ExecuteNonQuery();
    }

    public LightSchedule? GetLightSchedule(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM LightSchedules WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightSchedule(reader) : null;
    }

    public List<LightSchedule> GetLightSchedulesByTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightSchedules
            WHERE TentId = $tentId
            ORDER BY IsActive DESC, Name, Id;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var schedules = new List<LightSchedule>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            schedules.Add(MapLightSchedule(reader));
        }
        return schedules;
    }

    public LightSchedule? GetActiveLightScheduleForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightSchedules
            WHERE TentId = $tentId AND IsActive = 1
            ORDER BY UpdatedAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightSchedule(reader) : null;
    }

    public LightTransitionEvent CreateLightTransitionIfNotDuplicate(LightTransitionEvent transition)
    {
        transition.OccurredAtUtc = transition.OccurredAtUtc.ToUniversalTime();
        transition.CreatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = """
                SELECT *
                FROM LightTransitionEvents
                WHERE TentId = $tentId
                  AND Kind = $kind
                  AND OccurredAtUtc >= $fromUtc
                  AND OccurredAtUtc <= $toUtc
                ORDER BY OccurredAtUtc DESC, Id DESC
                LIMIT 1;
            """;
            duplicateCommand.Parameters.AddWithValue("$tentId", transition.TentId);
            duplicateCommand.Parameters.AddWithValue("$kind", transition.Kind.ToString());
            duplicateCommand.Parameters.AddWithValue("$fromUtc", ToStorageUtc(transition.OccurredAtUtc.AddMinutes(-2)));
            duplicateCommand.Parameters.AddWithValue("$toUtc", ToStorageUtc(transition.OccurredAtUtc.AddMinutes(2)));
            using var reader = duplicateCommand.ExecuteReader();
            if (reader.Read())
            {
                var existing = MapLightTransitionEvent(reader);
                transaction.Commit();
                return existing;
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO LightTransitionEvents (
                    TentId, Kind, OccurredAtUtc, Source, RawState, CreatedAtUtc
                )
                VALUES (
                    $tentId, $kind, $occurredAtUtc, $source, $rawState, $createdAtUtc
                );
                SELECT last_insert_rowid();
            """;
            AddLightTransitionParameters(insertCommand, transition);
            transition.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);
        }

        transaction.Commit();
        return transition;
    }

    public List<LightTransitionEvent> GetLightTransitionsByTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
            ORDER BY OccurredAtUtc DESC, Id DESC;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var transitions = new List<LightTransitionEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transitions.Add(MapLightTransitionEvent(reader));
        }
        return transitions;
    }

    public List<LightTransitionEvent> GetLightTransitionsByTentAndKindSince(int tentId, LightTransitionKind kind, DateTime sinceUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
              AND Kind = $kind
              AND OccurredAtUtc >= $sinceUtc
            ORDER BY OccurredAtUtc ASC, Id ASC;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$sinceUtc", ToStorageUtc(sinceUtc));

        var transitions = new List<LightTransitionEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transitions.Add(MapLightTransitionEvent(reader));
        }
        return transitions;
    }

    public LightTransitionEvent? GetLatestLightTransitionForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
            ORDER BY OccurredAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightTransitionEvent(reader) : null;
    }

    public LightTransitionEvent? GetLatestLightTransitionForTentAndKind(int tentId, LightTransitionKind kind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM LightTransitionEvents
            WHERE TentId = $tentId
              AND Kind = $kind
            ORDER BY OccurredAtUtc DESC, Id DESC
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);
        command.Parameters.AddWithValue("$kind", kind.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapLightTransitionEvent(reader) : null;
    }

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
    {
        var now = DateTime.UtcNow;

        // Scheduling: berechne Fälligkeiten aus SOP-Typ
        var isRecurring = string.Equals(sopDefinition.Type, "Recurring", StringComparison.OrdinalIgnoreCase);
        // E4-Fix: bevorzugt Schedule-Trigger, Root-Level nur als Fallback.
        var recurrenceIntervalDays = sopDefinition.Triggers
            .FirstOrDefault(t => string.Equals(t.Type, "Schedule", StringComparison.OrdinalIgnoreCase))
            ?.IntervalDays ?? sopDefinition.IntervalDays;
        DateTime? instanceDueAt = string.Equals(sopDefinition.Type, "MultiDay", StringComparison.OrdinalIgnoreCase) && sopDefinition.DurationDays.HasValue
            ? now.AddDays(sopDefinition.DurationDays.Value)
            : isRecurring && recurrenceIntervalDays.HasValue
                ? now.AddDays(recurrenceIntervalDays.Value)
                : null;

        // Erster Pass: NextStepDueAtUtc aus Step-Definitionen berechnen
        // Regel: Step mit waitMinutes → DueAtUtc = startTime + waitMinutes
        //        Erster Step ohne waitMinutes → DueAtUtc = startTime; weitere → null
        var orderedStepDefs = sopDefinition.Steps.OrderBy(s => s.Order).ToList();
        DateTime? nextStepDue = null;
        for (var i = 0; i < orderedStepDefs.Count; i++)
        {
            DateTime? stepDue = orderedStepDefs[i].WaitMinutes.HasValue
                ? now.AddMinutes(orderedStepDefs[i].WaitMinutes!.Value)
                : i == 0 ? now : null;
            if (stepDue.HasValue && (nextStepDue is null || stepDue.Value < nextStepDue.Value))
                nextStepDue = stepDue;
        }

        var instance = new SopInstance
        {
            GrowId = growId,
            SopId = sopDefinition.Id,
            SopName = sopDefinition.Name,
            SopType = sopDefinition.Type,
            Status = SopInstanceStatus.Active,
            Source = source,
            SourceRecommendationKey = NormalizeOptional(sourceRecommendationKey),
            TreatmentRecommendationStableKey = NormalizeOptional(treatmentRecommendationStableKey),
            StartedAtUtc = now,
            DueAtUtc = instanceDueAt,
            NextStepDueAtUtc = nextStepDue,
            IsRecurring = isRecurring,
            RecurrenceIntervalDays = recurrenceIntervalDays,
            Notes = NormalizeOptional(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = """
                SELECT COUNT(*)
                FROM SopInstances
                WHERE GrowId = $growId
                  AND SopId = $sopId
                  AND Status = $status;
            """;
            duplicateCommand.Parameters.AddWithValue("$growId", growId);
            duplicateCommand.Parameters.AddWithValue("$sopId", sopDefinition.Id);
            duplicateCommand.Parameters.AddWithValue("$status", SopInstanceStatus.Active.ToString());
            if (Convert.ToInt32(duplicateCommand.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
            {
                throw new InvalidOperationException("An active SOP instance already exists for this grow and sopId.");
            }
        }

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO SopInstances (
                    GrowId, SopId, SopName, SopType, Status, Source, SourceRecommendationKey,
                    TreatmentRecommendationStableKey, StartedAtUtc, CompletedAtUtc, CancelledAtUtc,
                    DueAtUtc, NextStepDueAtUtc, RecurrenceIntervalDays, IsRecurring,
                    Notes, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $growId, $sopId, $sopName, $sopType, $status, $source, $sourceRecommendationKey,
                    $treatmentRecommendationStableKey, $startedAtUtc, $completedAtUtc, $cancelledAtUtc,
                    $dueAtUtc, $nextStepDueAtUtc, $recurrenceIntervalDays, $isRecurring,
                    $notes, $createdAtUtc, $updatedAtUtc
                );
                SELECT last_insert_rowid();
            """;
            AddSopInstanceParameters(insertCommand, instance);
            instance.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);
        }

        for (var idx = 0; idx < orderedStepDefs.Count; idx++)
        {
            var stepDefinition = orderedStepDefs[idx];

            DateTime? stepDueAt;
            DateTime? stepAvailableAt;
            if (stepDefinition.WaitMinutes.HasValue)
            {
                stepDueAt = now.AddMinutes(stepDefinition.WaitMinutes.Value);
                stepAvailableAt = stepDueAt;
            }
            else
            {
                stepDueAt = idx == 0 ? now : null;
                stepAvailableAt = null;
            }

            var step = new SopStepInstance
            {
                SopInstanceId = instance.Id,
                StepId = stepDefinition.Id,
                Order = stepDefinition.Order,
                Title = stepDefinition.Title,
                Description = NormalizeOptional(stepDefinition.Description),
                StepType = stepDefinition.StepType,
                Status = SopStepInstanceStatus.Pending,
                WaitMinutes = stepDefinition.WaitMinutes,
                SubSopId = NormalizeOptional(stepDefinition.SubSopId),
                ExpectedInputsJson = stepDefinition.ExpectedInputs is { Count: > 0 }
                    ? JsonSerializer.Serialize(stepDefinition.ExpectedInputs)
                    : null,
                PhotoRequired = stepDefinition.PhotoRequired,
                PhotoRecommended = stepDefinition.PhotoRecommended,
                DueAtUtc = stepDueAt,
                AvailableAtUtc = stepAvailableAt,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            using var stepCommand = connection.CreateCommand();
            stepCommand.Transaction = transaction;
            stepCommand.CommandText = """
                INSERT INTO SopStepInstances (
                    SopInstanceId, StepId, "Order", Title, Description, StepType, Status,
                    WaitMinutes, SubSopId, ExpectedInputsJson, PhotoRequired, PhotoRecommended,
                    DueAtUtc, AvailableAtUtc, ReminderTaskId,
                    StartedAtUtc, CompletedAtUtc, SkippedAtUtc, Notes, MeasurementId, JournalEntryId,
                    PhotoAssetId, CreatedAtUtc, UpdatedAtUtc
                )
                VALUES (
                    $sopInstanceId, $stepId, $order, $title, $description, $stepType, $status,
                    $waitMinutes, $subSopId, $expectedInputsJson, $photoRequired, $photoRecommended,
                    $dueAtUtc, $availableAtUtc, $reminderTaskId,
                    $startedAtUtc, $completedAtUtc, $skippedAtUtc, $notes, $measurementId, $journalEntryId,
                    $photoAssetId, $createdAtUtc, $updatedAtUtc
                );
            """;
            AddSopStepInstanceParameters(stepCommand, step);
            stepCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        instance.StepCount = sopDefinition.Steps.Count;
        return instance;
    }

    public SopInstance? GetSopInstance(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT si.*, COUNT(ssi.Id) AS StepCount
            FROM SopInstances si
            LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
            WHERE si.Id = $id
            GROUP BY si.Id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSopInstance(reader) : null;
    }

    public List<SopInstance> GetSopInstancesByGrow(int growId)
        => GetSopInstancesByGrow(growId, activeOnly: false);

    public List<SopInstance> GetActiveSopInstancesByGrow(int growId)
        => GetSopInstancesByGrow(growId, activeOnly: true);

    public List<SopStepInstance> GetSopStepInstances(int sopInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM SopStepInstances
            WHERE SopInstanceId = $sopInstanceId
            ORDER BY "Order", Id;
        """;
        command.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);

        var steps = new List<SopStepInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            steps.Add(MapSopStepInstance(reader));
        }

        return steps;
    }

    public SopStepInstance? GetSopStepInstance(int stepInstanceId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT *
            FROM SopStepInstances
            WHERE Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", stepInstanceId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSopStepInstance(reader) : null;
    }

    public SopStepInstance UpdateSopStepInstance(
        int stepInstanceId,
        SopStepInstanceStatus status,
        string? notes,
        int? measurementId,
        int? journalEntryId,
        int? photoAssetId)
    {
        var now = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        SopStepInstance step;
        SopInstance instance;
        using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
                SELECT ssi.*
                FROM SopStepInstances ssi
                WHERE ssi.Id = $id
                LIMIT 1;
            """;
            selectCommand.Parameters.AddWithValue("$id", stepInstanceId);
            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"SOP step instance with id {stepInstanceId} does not exist.");
            }

            step = MapSopStepInstance(reader);
        }

        using (var instanceCommand = connection.CreateCommand())
        {
            instanceCommand.Transaction = transaction;
            instanceCommand.CommandText = """
                SELECT si.*, COUNT(ssi.Id) AS StepCount
                FROM SopInstances si
                LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
                WHERE si.Id = $id
                GROUP BY si.Id
                LIMIT 1;
            """;
            instanceCommand.Parameters.AddWithValue("$id", step.SopInstanceId);
            using var reader = instanceCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"SOP instance with id {step.SopInstanceId} does not exist.");
            }

            instance = MapSopInstance(reader);
        }

        if (instance.Status != SopInstanceStatus.Active)
        {
            throw new InvalidOperationException("SOP instance is not active.");
        }

        step.Status = status;
        step.Notes = NormalizeOptional(notes);
        step.MeasurementId = measurementId;
        step.JournalEntryId = journalEntryId;
        step.PhotoAssetId = photoAssetId;
        step.UpdatedAtUtc = now;

        switch (status)
        {
            case SopStepInstanceStatus.Pending:
                step.StartedAtUtc = null;
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.InProgress:
                step.StartedAtUtc ??= now;
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.Done:
                step.StartedAtUtc ??= now;
                step.CompletedAtUtc = now;
                step.SkippedAtUtc = null;
                break;
            case SopStepInstanceStatus.Skipped:
                step.CompletedAtUtc = null;
                step.SkippedAtUtc = now;
                break;
        }

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE SopStepInstances
                SET Status = $status,
                    StartedAtUtc = $startedAtUtc,
                    CompletedAtUtc = $completedAtUtc,
                    SkippedAtUtc = $skippedAtUtc,
                    Notes = $notes,
                    MeasurementId = $measurementId,
                    JournalEntryId = $journalEntryId,
                    PhotoAssetId = $photoAssetId,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id;
            """;
            updateCommand.Parameters.AddWithValue("$id", step.Id);
            updateCommand.Parameters.AddWithValue("$status", step.Status.ToString());
            updateCommand.Parameters.AddWithValue("$startedAtUtc", step.StartedAtUtc.HasValue ? ToStorageUtc(step.StartedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$completedAtUtc", step.CompletedAtUtc.HasValue ? ToStorageUtc(step.CompletedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$skippedAtUtc", step.SkippedAtUtc.HasValue ? ToStorageUtc(step.SkippedAtUtc.Value) : DBNull.Value);
            updateCommand.Parameters.AddWithValue("$notes", (object?)step.Notes ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$measurementId", (object?)step.MeasurementId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$journalEntryId", (object?)step.JournalEntryId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$photoAssetId", (object?)step.PhotoAssetId ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(step.UpdatedAtUtc));
            updateCommand.ExecuteNonQuery();
        }

        RecalculateSopInstanceStatus(connection, transaction, step.SopInstanceId, now);
        transaction.Commit();

        return GetSopStepInstance(step.Id)!;
    }

    public void RecalculateSopInstanceStatus(int sopInstanceId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        RecalculateSopInstanceStatus(connection, transaction, sopInstanceId, DateTime.UtcNow);
        transaction.Commit();
    }

    public void UpdateSopStepReminderTaskId(int stepId, int taskId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE SopStepInstances
            SET ReminderTaskId = $taskId,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        command.Parameters.AddWithValue("$id", stepId);
        command.Parameters.AddWithValue("$taskId", taskId);
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
        command.ExecuteNonQuery();
    }

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

    private List<SopInstance> GetSopInstancesByGrow(int growId, bool activeOnly)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var statusFilter = activeOnly ? "AND si.Status = $status" : string.Empty;
        command.CommandText = $"""
            SELECT si.*, COUNT(ssi.Id) AS StepCount
            FROM SopInstances si
            LEFT JOIN SopStepInstances ssi ON ssi.SopInstanceId = si.Id
            WHERE si.GrowId = $growId {statusFilter}
            GROUP BY si.Id
            ORDER BY si.StartedAtUtc DESC, si.Id DESC;
        """;
        command.Parameters.AddWithValue("$growId", growId);
        if (activeOnly)
        {
            command.Parameters.AddWithValue("$status", SopInstanceStatus.Active.ToString());
        }

        var instances = new List<SopInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            instances.Add(MapSopInstance(reader));
        }
        return instances;
    }

    private static void RecalculateSopInstanceStatus(SqliteConnection connection, SqliteTransaction transaction, int sopInstanceId, DateTime now)
    {
        int totalSteps;
        int openSteps;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = """
                SELECT
                    COUNT(*) AS TotalSteps,
                    COALESCE(SUM(CASE WHEN Status IN ('Done', 'Skipped') THEN 0 ELSE 1 END), 0) AS OpenSteps
                FROM SopStepInstances
                WHERE SopInstanceId = $sopInstanceId;
            """;
            countCommand.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);
            using var reader = countCommand.ExecuteReader();
            if (!reader.Read())
                return;
            totalSteps = Convert.ToInt32(reader["TotalSteps"], CultureInfo.InvariantCulture);
            openSteps = Convert.ToInt32(reader["OpenSteps"], CultureInfo.InvariantCulture);
        }

        if (totalSteps == 0)
            return;

        if (openSteps == 0)
        {
            // Alle Steps erledigt: SOP abschliessen, NextStepDueAtUtc auf null setzen
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE SopInstances
                SET Status = $status,
                    CompletedAtUtc = $completedAtUtc,
                    NextStepDueAtUtc = NULL,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id
                  AND Status = $activeStatus;
            """;
            updateCommand.Parameters.AddWithValue("$id", sopInstanceId);
            updateCommand.Parameters.AddWithValue("$status", SopInstanceStatus.Completed.ToString());
            updateCommand.Parameters.AddWithValue("$activeStatus", SopInstanceStatus.Active.ToString());
            updateCommand.Parameters.AddWithValue("$completedAtUtc", ToStorageUtc(now));
            updateCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
            updateCommand.ExecuteNonQuery();
        }
        else
        {
            // Offene Steps vorhanden: NextStepDueAtUtc aus frühester Fälligkeit berechnen
            string? minDueRaw;
            using (var dueCommand = connection.CreateCommand())
            {
                dueCommand.Transaction = transaction;
                dueCommand.CommandText = """
                    SELECT MIN(COALESCE(DueAtUtc, AvailableAtUtc)) AS MinDue
                    FROM SopStepInstances
                    WHERE SopInstanceId = $sopInstanceId
                      AND Status NOT IN ('Done', 'Skipped');
                """;
                dueCommand.Parameters.AddWithValue("$sopInstanceId", sopInstanceId);
                var raw = dueCommand.ExecuteScalar();
                minDueRaw = raw is DBNull or null ? null : raw.ToString();
            }

            using var updateNextCommand = connection.CreateCommand();
            updateNextCommand.Transaction = transaction;
            updateNextCommand.CommandText = """
                UPDATE SopInstances
                SET NextStepDueAtUtc = $nextStepDueAtUtc,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id;
            """;
            updateNextCommand.Parameters.AddWithValue("$id", sopInstanceId);
            updateNextCommand.Parameters.AddWithValue("$nextStepDueAtUtc", minDueRaw is not null ? (object)minDueRaw : DBNull.Value);
            updateNextCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(now));
            updateNextCommand.ExecuteNonQuery();
        }
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

    private static LightSchedule MapLightSchedule(SqliteDataReader reader)
    {
        return new LightSchedule
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            IsActive = reader["IsActive"] is not DBNull and not null && Convert.ToInt32(reader["IsActive"], CultureInfo.InvariantCulture) == 1,
            LightsOnTime = reader["LightsOnTime"]?.ToString() ?? string.Empty,
            LightsOffTime = reader["LightsOffTime"]?.ToString() ?? string.Empty,
            TimeZoneId = NullString(reader["TimeZoneId"]),
            Source = ParseEnum(reader["Source"]?.ToString(), LightSource.Manual),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static LightTransitionEvent MapLightTransitionEvent(SqliteDataReader reader)
    {
        return new LightTransitionEvent
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Kind = ParseEnum(reader["Kind"]?.ToString(), LightTransitionKind.LightOn),
            OccurredAtUtc = ParseStoredDateTime(reader["OccurredAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            Source = ParseEnum(reader["Source"]?.ToString(), LightSource.HomeAssistant),
            RawState = NullString(reader["RawState"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static SopInstance MapSopInstance(SqliteDataReader reader)
    {
        return new SopInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            SopId = reader["SopId"]?.ToString() ?? string.Empty,
            SopName = reader["SopName"]?.ToString() ?? string.Empty,
            SopType = reader["SopType"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), SopInstanceStatus.Active),
            Source = ParseEnum(reader["Source"]?.ToString(), SopStartSource.Manual),
            SourceRecommendationKey = NullString(reader["SourceRecommendationKey"]),
            TreatmentRecommendationStableKey = NullString(reader["TreatmentRecommendationStableKey"]),
            StartedAtUtc = ParseStoredDateTime(reader["StartedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            CompletedAtUtc = ParseStoredDateTime(reader["CompletedAtUtc"]?.ToString()),
            CancelledAtUtc = ParseStoredDateTime(reader["CancelledAtUtc"]?.ToString()),
            DueAtUtc = ParseStoredDateTimeIfColumn(reader, "DueAtUtc"),
            NextStepDueAtUtc = ParseStoredDateTimeIfColumn(reader, "NextStepDueAtUtc"),
            RecurrenceIntervalDays = HasColumn(reader, "RecurrenceIntervalDays") && reader["RecurrenceIntervalDays"] is not DBNull
                ? Convert.ToInt32(reader["RecurrenceIntervalDays"], CultureInfo.InvariantCulture)
                : null,
            IsRecurring = HasColumn(reader, "IsRecurring") && reader["IsRecurring"] is not DBNull
                && Convert.ToInt32(reader["IsRecurring"], CultureInfo.InvariantCulture) == 1,
            Notes = NullString(reader["Notes"]),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            StepCount = HasColumn(reader, "StepCount") && reader["StepCount"] is not DBNull
                ? Convert.ToInt32(reader["StepCount"], CultureInfo.InvariantCulture)
                : 0
        };
    }

    private static SopStepInstance MapSopStepInstance(SqliteDataReader reader)
    {
        return new SopStepInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            SopInstanceId = Convert.ToInt32(reader["SopInstanceId"], CultureInfo.InvariantCulture),
            StepId = reader["StepId"]?.ToString() ?? string.Empty,
            Order = Convert.ToInt32(reader["Order"], CultureInfo.InvariantCulture),
            Title = reader["Title"]?.ToString() ?? string.Empty,
            Description = NullString(reader["Description"]),
            StepType = reader["StepType"]?.ToString() ?? string.Empty,
            Status = ParseEnum(reader["Status"]?.ToString(), SopStepInstanceStatus.Pending),
            WaitMinutes = reader["WaitMinutes"] is DBNull or null ? null : Convert.ToInt32(reader["WaitMinutes"], CultureInfo.InvariantCulture),
            SubSopId = NullString(reader["SubSopId"]),
            ExpectedInputsJson = NullString(reader["ExpectedInputsJson"]),
            PhotoRequired = reader["PhotoRequired"] is not DBNull and not null && Convert.ToInt32(reader["PhotoRequired"], CultureInfo.InvariantCulture) == 1,
            PhotoRecommended = reader["PhotoRecommended"] is not DBNull and not null && Convert.ToInt32(reader["PhotoRecommended"], CultureInfo.InvariantCulture) == 1,
            DueAtUtc = ParseStoredDateTimeIfColumn(reader, "DueAtUtc"),
            AvailableAtUtc = ParseStoredDateTimeIfColumn(reader, "AvailableAtUtc"),
            ReminderTaskId = HasColumn(reader, "ReminderTaskId") && reader["ReminderTaskId"] is not DBNull
                ? Convert.ToInt32(reader["ReminderTaskId"], CultureInfo.InvariantCulture)
                : null,
            StartedAtUtc = ParseStoredDateTime(reader["StartedAtUtc"]?.ToString()),
            CompletedAtUtc = ParseStoredDateTime(reader["CompletedAtUtc"]?.ToString()),
            SkippedAtUtc = ParseStoredDateTime(reader["SkippedAtUtc"]?.ToString()),
            Notes = NullString(reader["Notes"]),
            MeasurementId = reader["MeasurementId"] is DBNull or null ? null : Convert.ToInt32(reader["MeasurementId"], CultureInfo.InvariantCulture),
            JournalEntryId = reader["JournalEntryId"] is DBNull or null ? null : Convert.ToInt32(reader["JournalEntryId"], CultureInfo.InvariantCulture),
            PhotoAssetId = reader["PhotoAssetId"] is DBNull or null ? null : Convert.ToInt32(reader["PhotoAssetId"], CultureInfo.InvariantCulture),
            CreatedAtUtc = ParseStoredDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
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

    private static void AddLightScheduleParameters(SqliteCommand command, LightSchedule schedule)
    {
        command.Parameters.AddWithValue("$tentId", schedule.TentId);
        command.Parameters.AddWithValue("$name", schedule.Name);
        command.Parameters.AddWithValue("$isActive", schedule.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$lightsOnTime", schedule.LightsOnTime);
        command.Parameters.AddWithValue("$lightsOffTime", schedule.LightsOffTime);
        command.Parameters.AddWithValue("$timeZoneId", (object?)schedule.TimeZoneId ?? DBNull.Value);
        command.Parameters.AddWithValue("$source", schedule.Source.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(schedule.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(schedule.UpdatedAtUtc));
    }

    private static void AddLightTransitionParameters(SqliteCommand command, LightTransitionEvent transition)
    {
        command.Parameters.AddWithValue("$tentId", transition.TentId);
        command.Parameters.AddWithValue("$kind", transition.Kind.ToString());
        command.Parameters.AddWithValue("$occurredAtUtc", ToStorageUtc(transition.OccurredAtUtc));
        command.Parameters.AddWithValue("$source", transition.Source.ToString());
        command.Parameters.AddWithValue("$rawState", (object?)transition.RawState ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(transition.CreatedAtUtc));
    }

    private static void AddSopInstanceParameters(SqliteCommand command, SopInstance instance)
    {
        command.Parameters.AddWithValue("$growId", instance.GrowId);
        command.Parameters.AddWithValue("$sopId", instance.SopId);
        command.Parameters.AddWithValue("$sopName", instance.SopName);
        command.Parameters.AddWithValue("$sopType", instance.SopType);
        command.Parameters.AddWithValue("$status", instance.Status.ToString());
        command.Parameters.AddWithValue("$source", instance.Source.ToString());
        command.Parameters.AddWithValue("$sourceRecommendationKey", (object?)instance.SourceRecommendationKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$treatmentRecommendationStableKey", (object?)instance.TreatmentRecommendationStableKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", ToStorageUtc(instance.StartedAtUtc));
        command.Parameters.AddWithValue("$completedAtUtc", instance.CompletedAtUtc.HasValue ? ToStorageUtc(instance.CompletedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$cancelledAtUtc", instance.CancelledAtUtc.HasValue ? ToStorageUtc(instance.CancelledAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$dueAtUtc", instance.DueAtUtc.HasValue ? ToStorageUtc(instance.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$nextStepDueAtUtc", instance.NextStepDueAtUtc.HasValue ? ToStorageUtc(instance.NextStepDueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$recurrenceIntervalDays", (object?)instance.RecurrenceIntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$isRecurring", instance.IsRecurring ? 1 : 0);
        command.Parameters.AddWithValue("$notes", (object?)instance.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(instance.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(instance.UpdatedAtUtc));
    }

    private static void AddSopStepInstanceParameters(SqliteCommand command, SopStepInstance step)
    {
        command.Parameters.AddWithValue("$sopInstanceId", step.SopInstanceId);
        command.Parameters.AddWithValue("$stepId", step.StepId);
        command.Parameters.AddWithValue("$order", step.Order);
        command.Parameters.AddWithValue("$title", step.Title);
        command.Parameters.AddWithValue("$description", (object?)step.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$stepType", step.StepType);
        command.Parameters.AddWithValue("$status", step.Status.ToString());
        command.Parameters.AddWithValue("$waitMinutes", (object?)step.WaitMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$subSopId", (object?)step.SubSopId ?? DBNull.Value);
        command.Parameters.AddWithValue("$expectedInputsJson", (object?)step.ExpectedInputsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$photoRequired", step.PhotoRequired ? 1 : 0);
        command.Parameters.AddWithValue("$photoRecommended", step.PhotoRecommended ? 1 : 0);
        command.Parameters.AddWithValue("$dueAtUtc", step.DueAtUtc.HasValue ? ToStorageUtc(step.DueAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$availableAtUtc", step.AvailableAtUtc.HasValue ? ToStorageUtc(step.AvailableAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$reminderTaskId", (object?)step.ReminderTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAtUtc", step.StartedAtUtc.HasValue ? ToStorageUtc(step.StartedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$completedAtUtc", step.CompletedAtUtc.HasValue ? ToStorageUtc(step.CompletedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$skippedAtUtc", step.SkippedAtUtc.HasValue ? ToStorageUtc(step.SkippedAtUtc.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)step.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$measurementId", (object?)step.MeasurementId ?? DBNull.Value);
        command.Parameters.AddWithValue("$journalEntryId", (object?)step.JournalEntryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$photoAssetId", (object?)step.PhotoAssetId ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(step.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(step.UpdatedAtUtc));
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
