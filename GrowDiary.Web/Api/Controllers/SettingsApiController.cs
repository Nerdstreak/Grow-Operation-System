using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Produces("application/json")]
public sealed class SettingsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;

    public SettingsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(SettingsOverviewDto), StatusCodes.Status200OK)]
    public ActionResult<SettingsOverviewDto> Overview()
        => Ok(new SettingsOverviewDto(
            HomeAssistant: _repository.GetHomeAssistantSettings().ToDto(),
            Tents: _repository.GetTents().Select(tent => tent.ToDto()).ToList()));

    [HttpGet("home-assistant")]
    [ProducesResponseType(typeof(HomeAssistantSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<HomeAssistantSettingsDto> HomeAssistant()
        => Ok(_repository.GetHomeAssistantSettings().ToDto());

    [HttpPut("home-assistant")]
    [ProducesResponseType(typeof(HomeAssistantSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<HomeAssistantSettingsDto> SaveHomeAssistant([FromBody] SaveHomeAssistantSettingsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var settings = request.ToModel();
        _repository.SaveHomeAssistantSettings(settings);
        return Ok(_repository.GetHomeAssistantSettings().ToDto());
    }

    [HttpGet("tents")]
    [ProducesResponseType(typeof(IReadOnlyList<TentDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TentDto>> Tents()
        => Ok(_repository.GetTents().Select(tent => tent.ToDto()).ToList());

    [HttpPost("tents")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<TentDto> CreateTent([FromBody] CreateTentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        if (!ValidateTentRequest(request.Name, request.TentType))
        {
            return ValidationError();
        }

        var created = _repository.CreateTent(request.ToModel());
        return CreatedAtAction(nameof(Tents), new { id = created.Id }, created.ToDto());
    }

    [HttpPut("tents/{id:int}")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<TentDto> SaveTent(int id, [FromBody] UpdateTentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var existing = _repository.GetTent(id);
        if (existing is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        if (!ValidateTentRequest(request.Name, request.TentType, request.Status))
        {
            return ValidationError();
        }

        _repository.UpdateTent(request.ToModel(id));
        if (request.Sensors is not null)
        {
            _repository.ReplaceTentSensors(id, request.ToSensors(id));
        }

        return Ok(_repository.GetTent(id)!.ToDto());
    }


    [HttpPost("tents/{id:int}/archive")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<TentDto> ArchiveTent(int id)
    {
        var existing = _repository.GetTent(id);
        if (existing is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        _repository.ArchiveTent(id);
        return Ok(_repository.GetTent(id)!.ToDto());
    }

    [HttpDelete("tents/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public IActionResult DeleteTent(int id)
    {
        var existing = _repository.GetTent(id);
        if (existing is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        if (_repository.HasTentDependencies(id))
        {
            _repository.ArchiveTent(id);
            return Ok(_repository.GetTent(id)!.ToDto());
        }

        _repository.DeleteTent(id);
        return NoContent();
    }

    private bool ValidateTentRequest(string name, string? tentType, string? status = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.Name), "Bitte gib dem Zelt einen Namen.");
        }

        if (!string.IsNullOrWhiteSpace(tentType) && !Enum.TryParse<TentType>(tentType, out _))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.TentType), $"Tent-Typ {tentType} ist nicht erlaubt.");
        }

        if (!string.IsNullOrWhiteSpace(status) && !Enum.TryParse<TentStatus>(status, out _))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.Status), $"Tent-Status {status} ist nicht erlaubt.");
        }

        return ModelState.IsValid;
    }
}
