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

        return Ok(new GrowExportValidationDto(
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
