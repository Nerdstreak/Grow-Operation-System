using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
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

    private readonly GrowRepository _repository;
    private readonly JournalRepository _journalRepository;
    private readonly TaskRepository _taskRepository;
    private readonly HarvestRepository _harvestRepository;

    public GrowExportsApiController(
        GrowRepository repository,
        JournalRepository journalRepository,
        TaskRepository taskRepository,
        HarvestRepository harvestRepository)
    {
        _repository = repository;
        _journalRepository = journalRepository;
        _taskRepository = taskRepository;
        _harvestRepository = harvestRepository;
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(GrowExportValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<GrowExportValidationDto> ValidateExport([FromBody] GrowExportDto? export)
    {
        if (export is null)
        {
            return BadRequest(new ApiError("invalid_export", "Export konnte nicht gelesen werden."));
        }

        return Ok(BuildValidation(export));
    }

    [HttpPost("import-plan")]
    [ProducesResponseType(typeof(GrowImportPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<GrowImportPlanDto> CreateImportPlan([FromBody] GrowExportDto? export)
    {
        if (export is null)
        {
            return BadRequest(new ApiError("invalid_export", "Export konnte nicht gelesen werden."));
        }

        var validation = BuildValidation(export);
        var blockers = new List<string>();
        var warnings = new List<string>(validation.Warnings);
        var conflicts = new List<GrowImportPlanConflictDto>();
        var plannedItems = new List<GrowImportPlanItemDto>();

        if (!validation.IsValid)
        {
            blockers.AddRange(validation.Errors);
        }

        var source = new GrowImportPlanSourceDto(
            OriginalGrowId: export.Grow?.Id,
            GrowName: export.Grow?.Name,
            TentName: export.TentSnapshot?.Name,
            HydroSetupName: export.HydroSetupSnapshot?.Name,
            ExportedAtUtc: export.ExportedAtUtc);

        if (validation.IsValid)
        {
            var existingGrows = _repository.GetAllGrows();
            var sameNameAndStart = existingGrows.FirstOrDefault(grow =>
                export.Grow is not null
                && string.Equals(grow.Name, export.Grow.Name, StringComparison.OrdinalIgnoreCase)
                && grow.StartDate.Date == export.Grow.StartDate.Date);
            if (sameNameAndStart is not null)
            {
                conflicts.Add(new GrowImportPlanConflictDto(
                    Kind: "possible-duplicate-grow",
                    Severity: "warning",
                    Message: $"Ein lokaler Grow mit gleichem Namen und Startdatum existiert bereits (Id {sameNameAndStart.Id})."));
            }

            if (export.Grow is not null && _repository.GetGrow(export.Grow.Id) is not null)
            {
                conflicts.Add(new GrowImportPlanConflictDto(
                    Kind: "source-id-conflict",
                    Severity: "info",
                    Message: "Die Original-Grow-Id existiert lokal bereits. Ein späterer Import muss neue lokale IDs vergeben."));
            }

            if (export.Anonymized)
            {
                warnings.Add("Der Export ist anonymisiert. Importierte Vergleichsdaten enthalten keine vollständigen Nutzer-/Strain-/Geräteangaben.");
            }

            warnings.Add("Import-Plan ist read-only. Es werden keine Daten geschrieben und ExportId-Deduplikation wird noch nicht persistiert.");

            plannedItems.Add(new GrowImportPlanItemDto("grow", "create-new-local-grow", export.Grow is null ? 0 : 1, "Grow wuerde mit neuer lokaler ID importiert."));
            plannedItems.Add(new GrowImportPlanItemDto("tent-snapshot", "store-as-snapshot", export.TentSnapshot is null ? 0 : 1, "Zelt-Snapshot dient der Vergleichbarkeit und erzeugt aktuell kein produktives Zelt."));
            plannedItems.Add(new GrowImportPlanItemDto("hydro-setup-snapshot", "store-as-snapshot", export.HydroSetupSnapshot is null ? 0 : 1, "HydroSetup-Snapshot dient der Vergleichbarkeit und erzeugt aktuell kein produktives HydroSetup."));
            plannedItems.Add(new GrowImportPlanItemDto("measurements", "import-for-grow", export.Measurements?.Count ?? 0, null));
            plannedItems.Add(new GrowImportPlanItemDto("journal", "import-for-grow", export.JournalEntries?.Count ?? 0, null));
            plannedItems.Add(new GrowImportPlanItemDto("tasks", "import-as-history", export.Tasks?.Count ?? 0, "Tasks wuerden als historische Eintraege behandelt, nicht als aktive Reminder."));
            plannedItems.Add(new GrowImportPlanItemDto("hardware", "store-as-snapshot", export.HardwareItems?.Count ?? 0, "Hardware wird fuer Vergleichbarkeit geplant, nicht als aktives lokales Inventar."));
            plannedItems.Add(new GrowImportPlanItemDto("harvest", "import-for-grow", export.Harvest is null ? 0 : 1, null));
            plannedItems.Add(new GrowImportPlanItemDto("addback-logs", "import-for-grow", export.AddbackLogs?.Count ?? 0, null));
            plannedItems.Add(new GrowImportPlanItemDto("changeouts", "import-for-grow", export.Changeouts?.Count ?? 0, null));
            plannedItems.Add(new GrowImportPlanItemDto("photos", "metadata-only", export.Photos?.Count ?? 0, "JSON-Export enthaelt nur Foto-Metadaten, keine Bilddateien."));
        }

        return Ok(new GrowImportPlanDto(
            ImportPlanSchema: "grow-os.grow-import-plan.v1",
            CheckedAtUtc: DateTime.UtcNow,
            ExportValid: validation.IsValid,
            ImportSupported: false,
            WouldModifyDatabase: false,
            IsAnonymized: export.Anonymized,
            ExportId: export.ExportId,
            ExportSchemaVersion: export.SchemaVersion,
            IntegrityHash: export.IntegrityHash,
            Source: source,
            SectionCounts: validation.ActualSectionCounts,
            PlannedItems: plannedItems,
            Conflicts: conflicts,
            Blockers: blockers,
            Warnings: warnings));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GrowExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowExportDto> ExportGrow(int id, [FromQuery] bool anonymize = false, [FromQuery] bool includePhotoMetadata = true)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
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

        var tentDto = grow.TentId.HasValue ? _repository.GetTent(grow.TentId.Value)?.ToDto() : null;
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

        var hydroSetupDto = grow.SystemId.HasValue ? _repository.GetHydroSetup(grow.SystemId.Value)?.ToDto() : null;
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

        return Ok(export with { IntegrityHash = ComputeIntegrityHash(export) });
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
