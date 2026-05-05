using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/grows")]
[Produces("application/json")]
public sealed class GrowWorkflowApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly HarvestRepository _harvestRepository;
    private readonly JournalRepository _journalRepository;
    private readonly AuditRepository _auditRepository;
    private readonly TargetValueService _targetValueService;

    public GrowWorkflowApiController(
        GrowRepository repository,
        HarvestRepository harvestRepository,
        JournalRepository journalRepository,
        AuditRepository auditRepository,
        TargetValueService targetValueService)
    {
        _repository = repository;
        _harvestRepository = harvestRepository;
        _journalRepository = journalRepository;
        _auditRepository = auditRepository;
        _targetValueService = targetValueService;
    }

    [HttpGet("{id:int}/addback")]
    [ProducesResponseType(typeof(AddbackDefaultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<AddbackDefaultsDto> AddbackDefaults(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        var measurements = _repository.GetMeasurementsForGrow(id);
        var latestByTime = measurements
            .OrderByDescending(measurement => measurement.TakenAt)
            .FirstOrDefault();
        var latestEc = measurements
            .OrderByDescending(measurement => measurement.TakenAt)
            .Where(measurement => measurement.ReservoirEc.HasValue)
            .Select(measurement => measurement.ReservoirEc)
            .FirstOrDefault();
        var stage = latestByTime?.Stage ?? GrowStage.Veg;
        var targets = _targetValueService.GetTargets(grow.HydroStyle, stage);
        double? suggestedEcTarget = targets is null
            ? null
            : Math.Round((targets.EcMin + targets.EcMax) / 2, 2);
        var suggestedReservoir = TryParseReservoirSize(grow.ReservoirSize);

        return Ok(new AddbackDefaultsDto(
            id,
            grow.Name,
            suggestedReservoir,
            latestEc,
            suggestedEcTarget,
            suggestedReservoir,
            latestEc,
            suggestedEcTarget,
            3.0));
    }

    [HttpPost("{id:int}/addback/calculate")]
    [ProducesResponseType(typeof(AddbackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<AddbackResultDto> CalculateAddback(int id, [FromBody] AddbackCalculateRequest request)
    {
        if (_repository.GetGrow(id) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var result = AddbackCalculator.Calculate(
            request.ReservoirLiters!.Value,
            request.EcIst!.Value,
            request.EcZiel!.Value,
            request.EcStock!.Value);

        return Ok(result.ToDto());
    }

    [HttpGet("{id:int}/harvest")]
    [ProducesResponseType(typeof(HarvestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<HarvestDto> Harvest(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        var harvest = _harvestRepository.GetForGrow(id);
        return Ok(harvest is null
            ? GrowWorkflowMapping.CreateDefaultHarvestDto(id, grow.Name)
            : harvest.ToDto(grow.Name));
    }

    [HttpPut("{id:int}/harvest")]
    [ProducesResponseType(typeof(HarvestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<HarvestDto> SaveHarvest(int id, [FromBody] HarvestUpsertRequest request)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        HarvestEntry entry;
        try
        {
            entry = request.ToEntry(id);
        }
        catch
        {
            ModelState.AddModelError(nameof(request.HarvestedAtLocal), "Erntedatum konnte nicht gelesen werden.");
            return ValidationError();
        }

        var existing = _harvestRepository.GetForGrow(id);
        if (existing is null)
        {
            _harvestRepository.Create(entry);
            if (grow.Status == GrowStatus.Running)
            {
                grow.Status = GrowStatus.Completed;
                grow.EndDate = entry.HarvestedAt.Date;
                _repository.UpdateGrow(grow);
            }

            _auditRepository.LogHarvestCreated(id, request.HarvestedAtLocal);
        }
        else
        {
            entry.Id = existing.Id;
            entry.CreatedAtUtc = existing.CreatedAtUtc;
            _harvestRepository.Update(entry);
        }

        return Ok(_harvestRepository.GetForGrow(id)!.ToDto(grow.Name));
    }

    [HttpPost("{id:int}/actions/confirm-germination")]
    [ProducesResponseType(typeof(GrowActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowActionResultDto> ConfirmGermination(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        if (grow.StartMaterial != StartMaterial.Seed)
        {
            return BadRequest(new ApiError("invalid_action", "Keimungsbestaetigung ist nur fuer Samen-Grows moeglich."));
        }

        if (!grow.GerminatedAt.HasValue)
        {
            grow.GerminatedAt = DateTime.Now;
            if (grow.Status == GrowStatus.Planning)
            {
                grow.Status = GrowStatus.Running;
            }

            _repository.UpdateGrow(grow);
            _journalRepository.Create(new JournalEntry
            {
                GrowId = id,
                EntryType = JournalEntryType.GerminationConfirmed,
                Body = "Keimung bestaetigt.",
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        return Ok(new GrowActionResultDto(_repository.GetGrow(id)!.ToDetailDto(), "Keimung bestaetigt."));
    }

    [HttpPost("{id:int}/actions/confirm-rooting")]
    [ProducesResponseType(typeof(GrowActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowActionResultDto> ConfirmRooting(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        if (grow.StartMaterial != StartMaterial.Clone)
        {
            return BadRequest(new ApiError("invalid_action", "Bewurzelungsbestaetigung ist nur fuer Stecklinge moeglich."));
        }

        if (!grow.RootedAt.HasValue)
        {
            grow.RootedAt = DateTime.Now;
            grow.CloneIsRooted = true;
            if (grow.Status == GrowStatus.Planning)
            {
                grow.Status = GrowStatus.Running;
            }

            _repository.UpdateGrow(grow);
            _journalRepository.Create(new JournalEntry
            {
                GrowId = id,
                EntryType = JournalEntryType.CloneRooted,
                Body = "Bewurzelung bestaetigt.",
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        return Ok(new GrowActionResultDto(_repository.GetGrow(id)!.ToDetailDto(), "Bewurzelung bestaetigt."));
    }

    [HttpPost("{id:int}/actions/flip-to-flower")]
    [ProducesResponseType(typeof(GrowActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowActionResultDto> FlipToFlower(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        if (grow.SeedType == SeedType.Autoflower)
        {
            return BadRequest(new ApiError("invalid_action", "Autoflower braucht keinen Flip."));
        }

        if (!grow.FlipDate.HasValue)
        {
            grow.FlipDate = DateTime.Today;
            _repository.UpdateGrow(grow);
            _journalRepository.Create(new JournalEntry
            {
                GrowId = id,
                EntryType = JournalEntryType.FlipToFlower,
                Body = "Auf 12/12 geflippt.",
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        return Ok(new GrowActionResultDto(_repository.GetGrow(id)!.ToDetailDto(), "Flip zu 12/12 eingetragen."));
    }

    private static double? TryParseReservoirSize(string? reservoirSize)
    {
        if (string.IsNullOrWhiteSpace(reservoirSize))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(reservoirSize, @"(\d+([.,]\d+)?)");
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(
            match.Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }
}
