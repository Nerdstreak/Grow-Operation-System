using System.Globalization;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Grows-API fuer React-freundliche JSON-Endpunkte.
/// </summary>
[ApiController]
[Route("api/grows")]
[Produces("application/json")]
public sealed class GrowsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly AuditRepository _auditRepository;
    private readonly WeekCounterService _weekCounter;
    private readonly DeviationAnalyzerService _deviationAnalyzer;
    private readonly TreatmentRecommender _treatmentRecommender;

    public GrowsApiController(
        GrowRepository repository,
        AuditRepository auditRepository,
        WeekCounterService weekCounter,
        DeviationAnalyzerService deviationAnalyzer,
        TreatmentRecommender treatmentRecommender)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _weekCounter = weekCounter;
        _deviationAnalyzer = deviationAnalyzer;
        _treatmentRecommender = treatmentRecommender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GrowSummaryDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<GrowSummaryDto>> List(
        [FromQuery] bool archived = false,
        [FromQuery] string? search = null)
    {
        var grows = archived
            ? _repository.GetArchivedGrows(search)
            : _repository.GetActiveGrows(search);

        return Ok(grows.Select(grow => grow.ToSummaryDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GrowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowDetailDto> Detail(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        return Ok(grow.ToDetailDto());
    }

    [HttpGet("{growId:int}/deviations")]
    [ProducesResponseType(typeof(IReadOnlyList<GrowDeviation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<GrowDeviation>> Deviations(int growId)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        var measurements = _repository.GetMeasurementsForGrow(growId);
        return Ok(_deviationAnalyzer.Analyze(grow, measurements).ToList());
    }

    [HttpGet("{growId:int}/treatment-recommendations")]
    [ProducesResponseType(typeof(GrowTreatmentRecommendationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowTreatmentRecommendationDto> TreatmentRecommendations(int growId)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        var measurements = _repository.GetMeasurementsForGrow(growId);
        var deviations = _deviationAnalyzer.Analyze(grow, measurements);
        return Ok(_treatmentRecommender.Recommend(grow, deviations));
    }

    [HttpPost]
    [ProducesResponseType(typeof(GrowDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<GrowDetailDto> Create([FromBody] GrowUpsertRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        GrowRun grow;
        try
        {
            grow = request.ToFormModel().ToGrow();
        }
        catch
        {
            ModelState.AddModelError(nameof(request.StartDate), "Startdatum konnte nicht gelesen werden.");
            return ValidationError();
        }

        if (!ValidateHydroStyle(grow.HydroStyle))
        {
            return ValidationError();
        }

        if (!ValidateHydroSetupAssignment(grow, nameof(request.SystemId), requireHydroSetup: true))
        {
            return ValidationError();
        }

        if (!ValidateSetupAssignment(grow, nameof(request.SetupId)))
        {
            return ValidationError();
        }

        var growId = _repository.CreateGrow(grow);

        var savedGrow = _repository.GetGrow(growId)!;
        var weekInfo = _weekCounter.Calculate(savedGrow);
        if (savedGrow.Status == GrowStatus.Planning &&
            weekInfo.State != GrowCounterState.WaitingForGermination &&
            weekInfo.State != GrowCounterState.WaitingForRooting &&
            weekInfo.State != GrowCounterState.NoData)
        {
            savedGrow.Status = GrowStatus.Running;
            _repository.UpdateGrow(savedGrow);
        }

        _auditRepository.Add(new AuditEntry
        {
            GrowId = growId,
            EntityType = "Grow",
            Action = "Grow angelegt",
            Summary = $"Grow '{request.Name}' wurde erstellt{(request.TemplateId.HasValue ? $" auf Basis des Templates #{request.TemplateId}" : string.Empty)}."
        });

        return CreatedAtAction(nameof(Detail), new { id = growId }, _repository.GetGrow(growId)!.ToDetailDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(GrowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<GrowDetailDto> Update(int id, [FromBody] GrowUpsertRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var existing = _repository.GetGrow(id);
        if (existing is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        GrowRun grow;
        try
        {
            grow = request.ToFormModel().ToGrow();
        }
        catch
        {
            ModelState.AddModelError(nameof(request.StartDate), "Startdatum oder Flip-Datum konnten nicht gelesen werden.");
            return ValidationError();
        }

        if (!ValidateHydroStyle(grow.HydroStyle))
        {
            return ValidationError();
        }

        if (!grow.SystemId.HasValue && existing.SystemId.HasValue)
        {
            grow.SystemId = existing.SystemId;
        }

        if (!ValidateHydroSetupAssignment(grow, nameof(request.SystemId), requireHydroSetup: !IsLegacyGrowWithoutHydroSetup(existing)))
        {
            return ValidationError();
        }

        if (!ValidateSetupAssignment(grow, nameof(request.SetupId)))
        {
            return ValidationError();
        }

        grow.Id = id;
        grow.CreatedAtUtc = existing.CreatedAtUtc;
        _repository.UpdateGrow(grow);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = id,
            EntityType = "Grow",
            EntityId = id,
            Action = "Grow geaendert",
            Summary = $"Grow '{grow.Name}' aktualisiert. Status: {grow.Status}, SystemId: {(grow.SystemId.HasValue ? grow.SystemId.Value.ToString(CultureInfo.InvariantCulture) : "Legacy")}."
        });

        return Ok(_repository.GetGrow(id)!.ToDetailDto());
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        var existing = _repository.GetGrow(id);
        if (existing is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {id} existiert nicht.");
        }

        _repository.DeleteGrow(id);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = id,
            EntityType = "Grow",
            EntityId = id,
            Action = "Grow geloescht",
            Summary = $"Grow '{existing.Name}' wurde geloescht."
        });

        return NoContent();
    }

    private bool ValidateHydroSetupAssignment(GrowRun grow, string fieldName, bool requireHydroSetup)
    {
        if (!grow.SystemId.HasValue)
        {
            if (requireHydroSetup)
            {
                ModelState.AddModelError(fieldName, "Neue Grows brauchen ein DWC/RDWC-Hydro-Setup.");
                return false;
            }

            return true;
        }

        var hydroSetup = _repository.GetHydroSetup(grow.SystemId.Value);
        if (hydroSetup is null)
        {
            ModelState.AddModelError(fieldName, $"Hydro-Setup mit Id {grow.SystemId.Value} existiert nicht.");
            return false;
        }

        if (hydroSetup.Status == HydroSetupStatus.Archived)
        {
            ModelState.AddModelError(fieldName, "Archivierte Hydro-Setups koennen keinem neuen oder aktiven Grow zugeordnet werden.");
            return false;
        }

        if (!hydroSetup.TentId.HasValue)
        {
            ModelState.AddModelError(fieldName, "Das Hydro-Setup ist keinem Zelt zugeordnet.");
            return false;
        }

        if (grow.TentId.HasValue && grow.TentId.Value != hydroSetup.TentId.Value)
        {
            ModelState.AddModelError(fieldName, "Das Hydro-Setup gehoert zu einem anderen Zelt als der Grow.");
            return false;
        }

        if (!Enum.TryParse<HydroStyle>(hydroSetup.HydroStyle, out var hydroStyle) || hydroStyle is not (HydroStyle.DWC or HydroStyle.RDWC))
        {
            ModelState.AddModelError(fieldName, "Das Hydro-Setup muss DWC oder RDWC sein.");
            return false;
        }

        grow.TentId = hydroSetup.TentId;
        grow.HydroStyle = hydroStyle;
        grow.MediumType = MediumType.Hydro;
        grow.FeedingStyle = FeedingStyle.None;
        grow.IrrigationType = IrrigationType.ActiveHydro;
        grow.MediumDetail = hydroStyle.ToString();
        grow.HasChiller = hydroSetup.HasChiller;
        grow.ContainerSize = FormatPotSize(hydroSetup);
        grow.ReservoirSize = FormatReservoirSize(hydroSetup);

        return true;
    }

    private bool ValidateSetupAssignment(GrowRun grow, string fieldName)
    {
        if (!grow.SetupId.HasValue)
        {
            return true;
        }

        var setup = _repository.GetSetup(grow.SetupId.Value);
        if (setup is null)
        {
            ModelState.AddModelError(fieldName, $"Setup mit Id {grow.SetupId.Value} existiert nicht.");
            return false;
        }

        if (setup.SetupType != SetupType.Production)
        {
            ModelState.AddModelError(fieldName, $"Setup-Typ {setup.SetupType} kann keinem GrowRun zugeordnet werden. Erlaubt ist nur Production.");
            return false;
        }

        var setupTent = _repository.GetTent(setup.TentId);
        if (setupTent is null)
        {
            ModelState.AddModelError(fieldName, $"Zelt mit Id {setup.TentId} existiert nicht.");
            return false;
        }

        if (!SetupTentCompatibilityPolicy.IsCompatible(setupTent.TentType, setup.SetupType))
        {
            ModelState.AddModelError(fieldName, $"Setup-Typ {setup.SetupType} ist fuer Tent-Typ {setupTent.TentType} nicht erlaubt.");
            return false;
        }

        if (grow.TentId.HasValue && grow.TentId.Value != setup.TentId)
        {
            ModelState.AddModelError(fieldName, "Das Production-Setup gehoert zu einem anderen Zelt als der GrowRun.");
            return false;
        }

        return true;
    }

    private bool ValidateHydroStyle(HydroStyle hydroStyle)
    {
        if (hydroStyle is HydroStyle.DWC or HydroStyle.RDWC)
        {
            return true;
        }

        ModelState.AddModelError(nameof(GrowUpsertRequest.HydroStyle), "Grow OS unterstuetzt neue Grows aktuell nur mit DWC oder RDWC.");
        return false;
    }

    private static bool IsLegacyGrowWithoutHydroSetup(GrowRun existing)
        => !existing.SystemId.HasValue;

    private static string? FormatPotSize(GrowSystem hydroSetup)
    {
        if (hydroSetup.PotSizeLiters is > 0 && hydroSetup.PotCount is > 0)
        {
            return $"{hydroSetup.PotCount.Value.ToString(CultureInfo.InvariantCulture)} x {FormatLiters(hydroSetup.PotSizeLiters.Value)} L";
        }

        if (hydroSetup.PotSizeLiters is > 0)
        {
            return $"{FormatLiters(hydroSetup.PotSizeLiters.Value)} L";
        }

        return null;
    }

    private static string? FormatReservoirSize(GrowSystem hydroSetup)
    {
        var totalVolume = CalculateTotalVolume(hydroSetup);
        if (totalVolume is > 0)
        {
            return $"{FormatLiters(totalVolume.Value)} L Gesamtvolumen";
        }

        if (hydroSetup.ReservoirLiters is > 0)
        {
            return $"{FormatLiters(hydroSetup.ReservoirLiters.Value)} L Tank";
        }

        return null;
    }

    private static double? CalculateTotalVolume(GrowSystem hydroSetup)
    {
        var potVolume = hydroSetup.PotCount.GetValueOrDefault() * hydroSetup.PotSizeLiters.GetValueOrDefault();
        var total = potVolume + hydroSetup.ReservoirLiters.GetValueOrDefault();
        return total > 0 ? total : null;
    }

    private static string FormatLiters(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
