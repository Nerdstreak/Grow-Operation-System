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

    public GrowsApiController(
        GrowRepository repository,
        AuditRepository auditRepository,
        WeekCounterService weekCounter,
        DeviationAnalyzerService deviationAnalyzer)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _weekCounter = weekCounter;
        _deviationAnalyzer = deviationAnalyzer;
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
            Summary = $"Setup '{request.Name}' wurde erstellt{(request.TemplateId.HasValue ? $" auf Basis des Templates #{request.TemplateId}" : string.Empty)}."
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
            Action = "Setup geaendert",
            Summary = $"Setup von '{grow.Name}' aktualisiert. Status: {grow.Status}, Medium: {grow.Profile.Label}."
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
}
