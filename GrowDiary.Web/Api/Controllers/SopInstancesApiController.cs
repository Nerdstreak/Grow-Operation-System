using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/sop-instances")]
[Produces("application/json")]
public sealed class SopInstancesApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly KnowledgeBaseLoader _knowledgeBase;

    public SopInstancesApiController(GrowRepository repository, KnowledgeBaseLoader knowledgeBase)
    {
        _repository = repository;
        _knowledgeBase = knowledgeBase;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SopInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<SopInstanceDto>> List([FromQuery] int growId)
    {
        if (_repository.GetGrow(growId) is null)
        {
            ModelState.AddModelError(nameof(StartSopInstanceRequest.GrowId), $"Grow mit Id {growId} existiert nicht.");
            return ValidationError();
        }

        return Ok(_repository.GetSopInstancesByGrow(growId).Select(instance => instance.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SopInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<SopInstanceDto> Detail(int id)
    {
        var instance = _repository.GetSopInstance(id);
        return instance is null
            ? NotFoundError("sop_instance_not_found", $"SOP-Instanz mit Id {id} existiert nicht.")
            : Ok(instance.ToDto());
    }

    [HttpGet("{id:int}/steps")]
    [ProducesResponseType(typeof(IReadOnlyList<SopStepInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<SopStepInstanceDto>> Steps(int id)
    {
        if (_repository.GetSopInstance(id) is null)
        {
            return NotFoundError("sop_instance_not_found", $"SOP-Instanz mit Id {id} existiert nicht.");
        }

        return Ok(_repository.GetSopStepInstances(id).Select(step => step.ToDto()).ToList());
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(SopInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public ActionResult<SopInstanceDto> Start([FromBody] StartSopInstanceRequest request)
    {
        if (_repository.GetGrow(request.GrowId) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {request.GrowId} existiert nicht.");
        }

        if (!Enum.IsDefined(request.Source))
        {
            ModelState.AddModelError(nameof(request.Source), "Source muss Manual, Recommendation oder System sein.");
        }

        var sop = _knowledgeBase.Sops.FirstOrDefault(item => string.Equals(item.Id, request.SopId, StringComparison.OrdinalIgnoreCase));
        if (sop is null)
        {
            ModelState.AddModelError(nameof(request.SopId), $"SOP mit Id '{request.SopId}' existiert nicht.");
        }
        else if (sop.Steps.Count == 0)
        {
            ModelState.AddModelError(nameof(request.SopId), $"SOP '{request.SopId}' hat keine ausfuehrbaren Steps.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        try
        {
            var instance = _repository.StartSopInstance(
                request.GrowId,
                sop!,
                request.Source,
                request.SourceRecommendationKey,
                request.TreatmentRecommendationStableKey,
                request.Notes);
            return CreatedAtAction(nameof(Detail), new { id = instance.Id }, _repository.GetSopInstance(instance.Id)!.ToDto());
        }
        catch (InvalidOperationException)
        {
            return Conflict(new ApiError("active_sop_exists", "Fuer diesen Grow ist diese SOP bereits aktiv."));
        }
    }
}
