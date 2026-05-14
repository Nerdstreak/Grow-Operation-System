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

        return Ok(new GrowExportDto(
            SchemaVersion: ExportSchemaVersion,
            ExportedAtUtc: DateTime.UtcNow,
            Anonymized: anonymize,
            Grow: growDto,
            TentSnapshot: tentDto,
            HydroSetupSnapshot: hydroSetupDto,
            Measurements: _repository.GetMeasurementsForGrow(id).Select(measurement => measurement.ToDto()).ToList(),
            JournalEntries: _journalRepository.GetForGrow(id).Select(entry => entry.ToDto()).ToList(),
            Tasks: _taskRepository.GetForGrow(id).Select(task => task.ToDto()).ToList(),
            HardwareItems: hardwareItems,
            Harvest: harvest,
            AddbackLogs: _repository.GetAddbackLogsForGrow(id).Select(entry => entry.ToDto()).ToList(),
            Changeouts: _repository.GetChangeoutsForGrow(id).Select(entry => entry.ToDto()).ToList(),
            Photos: photos,
            Warnings: warnings));
    }
}
