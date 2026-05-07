using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/light-transitions")]
[Produces("application/json")]
public sealed class LightTransitionsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;

    public LightTransitionsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LightTransitionEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<LightTransitionEventDto>> List([FromQuery] int tentId)
    {
        if (_repository.GetTent(tentId) is null)
        {
            ModelState.AddModelError(nameof(CreateLightScheduleRequest.TentId), $"Zelt mit Id {tentId} existiert nicht.");
            return ValidationError();
        }

        return Ok(_repository.GetLightTransitionsByTent(tentId).Select(transition => transition.ToDto()).ToList());
    }
}
