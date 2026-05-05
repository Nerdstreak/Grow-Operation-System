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

    public GrowsApiController(
        GrowRepository repository,
        AuditRepository auditRepository,
        WeekCounterService weekCounter)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _weekCounter = weekCounter;
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
}
