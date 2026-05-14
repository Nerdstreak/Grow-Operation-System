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
        var suggestedReservoir = ResolveAddbackReservoirLiters(grow);

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
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        if (!request.EcIst.HasValue)
        {
            ModelState.AddModelError(nameof(request.EcIst), "Ist-EC ist erforderlich.");
        }

        if (!request.EcZiel.HasValue)
        {
            ModelState.AddModelError(nameof(request.EcZiel), "Ziel-EC ist erforderlich.");
        }

        if (!request.EcStock.HasValue)
        {
            ModelState.AddModelError(nameof(request.EcStock), "Addback-EC ist erforderlich.");
        }

        var reservoirLiters = request.ReservoirLiters ?? ResolveAddbackReservoirLiters(grow);
        if (!reservoirLiters.HasValue)
        {
            ModelState.AddModelError(nameof(request.ReservoirLiters), "Reservoir-Volumen konnte nicht aus dem HydroSetup oder Legacy-Grow gelesen werden.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var result = AddbackCalculator.Calculate(
            reservoirLiters!.Value,
            request.EcIst!.Value,
            request.EcZiel!.Value,
            request.EcStock!.Value);

        return Ok(result.ToDto());
    }

    [HttpGet("{id:int}/addback/logs")]
    [ProducesResponseType(typeof(IReadOnlyList<AddbackLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<AddbackLogDto>> GetAddbackLogs(int id)
    {
        if (_repository.GetGrow(id) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        return Ok(_repository.GetAddbackLogsForGrow(id).Select(entry => entry.ToDto()).ToList());
    }

    [HttpPost("{id:int}/addback/logs")]
    [ProducesResponseType(typeof(AddbackLogDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<AddbackLogDto> CreateAddbackLog(int id, [FromBody] CreateAddbackLogRequest request)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        ValidateOperationLogValues(
            (request.ReservoirLiters, nameof(request.ReservoirLiters)),
            (request.EcBefore, nameof(request.EcBefore)),
            (request.EcTarget, nameof(request.EcTarget)),
            (request.EcStock, nameof(request.EcStock)),
            (request.EcAfter, nameof(request.EcAfter)),
            (request.LitersAdded, nameof(request.LitersAdded)),
            (request.NewReservoirVolumeLiters, nameof(request.NewReservoirVolumeLiters)));
        ValidatePh(request.PhBefore, nameof(request.PhBefore));
        ValidatePh(request.PhAfter, nameof(request.PhAfter));

        if (!Enum.IsDefined(request.Kind))
        {
            ModelState.AddModelError(nameof(request.Kind), "Addback-Art ist ungueltig.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var resolvedReservoir = request.ReservoirLiters ?? ResolveAddbackReservoirLiters(grow);
        var usedHydroVolume = request.UsedHydroSetupVolume
            ?? (!request.ReservoirLiters.HasValue && grow.SystemId.HasValue && CalculateHydroSetupTotalVolumeLiters(_repository.GetHydroSetup(grow.SystemId.Value)).HasValue);

        var created = _repository.CreateAddbackLog(new AddbackLogEntry
        {
            GrowId = id,
            HydroSetupId = grow.SystemId,
            Kind = request.Kind,
            PerformedAtUtc = request.PerformedAtUtc ?? DateTime.UtcNow,
            ReservoirLiters = resolvedReservoir,
            EcBefore = request.EcBefore,
            EcTarget = request.EcTarget,
            EcStock = request.EcStock,
            EcAfter = request.EcAfter,
            PhBefore = request.PhBefore,
            PhAfter = request.PhAfter,
            LitersAdded = request.LitersAdded,
            NewReservoirVolumeLiters = request.NewReservoirVolumeLiters,
            UsedHydroSetupVolume = usedHydroVolume,
            Notes = request.Notes
        });

        return CreatedAtAction(nameof(GetAddbackLogs), new { id }, created.ToDto());
    }

    [HttpGet("{id:int}/changeouts")]
    [ProducesResponseType(typeof(IReadOnlyList<ChangeoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<ChangeoutDto>> GetChangeouts(int id)
    {
        if (_repository.GetGrow(id) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        return Ok(_repository.GetChangeoutsForGrow(id).Select(entry => entry.ToDto()).ToList());
    }

    [HttpPost("{id:int}/changeouts")]
    [ProducesResponseType(typeof(ChangeoutDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<ChangeoutDto> CreateChangeout(int id, [FromBody] CreateChangeoutRequest request)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        ValidateOperationLogValues(
            (request.VolumeChangedLiters, nameof(request.VolumeChangedLiters)),
            (request.PercentChanged, nameof(request.PercentChanged)),
            (request.EcBefore, nameof(request.EcBefore)),
            (request.EcAfter, nameof(request.EcAfter)));
        ValidatePh(request.PhBefore, nameof(request.PhBefore));
        ValidatePh(request.PhAfter, nameof(request.PhAfter));

        if (request.PercentChanged is < 0 or > 100)
        {
            ModelState.AddModelError(nameof(request.PercentChanged), "Prozentwert muss zwischen 0 und 100 liegen.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            ModelState.AddModelError(nameof(request.Kind), "Changeout-Art ist ungueltig.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var created = _repository.CreateChangeout(new ChangeoutEntry
        {
            GrowId = id,
            HydroSetupId = grow.SystemId,
            Kind = request.Kind,
            PerformedAtUtc = request.PerformedAtUtc ?? DateTime.UtcNow,
            VolumeChangedLiters = request.VolumeChangedLiters,
            PercentChanged = request.PercentChanged,
            EcBefore = request.EcBefore,
            EcAfter = request.EcAfter,
            PhBefore = request.PhBefore,
            PhAfter = request.PhAfter,
            Notes = request.Notes
        });

        return CreatedAtAction(nameof(GetChangeouts), new { id }, created.ToDto());
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

    private void ValidateOperationLogValues(params (double? Value, string FieldName)[] values)
    {
        foreach (var (value, fieldName) in values)
        {
            if (value is < 0)
            {
                ModelState.AddModelError(fieldName, "Wert darf nicht negativ sein.");
            }
        }
    }

    private void ValidatePh(double? value, string fieldName)
    {
        if (value is < 0 or > 14)
        {
            ModelState.AddModelError(fieldName, "pH-Wert muss zwischen 0 und 14 liegen.");
        }
    }

    private double? ResolveAddbackReservoirLiters(GrowRun grow)
    {
        if (grow.SystemId.HasValue)
        {
            var hydroSetup = _repository.GetHydroSetup(grow.SystemId.Value);
            var totalVolume = CalculateHydroSetupTotalVolumeLiters(hydroSetup);
            if (totalVolume.HasValue)
            {
                return totalVolume;
            }
        }

        return TryParseReservoirSize(grow.ReservoirSize);
    }

    private static double? CalculateHydroSetupTotalVolumeLiters(GrowSystem? hydroSetup)
    {
        if (hydroSetup is null)
        {
            return null;
        }

        var siteVolume = (hydroSetup.PotCount ?? 0) * (hydroSetup.PotSizeLiters ?? 0);
        var reservoirVolume = hydroSetup.ReservoirLiters ?? 0;
        var total = siteVolume + reservoirVolume;

        return total > 0 ? Math.Round(total, 1) : null;
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
