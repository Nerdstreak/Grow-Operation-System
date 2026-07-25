using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// What the holiday guard sees right now: the slow failures that no single reading reveals.
/// </summary>
[ApiController]
[Route("api/trends")]
[Produces("application/json")]
public sealed class TrendsApiController : ApiControllerBase
{
    private readonly TrendWatchRunner _runner;
    private readonly GrowRepository _repository;

    public TrendsApiController(TrendWatchRunner runner, GrowRepository repository)
    {
        _runner = runner;
        _repository = repository;
    }

    [HttpGet("{growId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<TrendFindingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<TrendFindingDto>> ForGrow(int growId)
    {
        if (_repository.GetGrow(growId) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        var findings = _runner.Inspect(growId, DateTime.Now)
            .Select(finding => new TrendFindingDto(
                finding.Code,
                finding.Severity.ToString(),
                finding.Headline,
                finding.Detail,
                finding.GuidanceId))
            .ToList();

        return Ok(findings);
    }
}
