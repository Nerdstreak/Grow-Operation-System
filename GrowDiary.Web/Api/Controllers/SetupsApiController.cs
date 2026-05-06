using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/setups")]
[Produces("application/json")]
public sealed class SetupsApiController : ApiControllerBase
{
    private static readonly HashSet<string> MotherHealthStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stable",
        "Watch",
        "Critical"
    };

    private static readonly HashSet<string> QuarantineResults = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending",
        "Cleared",
        "Rejected"
    };

    private readonly GrowRepository _repository;

    public SetupsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SetupDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SetupDto>> List([FromQuery] int? tentId = null)
    {
        var setups = tentId.HasValue
            ? _repository.GetSetupsForTent(tentId.Value)
            : _repository.GetSetups();

        return Ok(setups.Select(setup => setup.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<SetupDto> Detail(int id)
    {
        var setup = _repository.GetSetup(id);
        if (setup is null)
        {
            return NotFoundError("setup_not_found", $"Setup mit Id {id} existiert nicht.");
        }

        return Ok(setup.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(SetupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<SetupDto> Create([FromBody] CreateSetupRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Bitte gib dem Setup einen Namen.");
            return ValidationError();
        }

        var tent = _repository.GetTent(request.TentId);
        if (tent is null)
        {
            ModelState.AddModelError(nameof(request.TentId), $"Zelt mit Id {request.TentId} existiert nicht.");
            return ValidationError();
        }

        if (!SetupTentCompatibilityPolicy.IsCompatible(tent.TentType, request.SetupType))
        {
            ModelState.AddModelError(nameof(request.SetupType), $"Setup-Typ {request.SetupType} ist fuer Tent-Typ {tent.TentType} nicht erlaubt.");
            return ValidationError();
        }

        ValidateBasisFields(request.MotherHealthStatus, request.QuarantineResult, request.QuarantineStartedAt, request.QuarantinePlannedEndAt);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var setup = _repository.CreateSetup(request.ToModel());
        return CreatedAtAction(nameof(Detail), new { id = setup.Id }, setup.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<SetupDto> Update(int id, [FromBody] UpdateSetupRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Bitte gib dem Setup einen Namen.");
            return ValidationError();
        }

        var setup = _repository.GetSetup(id);
        if (setup is null)
        {
            return NotFoundError("setup_not_found", $"Setup mit Id {id} existiert nicht.");
        }

        ValidateBasisFields(request.MotherHealthStatus, request.QuarantineResult, request.QuarantineStartedAt, request.QuarantinePlannedEndAt);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        request.ApplyTo(setup);
        _repository.UpdateSetup(setup);

        return Ok(_repository.GetSetup(id)!.ToDto());
    }

    private void ValidateBasisFields(string? motherHealthStatus, string? quarantineResult, DateTime? quarantineStartedAt, DateTime? quarantinePlannedEndAt)
    {
        if (!string.IsNullOrWhiteSpace(motherHealthStatus) && !MotherHealthStatuses.Contains(motherHealthStatus.Trim()))
        {
            ModelState.AddModelError(nameof(UpdateSetupRequest.MotherHealthStatus), "MotherHealthStatus muss Stable, Watch oder Critical sein.");
        }

        if (!string.IsNullOrWhiteSpace(quarantineResult) && !QuarantineResults.Contains(quarantineResult.Trim()))
        {
            ModelState.AddModelError(nameof(UpdateSetupRequest.QuarantineResult), "QuarantineResult muss Pending, Cleared oder Rejected sein.");
        }

        if (quarantineStartedAt.HasValue && quarantinePlannedEndAt.HasValue && quarantinePlannedEndAt.Value < quarantineStartedAt.Value)
        {
            ModelState.AddModelError(nameof(UpdateSetupRequest.QuarantinePlannedEndAt), "Das geplante Ende darf nicht vor dem Quarantaene-Start liegen.");
        }
    }
}
