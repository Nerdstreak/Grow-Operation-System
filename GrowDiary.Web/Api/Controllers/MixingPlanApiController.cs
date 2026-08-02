using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>Der Mischplan für heute — konkrete Milliliter statt „nach Plan".</summary>
[ApiController]
[Route("api/grows/{growId:int}/mixing-plan")]
[Produces("application/json")]
public sealed class MixingPlanApiController : ApiControllerBase
{
    private readonly MischplanService _mischplan;

    public MixingPlanApiController(MischplanService mischplan)
    {
        _mischplan = mischplan;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(Mischplan), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<Mischplan> Get(int growId)
    {
        var plan = _mischplan.FuerGrow(growId);
        return plan is null
            ? NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.")
            : Ok(plan);
    }
}
