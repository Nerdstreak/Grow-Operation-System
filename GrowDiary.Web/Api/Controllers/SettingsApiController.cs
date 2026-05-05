using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Bitte gib dem Zelt einen Namen.");
            return ValidationError();
        }

        _repository.UpdateTent(request.ToModel(id));
        if (request.Sensors is not null)
        {
            _repository.ReplaceTentSensors(id, request.ToSensors(id));
        }

        return Ok(_repository.GetTent(id)!.ToDto());
    }
}
