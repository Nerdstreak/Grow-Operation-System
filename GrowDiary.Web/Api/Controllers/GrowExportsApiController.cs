using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/exports/grows")]
[Produces("application/json")]
public sealed class GrowExportsApiController : ApiControllerBase
{
    private const string ExportSchemaVersion = "grow-os.grow-export.v1";
    private const string ExportValidationSchemaVersion = "grow-os.grow-export.validation.v1";
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly JournalRepository _journalRepository;
    private readonly TaskRepository _taskRepository;
    private readonly HarvestRepository _harvestRepository;
    private readonly SystemAuditRepository _auditRepository;

    public GrowExportsApiController(
        AppPaths paths,
        GrowRepository repository,
        JournalRepository journalRepository,
        TaskRepository taskRepository,
        HarvestRepository harvestRepository,
        SystemAuditRepository auditRepository)
    {
        _paths = paths;
        _repository = repository;
        _journalRepository = journalRepository;
        _taskRepository = taskRepository;
        _harvestRepository = harvestRepository;
        _auditRepository = auditRepository;
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(GrowExportValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<GrowExportValidationDto> ValidateExport([FromBody] GrowExportDto? export)
    {
        if (export is null)
        {
            return BadRequestError("invalid_export", "Export konnte nicht gelesen werden.");
        }

        var validation = BuildValidation(export);
        LogExportAudit(
            action: "grow-export-validated",
            summary: validation.IsValid ? "Grow-Export erfolgreich validiert." : "Grow-Export-Validierung fehlgeschlagen.",
            success: validation.IsValid,
            relatedGrowId: export.Grow?.Id,
            relatedFileName: export.ExportId,
            severity: validation.IsValid ? "info" : "warning");
        return Ok(validation);
    }

    [HttpPost("import-plan")]
    [ProducesResponseType(typeof(GrowImportPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<GrowImportPlanDto> CreateImportPlan([FromBody] GrowExportDto? export)
    {
        if (export is null)
        {
            return BadRequestError("invalid_export", "Export konnte nicht gelesen werden.");
        }

        var plan = BuildImportPlan(export, wouldModifyDatabase: false);
        LogExportAudit(
            action: "grow-import-plan-created",
            summary: plan.Blockers.Count == 0 ? "Grow-Import-Plan erfolgreich erstellt." : "Grow-Import-Plan mit Blockern erstellt.",
            success: plan.Blockers.Count == 0,
            relatedGrowId: export.Grow?.Id,
            relatedFileName: export.ExportId,
            severity: plan.Blockers.Count == 0 ? "info" : "warning");
        return Ok(plan);
    }

    [HttpPost("import")]
    [ProducesResponseType(typeof(GrowImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public ActionResult<GrowImportResultDto> ImportGrow([FromBody] GrowExportDto? export)
    {
        if (export is null)
        {
            return BadRequestError("invalid_export", "Export konnte nicht gelesen werden.");
        }

        var plan = BuildImportPlan(export, wouldModifyDatabase: true);
        if (plan.Blockers.Count > 0 || !plan.ExportValid || !plan.ImportSupported)
        {
            LogExportAudit(
                action: "grow-import-blocked",
                summary: "Grow-Import wurde durch Preflight-Blocker verhindert.",
                success: false,
                relatedGrowId: export.Grow?.Id,
                relatedFileName: export.ExportId,
                severity: "warning");
            return BadRequestError("import_blocked", "Import wurde blockiert: " + string.Join(" ", plan.Blockers.DefaultIfEmpty("Export ist nicht import-ready.")));
        }

        var safetyBackup = CreateImportSafetyBackup();
        if (safetyBackup is null)
        {
            LogExportAudit(
                action: "grow-import-safety-backup-failed",
                summary: "Grow-Import wurde abgebrochen, weil kein Safety-Backup erstellt werden konnte.",
                success: false,
                relatedGrowId: export.Grow?.Id,
                relatedFileName: export.ExportId,
                severity: "error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiErrorFactory.ServerError("import_safety_backup_failed", "Safety-Backup konnte vor dem Import nicht erstellt werden.", HttpContext?.TraceIdentifier));
        }

        int? importedGrowId = null;
        try
        {
            var warnings = new List<string>(plan.Warnings);
            if ((export.HardwareItems?.Count ?? 0) > 0)
            {
                warnings.Add("Hardware wurde bewusst nicht als aktives Inventar importiert; die Daten bleiben im Export-Snapshot/Importkontext.");
            }
            if ((export.Photos?.Count ?? 0) > 0)
            {
                warnings.Add("Foto-Metadaten wurden bewusst nicht importiert, weil JSON-Exporte keine Bilddateien enthalten.");
            }

            var grow = ToImportedGrowRun(export);
            importedGrowId = _repository.CreateGrow(grow);

            var measurementIdMap = new Dictionary<int, int>();
            var importedMeasurements = 0;
            foreach (var measurement in export.Measurements ?? Array.Empty<MeasurementDto>())
            {
                var importedId = _repository.CreateMeasurement(ToImportedMeasurement(measurement, importedGrowId.Value));
                measurementIdMap[measurement.Id] = importedId;
                importedMeasurements++;
            }

            var importedJournalEntries = 0;
            foreach (var entry in export.JournalEntries ?? Array.Empty<JournalEntryDto>())
            {
                _journalRepository.Create(ToImportedJournalEntry(entry, importedGrowId.Value, measurementIdMap));
                importedJournalEntries++;
            }

            var importedTasks = 0;
            foreach (var task in export.Tasks ?? Array.Empty<GrowTaskDto>())
            {
                _taskRepository.Create(ToImportedHistoricalTask(task, importedGrowId.Value));
                importedTasks++;
            }

            var importedAddbackLogs = 0;
            foreach (var entry in export.AddbackLogs ?? Array.Empty<AddbackLogDto>())
            {
                _repository.CreateAddbackLog(ToImportedAddbackLog(entry, importedGrowId.Value));
                importedAddbackLogs++;
            }

            var importedChangeouts = 0;
            foreach (var entry in export.Changeouts ?? Array.Empty<ChangeoutDto>())
            {
                _repository.CreateChangeout(ToImportedChangeout(entry, importedGrowId.Value));
                importedChangeouts++;
            }

            var importedHarvestEntries = 0;
            if (export.Harvest is not null)
            {
                _harvestRepository.Create(ToImportedHarvest(export.Harvest, importedGrowId.Value));
                importedHarvestEntries = 1;
            }

            var importedGrow = _repository.GetGrow(importedGrowId.Value);
            var result = new GrowImportResultDto(
                ImportSchema: "grow-os.grow-import.v1",
                ImportedAtUtc: DateTime.UtcNow,
                Success: true,
                ExportId: export.ExportId,
                ImportedGrowId: importedGrowId.Value,
                ImportedGrowName: importedGrow?.Name ?? grow.Name,
                SafetyBackupFileName: safetyBackup.Value.FileName,
                SafetyBackupDownloadUrl: safetyBackup.Value.DownloadUrl,
                ImportedMeasurements: importedMeasurements,
                ImportedJournalEntries: importedJournalEntries,
                ImportedTasks: importedTasks,
                ImportedAddbackLogs: importedAddbackLogs,
                ImportedChangeouts: importedChangeouts,
                ImportedHarvestEntries: importedHarvestEntries,
                SkippedHardwareItems: export.HardwareItems?.Count ?? 0,
                SkippedPhotos: export.Photos?.Count ?? 0,
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

            LogExportAudit(
                action: "grow-import-executed",
                summary: $"Grow-Import erfolgreich ausgefuehrt. Neue lokale Grow-Id: {importedGrowId.Value}.",
                success: true,
                relatedGrowId: importedGrowId.Value,
                relatedFileName: export.ExportId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            if (importedGrowId.HasValue)
            {
                try { _repository.DeleteGrow(importedGrowId.Value); } catch { }
            }

            LogExportAudit(
                action: "grow-import-failed",
                summary: "Grow-Import fehlgeschlagen: " + ex.Message,
                success: false,
                relatedGrowId: importedGrowId,
                relatedFileName: export.ExportId,
                severity: "error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiErrorFactory.ServerError("import_failed", "Import ist fehlgeschlagen. Safety-Backup wurde erstellt: " + safetyBackup.Value.FileName + ". Fehler: " + ex.Message, HttpContext?.TraceIdentifier));
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GrowExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowExportDto> ExportGrow(int id, [FromQuery] bool anonymize = false, [FromQuery] bool includePhotoMetadata = true)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            LogExportAudit("grow-export-requested", $"Grow-Export fuer fehlenden Grow #{id} angefordert.", false, relatedGrowId: id, severity: "warning");
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        var warnings = new List<string>();
        var growDto = grow.ToDetailDto();
        if (anonymize)
        {
            growDto = growDto with
            {
                Name = $"Grow {grow.Id}",
                Strain = null,
                Breeder = null,
                CloneSource = null,
                Nutrients = null,
                Notes = null,
                LatestPhotoPath = null
            };
            warnings.Add("Export wurde anonymisiert: Name, Sorte, Breeder, Clone-Quelle, Nährstoffnotizen und freie Grow-Notizen wurden entfernt.");
        }

        var tentDto = TryReadTentSnapshotDto(grow.TentSnapshotJson);
        if (tentDto is null && grow.TentId.HasValue)
        {
            tentDto = _repository.GetTent(grow.TentId.Value)?.ToDto();
            if (!string.IsNullOrWhiteSpace(grow.TentSnapshotJson))
            {
                warnings.Add("Gespeicherter Zelt-Snapshot konnte nicht gelesen werden; Export nutzt aktuelle Zeltdaten als Fallback.");
            }
            else if (tentDto is not null)
            {
                warnings.Add("Legacy-Grow ohne gespeicherten Zelt-Snapshot; Export nutzt aktuelle Zeltdaten.");
            }
        }
        if (anonymize && tentDto is not null)
        {
            tentDto = tentDto with
            {
                Name = $"Zelt {tentDto.Id}",
                Notes = null,
                LightControllerEntityId = null,
                HvacControllerEntityId = null,
                CameraEntityId = null,
                Sensors = tentDto.Sensors.Select(sensor => sensor with
                {
                    HaEntityId = string.Empty,
                    DisplayLabel = null
                }).ToList()
            };
        }

        var hydroSetupDto = TryReadHydroSetupSnapshotDto(grow.HydroSetupSnapshotJson);
        if (hydroSetupDto is null && grow.SystemId.HasValue)
        {
            hydroSetupDto = _repository.GetHydroSetup(grow.SystemId.Value)?.ToDto();
            if (!string.IsNullOrWhiteSpace(grow.HydroSetupSnapshotJson))
            {
                warnings.Add("Gespeicherter HydroSetup-Snapshot konnte nicht gelesen werden; Export nutzt aktuelle HydroSetup-Daten als Fallback.");
            }
            else if (hydroSetupDto is not null)
            {
                warnings.Add("Legacy-Grow ohne gespeicherten HydroSetup-Snapshot; Export nutzt aktuelle HydroSetup-Daten.");
            }
        }
        if (anonymize && hydroSetupDto is not null)
        {
            hydroSetupDto = hydroSetupDto with
            {
                Name = $"HydroSetup {hydroSetupDto.Id}",
                CirculationPumpNotes = null,
                AirPumpNotes = null,
                Notes = null
            };
        }

        var hardwareItems = _repository.GetHardwareItems()
            .Where(item => item.GrowId == id || (grow.SystemId.HasValue && item.HydroSetupId == grow.SystemId.Value))
            .Select(item => item.ToDto())
            .ToList();
        if (anonymize)
        {
            hardwareItems = hardwareItems.Select(item => item with
            {
                Name = $"Hardware {item.Id}",
                HaEntityId = null,
                Manufacturer = null,
                Model = null,
                SerialNumber = null,
                Notes = null
            }).ToList();
        }

        var harvest = _harvestRepository.GetForGrow(id)?.ToDto(growDto.Name);
        var photos = includePhotoMetadata
            ? _repository.GetPhotosForGrow(id).Select(photo => photo.ToDto()).ToList()
            : new List<PhotoAssetDto>();
        if (anonymize && photos.Count > 0)
        {
            photos = photos.Select(photo => photo with
            {
                RelativePath = string.Empty,
                Caption = null
            }).ToList();
            warnings.Add("Fotodateipfade wurden im anonymisierten Export entfernt. Bilddateien selbst sind nicht Teil dieses JSON-Exports.");
        }

        if (!grow.SystemId.HasValue)
        {
            warnings.Add("Grow hat kein HydroSetup. Technische Systemdaten stammen aus Legacy-Grow-Feldern.");
        }

        var measurements = _repository.GetMeasurementsForGrow(id).Select(measurement => measurement.ToDto()).ToList();
        var journalEntries = _journalRepository.GetForGrow(id).Select(entry => entry.ToDto()).ToList();
        var tasks = _taskRepository.GetForGrow(id).Select(task => task.ToDto()).ToList();
        var addbackLogs = _repository.GetAddbackLogsForGrow(id).Select(entry => entry.ToDto()).ToList();
        var changeouts = _repository.GetChangeoutsForGrow(id).Select(entry => entry.ToDto()).ToList();
        var exportedAtUtc = DateTime.UtcNow;
        var sectionCounts = new GrowExportSectionCountsDto(
            Measurements: measurements.Count,
            JournalEntries: journalEntries.Count,
            Tasks: tasks.Count,
            HardwareItems: hardwareItems.Count,
            AddbackLogs: addbackLogs.Count,
            Changeouts: changeouts.Count,
            Photos: photos.Count);

        var export = new GrowExportDto(
            SchemaVersion: ExportSchemaVersion,
            ExportId: $"grow-{grow.Id}-{exportedAtUtc:yyyyMMddHHmmssfff}",
            ExportedAtUtc: exportedAtUtc,
            Anonymized: anonymize,
            IntegrityHash: string.Empty,
            SectionCounts: sectionCounts,
            Grow: growDto,
            TentSnapshot: tentDto,
            HydroSetupSnapshot: hydroSetupDto,
            Measurements: measurements,
            JournalEntries: journalEntries,
            Tasks: tasks,
            HardwareItems: hardwareItems,
            Harvest: harvest,
            AddbackLogs: addbackLogs,
            Changeouts: changeouts,
            Photos: photos,
            Warnings: warnings);

        var finalExport = export with { IntegrityHash = ComputeIntegrityHash(export) };
        LogExportAudit(
            action: "grow-export-created",
            summary: anonymize ? "Anonymisierter Grow-Export erstellt." : "Grow-Export erstellt.",
            success: true,
            relatedGrowId: id,
            relatedFileName: finalExport.ExportId);
        return Ok(finalExport);
    }

    private static TentDto? TryReadTentSnapshotDto(string? snapshotJson)
    {
        var snapshot = TryDeserializeSnapshot<GrowTentSnapshot>(snapshotJson);
        if (snapshot is null)
        {
            return null;
        }

        return new TentDto(
            Id: snapshot.Id,
            Name: snapshot.Name,
            Kind: snapshot.Kind,
            TentType: snapshot.TentType.ToString(),
            Status: snapshot.Status.ToString(),
            Notes: snapshot.Notes,
            DisplayOrder: snapshot.DisplayOrder,
            AccentColor: snapshot.AccentColor,
            WidthCm: snapshot.WidthCm,
            DepthCm: snapshot.DepthCm,
            TentHeightCm: snapshot.TentHeightCm,
            LightType: snapshot.LightType,
            LightWatt: snapshot.LightWatt,
            LightController: snapshot.LightController?.ToString(),
            LightControllerEntityId: snapshot.LightControllerEntityId,
            ExhaustFanCount: snapshot.ExhaustFanCount,
            ExhaustM3h: snapshot.ExhaustM3h,
            CirculationFanCount: snapshot.CirculationFanCount,
            HvacController: snapshot.HvacController?.ToString(),
            HvacControllerEntityId: snapshot.HvacControllerEntityId,
            Co2Available: snapshot.Co2Available,
            CameraEntityId: snapshot.CameraEntityId,
            ActiveGrowCount: 0,
            ArchivedGrowCount: 0,
            ActiveSetupCount: 0,
            ArchivedSetupCount: 0,
            Sensors: (snapshot.Sensors ?? Array.Empty<GrowTentSensorSnapshot>()).Select(sensor => new TentSensorDto(
                Id: sensor.Id,
                TentId: snapshot.Id,
                MetricType: sensor.MetricType.ToString(),
                HaEntityId: sensor.HaEntityId,
                DisplayLabel: sensor.DisplayLabel,
                IsActive: sensor.IsActive)).ToList());
    }

    private static HydroSetupDto? TryReadHydroSetupSnapshotDto(string? snapshotJson)
    {
        var snapshot = TryDeserializeSnapshot<GrowHydroSetupSnapshot>(snapshotJson);
        if (snapshot is null)
        {
            return null;
        }

        return new HydroSetupDto(
            Id: snapshot.Id,
            Name: snapshot.Name,
            TentId: snapshot.TentId,
            TentName: snapshot.TentName,
            HydroStyle: Enum.TryParse<HydroStyle>(snapshot.HydroStyle, out var hydroStyle) ? hydroStyle : HydroStyle.None,
            PotCount: snapshot.PotCount,
            PotSizeLiters: snapshot.PotSizeLiters,
            ReservoirLiters: snapshot.ReservoirLiters,
            TotalVolumeLiters: snapshot.TotalVolumeLiters,
            LayoutType: snapshot.LayoutType,
            ReservoirPosition: snapshot.ReservoirPosition,
            Status: snapshot.Status,
            HasCirculationPump: snapshot.HasCirculationPump,
            CirculationPumpNotes: snapshot.CirculationPumpNotes,
            HasAirPump: snapshot.HasAirPump,
            AirPumpNotes: snapshot.AirPumpNotes,
            AirStoneCount: snapshot.AirStoneCount,
            HasChiller: snapshot.HasChiller,
            HasUvSterilizer: snapshot.HasUvSterilizer,
            Notes: snapshot.Notes,
            DisplayOrder: snapshot.DisplayOrder,
            ActiveGrowCount: 0,
            CreatedAtUtc: snapshot.CreatedAtUtc,
            UpdatedAtUtc: snapshot.UpdatedAtUtc);
    }

    private static T? TryDeserializeSnapshot<T>(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(snapshotJson, ExportJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private GrowImportPlanDto BuildImportPlan(GrowExportDto export, bool wouldModifyDatabase)
    {
        var validation = BuildValidation(export);
        var blockers = new List<string>();
        var warnings = new List<string>(validation.Warnings);
        var conflicts = new List<GrowImportPlanConflictDto>();
        var plannedItems = new List<GrowImportPlanItemDto>();

        if (!validation.IsValid)
        {
            blockers.AddRange(validation.Errors);
        }
        if (export.Grow is null)
        {
            blockers.Add("Export enthaelt keinen Grow-Datensatz.");
        }

        var source = new GrowImportPlanSourceDto(
            OriginalGrowId: export.Grow?.Id,
            GrowName: export.Grow?.Name,
            TentName: export.TentSnapshot?.Name,
            HydroSetupName: export.HydroSetupSnapshot?.Name,
            ExportedAtUtc: export.ExportedAtUtc);

        if (validation.IsValid && export.Grow is not null)
        {
            var existingGrows = _repository.GetAllGrows();
            var sameNameAndStart = existingGrows.FirstOrDefault(grow =>
                string.Equals(grow.Name, export.Grow.Name, StringComparison.OrdinalIgnoreCase)
                && grow.StartDate.Date == export.Grow.StartDate.Date);
            if (sameNameAndStart is not null)
            {
                conflicts.Add(new GrowImportPlanConflictDto(
                    Kind: "possible-duplicate-grow",
                    Severity: "warning",
                    Message: $"Ein lokaler Grow mit gleichem Namen und Startdatum existiert bereits (Id {sameNameAndStart.Id}). Der Import legt trotzdem eine neue lokale Grow-Id an."));
            }

            if (_repository.GetGrow(export.Grow.Id) is not null)
            {
                conflicts.Add(new GrowImportPlanConflictDto(
                    Kind: "source-id-conflict",
                    Severity: "info",
                    Message: "Die Original-Grow-Id existiert lokal bereits. Der Import vergibt immer eine neue lokale Id."));
            }

            if (export.Anonymized)
            {
                warnings.Add("Der Export ist anonymisiert. Importierte Vergleichsdaten enthalten keine vollständigen Nutzer-/Strain-/Geräteangaben.");
            }

            warnings.Add(wouldModifyDatabase
                ? "Import legt einen neuen lokalen Grow an und überschreibt keine bestehenden Grows, Zelte oder HydroSetups."
                : "Import-Plan ist ein Dry-Run. Es werden keine Daten geschrieben.");

            plannedItems.Add(new GrowImportPlanItemDto("grow", "create-new-local-grow", 1, "Grow wird mit neuer lokaler ID importiert."));
            plannedItems.Add(new GrowImportPlanItemDto("tent-snapshot", "store-on-imported-grow", export.TentSnapshot is null ? 0 : 1, "Zelt-Snapshot wird am importierten Grow gespeichert und erzeugt kein produktives Zelt."));
            plannedItems.Add(new GrowImportPlanItemDto("hydro-setup-snapshot", "store-on-imported-grow", export.HydroSetupSnapshot is null ? 0 : 1, "HydroSetup-Snapshot wird am importierten Grow gespeichert und erzeugt kein produktives HydroSetup."));
            plannedItems.Add(new GrowImportPlanItemDto("measurements", "import-for-new-grow", export.Measurements?.Count ?? 0, null));
            plannedItems.Add(new GrowImportPlanItemDto("journal", "import-for-new-grow", export.JournalEntries?.Count ?? 0, "Messungsreferenzen werden auf neue lokale Measurement-Ids gemappt, falls vorhanden."));
            plannedItems.Add(new GrowImportPlanItemDto("tasks", "import-as-history", export.Tasks?.Count ?? 0, "Tasks werden historisch importiert und nicht als aktive Reminder geöffnet."));
            plannedItems.Add(new GrowImportPlanItemDto("hardware", "skip-active-inventory", export.HardwareItems?.Count ?? 0, "Hardware wird nicht als aktives lokales Inventar angelegt."));
            plannedItems.Add(new GrowImportPlanItemDto("harvest", "import-for-new-grow", export.Harvest is null ? 0 : 1, null));
            plannedItems.Add(new GrowImportPlanItemDto("addback-logs", "import-for-new-grow", export.AddbackLogs?.Count ?? 0, "HydroSetupId wird gelöst, weil kein lokales Live-HydroSetup angelegt wird."));
            plannedItems.Add(new GrowImportPlanItemDto("changeouts", "import-for-new-grow", export.Changeouts?.Count ?? 0, "HydroSetupId wird gelöst, weil kein lokales Live-HydroSetup angelegt wird."));
            plannedItems.Add(new GrowImportPlanItemDto("photos", "skip-metadata-only", export.Photos?.Count ?? 0, "JSON-Export enthält nur Foto-Metadaten, keine Bilddateien."));
        }

        return new GrowImportPlanDto(
            ImportPlanSchema: "grow-os.grow-import-plan.v1",
            CheckedAtUtc: DateTime.UtcNow,
            ExportValid: validation.IsValid,
            ImportSupported: blockers.Count == 0,
            WouldModifyDatabase: wouldModifyDatabase && blockers.Count == 0,
            IsAnonymized: export.Anonymized,
            ExportId: export.ExportId,
            ExportSchemaVersion: export.SchemaVersion,
            IntegrityHash: export.IntegrityHash,
            Source: source,
            SectionCounts: validation.ActualSectionCounts,
            PlannedItems: plannedItems,
            Conflicts: conflicts,
            Blockers: blockers,
            Warnings: warnings);
    }

    private GrowRun ToImportedGrowRun(GrowExportDto export)
    {
        var source = export.Grow ?? throw new InvalidOperationException("Export enthaelt keinen Grow-Datensatz.");
        var importedName = string.IsNullOrWhiteSpace(source.Name)
            ? $"Import {export.ExportId}"
            : source.Name.Trim() + " (Import)";

        return new GrowRun
        {
            TentId = null,
            SystemId = null,
            SetupId = null,
            Name = importedName,
            Strain = source.Strain,
            Breeder = source.Breeder,
            Status = source.Status,
            MediumType = source.MediumType,
            FeedingStyle = source.FeedingStyle,
            HydroStyle = source.HydroStyle,
            IrrigationType = source.IrrigationType,
            WaterSource = source.WaterSource,
            Environment = source.Environment,
            Light = source.Light,
            ContainerSize = source.ContainerSize,
            ReservoirSize = source.ReservoirSize,
            MediumDetail = source.MediumDetail,
            IrrigationStyle = source.IrrigationStyle,
            HasChiller = source.HasChiller,
            SeedType = source.SeedType,
            StartMaterial = source.StartMaterial,
            GerminationMethod = source.GerminationMethod,
            PropagationMedium = source.PropagationMedium,
            CloneSource = source.CloneSource,
            CloneIsRooted = source.CloneIsRooted,
            BreederFlowerWeeksMin = source.BreederFlowerWeeksMin,
            BreederFlowerWeeksMax = source.BreederFlowerWeeksMax,
            PlantCount = source.PlantCount,
            PhenoNumber = source.PhenoNumber,
            EntryPoint = source.EntryPoint,
            DaysAlreadyInPhase = source.DaysAlreadyInPhase,
            AutoflowerDaysSinceGermination = source.AutoflowerDaysSinceGermination,
            StartDate = source.StartDate.Date,
            EndDate = source.EndDate?.Date,
            FlipDate = source.FlipDate?.Date,
            GerminatedAt = source.GerminatedAt,
            RootedAt = source.RootedAt,
            Nutrients = source.Nutrients,
            Notes = AppendImportNote(source.Notes, export),
            TentSnapshotJson = export.TentSnapshot is null ? null : JsonSerializer.Serialize(ToTentSnapshot(export.TentSnapshot), ExportJsonOptions),
            HydroSetupSnapshotJson = export.HydroSetupSnapshot is null ? null : JsonSerializer.Serialize(ToHydroSetupSnapshot(export.HydroSetupSnapshot), ExportJsonOptions),
            SnapshotsCapturedAtUtc = DateTime.UtcNow
        };
    }

    private static string? AppendImportNote(string? existingNotes, GrowExportDto export)
    {
        var marker = $"Importiert aus Grow-Export {export.ExportId} vom {export.ExportedAtUtc:O}.";
        return string.IsNullOrWhiteSpace(existingNotes) ? marker : existingNotes.Trim() + Environment.NewLine + marker;
    }

    private static Measurement ToImportedMeasurement(MeasurementDto dto, int growId)
        => new()
        {
            GrowId = growId,
            TakenAt = dto.TakenAt,
            Stage = dto.Stage,
            Source = ValueOrigin.Imported,
            Notes = dto.Notes,
            AirTemperatureC = dto.AirTemperatureC,
            HumidityPercent = dto.HumidityPercent,
            HeightCm = dto.HeightCm,
            WaterAmountMl = dto.WaterAmountMl,
            RunoffAmountMl = dto.RunoffAmountMl,
            IrrigationPh = dto.IrrigationPh,
            IrrigationEc = dto.IrrigationEc,
            DrainPh = dto.DrainPh,
            DrainEc = dto.DrainEc,
            ReservoirPh = dto.ReservoirPh,
            ReservoirEc = dto.ReservoirEc,
            ReservoirWaterTempC = dto.ReservoirWaterTempC,
            ReservoirLevelCm = dto.ReservoirLevelCm,
            ReservoirLevelLiters = dto.ReservoirLevelLiters,
            DissolvedOxygenMgL = dto.DissolvedOxygenMgL,
            OrpMv = dto.OrpMv,
            TopOffLiters = dto.TopOffLiters,
            AddbackEc = dto.AddbackEc,
            SolutionChange = dto.SolutionChange,
            PpfdMol = dto.PpfdMol,
            Co2Ppm = dto.Co2Ppm
        };

    private static JournalEntry ToImportedJournalEntry(JournalEntryDto dto, int growId, IReadOnlyDictionary<int, int> measurementIdMap)
        => new()
        {
            GrowId = growId,
            MeasurementId = dto.MeasurementId.HasValue && measurementIdMap.TryGetValue(dto.MeasurementId.Value, out var mappedId) ? mappedId : null,
            Title = dto.Title,
            Body = dto.Body,
            EntryType = dto.EntryType,
            Source = ValueOrigin.Imported,
            OccurredAtUtc = dto.OccurredAtUtc
        };

    private static GrowTask ToImportedHistoricalTask(GrowTaskDto dto, int growId)
        => new()
        {
            GrowId = growId,
            Title = dto.Title,
            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? $"Import-Historie. Ursprünglicher Status: {dto.Status}."
                : dto.Notes.Trim() + Environment.NewLine + $"Import-Historie. Ursprünglicher Status: {dto.Status}.",
            DueAtUtc = dto.DueAtUtc,
            Priority = dto.Priority,
            Status = GrowTaskStatus.Done,
            CompletedAtUtc = dto.CompletedAtUtc ?? DateTime.UtcNow
        };

    private static AddbackLogEntry ToImportedAddbackLog(AddbackLogDto dto, int growId)
        => new()
        {
            GrowId = growId,
            HydroSetupId = null,
            Kind = dto.Kind,
            PerformedAtUtc = dto.PerformedAtUtc,
            ReservoirLiters = dto.ReservoirLiters,
            EcBefore = dto.EcBefore,
            EcTarget = dto.EcTarget,
            EcStock = dto.EcStock,
            EcAfter = dto.EcAfter,
            PhBefore = dto.PhBefore,
            PhAfter = dto.PhAfter,
            LitersAdded = dto.LitersAdded,
            NewReservoirVolumeLiters = dto.NewReservoirVolumeLiters,
            UsedHydroSetupVolume = dto.UsedHydroSetupVolume,
            Notes = dto.Notes
        };

    private static ChangeoutEntry ToImportedChangeout(ChangeoutDto dto, int growId)
        => new()
        {
            GrowId = growId,
            HydroSetupId = null,
            Kind = dto.Kind,
            PerformedAtUtc = dto.PerformedAtUtc,
            VolumeChangedLiters = dto.VolumeChangedLiters,
            PercentChanged = dto.PercentChanged,
            EcBefore = dto.EcBefore,
            EcAfter = dto.EcAfter,
            PhBefore = dto.PhBefore,
            PhAfter = dto.PhAfter,
            Notes = dto.Notes
        };

    private static HarvestEntry ToImportedHarvest(HarvestDto dto, int growId)
        => new()
        {
            GrowId = growId,
            HarvestedAt = DateTime.TryParse(dto.HarvestedAtLocal, out var harvestedAt) ? harvestedAt.Date : DateTime.Today,
            WetWeightG = dto.WetWeightG,
            DryWeightG = dto.DryWeightG,
            DryDays = dto.DryDays,
            YieldNotes = dto.YieldNotes,
            Rating = dto.Rating,
            FlavorNotes = dto.FlavorNotes,
            EffectNotes = dto.EffectNotes,
            NugStructure = dto.NugStructure
        };

    private static GrowTentSnapshot ToTentSnapshot(TentDto dto)
        => new(
            Id: dto.Id,
            Name: dto.Name,
            Kind: dto.Kind,
            TentType: Enum.TryParse<TentType>(dto.TentType, out var tentType) ? tentType : TentType.MultiPurpose,
            Status: Enum.TryParse<TentStatus>(dto.Status, out var status) ? status : TentStatus.Active,
            Notes: dto.Notes,
            DisplayOrder: dto.DisplayOrder,
            AccentColor: dto.AccentColor,
            WidthCm: dto.WidthCm,
            DepthCm: dto.DepthCm,
            TentHeightCm: dto.TentHeightCm,
            LightType: dto.LightType,
            LightWatt: dto.LightWatt,
            LightController: Enum.TryParse<LightControllerType>(dto.LightController, out var lightController) ? (LightControllerType?)lightController : null,
            LightControllerEntityId: dto.LightControllerEntityId,
            ExhaustFanCount: dto.ExhaustFanCount,
            ExhaustM3h: dto.ExhaustM3h,
            CirculationFanCount: dto.CirculationFanCount,
            HvacController: Enum.TryParse<HvacControllerType>(dto.HvacController, out var hvacController) ? (HvacControllerType?)hvacController : null,
            HvacControllerEntityId: dto.HvacControllerEntityId,
            Co2Available: dto.Co2Available,
            CameraEntityId: dto.CameraEntityId,
            Sensors: (dto.Sensors ?? Array.Empty<TentSensorDto>()).Select(sensor => new GrowTentSensorSnapshot(
                Id: sensor.Id,
                MetricType: Enum.TryParse<SensorMetricType>(sensor.MetricType, out var metricType) ? metricType : SensorMetricType.AirTemperature,
                HaEntityId: sensor.HaEntityId,
                DisplayLabel: sensor.DisplayLabel,
                IsActive: sensor.IsActive)).ToList());

    private static GrowHydroSetupSnapshot ToHydroSetupSnapshot(HydroSetupDto dto)
        => new(
            Id: dto.Id,
            TentId: dto.TentId,
            TentName: dto.TentName,
            Name: dto.Name,
            HydroStyle: dto.HydroStyle.ToString(),
            PotCount: dto.PotCount,
            PotSizeLiters: dto.PotSizeLiters,
            ReservoirLiters: dto.ReservoirLiters,
            TotalVolumeLiters: dto.TotalVolumeLiters,
            Status: dto.Status,
            LayoutType: dto.LayoutType,
            ReservoirPosition: dto.ReservoirPosition,
            HasCirculationPump: dto.HasCirculationPump,
            CirculationPumpNotes: dto.CirculationPumpNotes,
            HasAirPump: dto.HasAirPump,
            AirPumpNotes: dto.AirPumpNotes,
            AirStoneCount: dto.AirStoneCount,
            HasChiller: dto.HasChiller,
            HasUvSterilizer: dto.HasUvSterilizer,
            Notes: dto.Notes,
            DisplayOrder: dto.DisplayOrder,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc);

    private ImportSafetyBackup? CreateImportSafetyBackup()
    {
        try
        {
            var backupRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "backups");
            Directory.CreateDirectory(backupRoot);
            var fileName = CreateUniqueImportSafetyBackupFileName(backupRoot);
            var backupPath = Path.Combine(backupRoot, fileName);

            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                AddBackupEntryIfExists(archive, _paths.DatabasePath, "App_Data/grow-diary.db");
                AddBackupEntryIfExists(archive, _paths.DatabasePath + "-wal", "App_Data/grow-diary.db-wal");
                AddBackupEntryIfExists(archive, _paths.DatabasePath + "-shm", "App_Data/grow-diary.db-shm");
            }

            return new ImportSafetyBackup(fileName, $"/api/system/backup/{Uri.EscapeDataString(fileName)}");
        }
        catch
        {
            return null;
        }
    }

    private static string CreateUniqueImportSafetyBackupFileName(string backupRoot)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = attempt == 0 ? string.Empty : "-" + attempt.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"grow-os-backup-import-safety-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}{suffix}.zip";
            if (!System.IO.File.Exists(Path.Combine(backupRoot, fileName)))
            {
                return fileName;
            }
        }

        return "grow-os-backup-import-safety-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8] + ".zip";
    }

    private static void AddBackupEntryIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!System.IO.File.Exists(sourcePath))
        {
            return;
        }

        var entry = archive.CreateEntry(entryName);
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destination = entry.Open();
        source.CopyTo(destination);
    }

    private readonly record struct ImportSafetyBackup(string FileName, string DownloadUrl);

    private void LogExportAudit(string action, string summary, bool success, int? relatedGrowId = null, string? relatedFileName = null, string severity = "info")
    {
        try
        {
            _auditRepository.Add(new SystemAuditEvent
            {
                EventType = "export",
                Action = action,
                Summary = summary,
                Severity = severity,
                Source = "grow-export-api",
                RelatedGrowId = relatedGrowId,
                RelatedFileName = relatedFileName,
                Success = success
            });
        }
        catch
        {
            // Audit logging must never break export/import-plan endpoints.
        }
    }

    private static GrowExportValidationDto BuildValidation(GrowExportDto export)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!string.Equals(export.SchemaVersion, ExportSchemaVersion, StringComparison.Ordinal))
        {
            errors.Add($"Nicht unterstuetzte Export-Schema-Version: {export.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(export.ExportId))
        {
            errors.Add("ExportId fehlt.");
        }

        var actualCounts = CountSections(export);
        var sectionCountsValid = export.SectionCounts is not null && SectionCountsEqual(export.SectionCounts, actualCounts);
        if (!sectionCountsValid)
        {
            errors.Add("SectionCounts stimmen nicht mit dem Export-Inhalt ueberein.");
        }

        var expectedHash = ComputeIntegrityHash(export);
        var integrityHashValid = !string.IsNullOrWhiteSpace(export.IntegrityHash)
            && string.Equals(export.IntegrityHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        if (!integrityHashValid)
        {
            errors.Add("IntegrityHash ist ungueltig oder fehlt.");
        }

        var containsPotentialSecrets = ContainsPotentialSecrets(export);
        if (containsPotentialSecrets)
        {
            errors.Add("Export enthaelt potenzielle Secrets und darf nicht importiert werden.");
        }

        if (export.HydroSetupSnapshot is null)
        {
            warnings.Add("Export enthaelt keinen HydroSetup-Snapshot. Vergleichbarkeit kann fuer Legacy-Grows eingeschraenkt sein.");
        }

        return new GrowExportValidationDto(
            ValidationSchema: ExportValidationSchemaVersion,
            CheckedAtUtc: DateTime.UtcNow,
            ExportSchemaVersion: export.SchemaVersion,
            ExportId: export.ExportId,
            IsValid: errors.Count == 0,
            IntegrityHashValid: integrityHashValid,
            SectionCountsValid: sectionCountsValid,
            ContainsPotentialSecrets: containsPotentialSecrets,
            DeclaredSectionCounts: export.SectionCounts,
            ActualSectionCounts: actualCounts,
            Errors: errors,
            Warnings: warnings);
    }

    private static GrowExportSectionCountsDto CountSections(GrowExportDto export)
        => new(
            Measurements: export.Measurements?.Count ?? 0,
            JournalEntries: export.JournalEntries?.Count ?? 0,
            Tasks: export.Tasks?.Count ?? 0,
            HardwareItems: export.HardwareItems?.Count ?? 0,
            AddbackLogs: export.AddbackLogs?.Count ?? 0,
            Changeouts: export.Changeouts?.Count ?? 0,
            Photos: export.Photos?.Count ?? 0);

    private static bool SectionCountsEqual(GrowExportSectionCountsDto left, GrowExportSectionCountsDto right)
        => left.Measurements == right.Measurements
           && left.JournalEntries == right.JournalEntries
           && left.Tasks == right.Tasks
           && left.HardwareItems == right.HardwareItems
           && left.AddbackLogs == right.AddbackLogs
           && left.Changeouts == right.Changeouts
           && left.Photos == right.Photos;

    private static string ComputeIntegrityHash(GrowExportDto export)
    {
        var canonical = export with { IntegrityHash = string.Empty };
        var json = JsonSerializer.Serialize(canonical, ExportJsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ContainsPotentialSecrets(GrowExportDto export)
    {
        var json = JsonSerializer.Serialize(export, ExportJsonOptions);
        var forbiddenTerms = new[]
        {
            "ha-config",
            "access_token",
            "refresh_token",
            "bearer ",
            "dataProtectionKeys",
            "secret-token",
            "api-token"
        };

        return forbiddenTerms.Any(term => json.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
