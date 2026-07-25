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
    private readonly SolutionStabilityAnalyzer _stability;

    public TrendsApiController(
        TrendWatchRunner runner,
        GrowRepository repository,
        SolutionStabilityAnalyzer stability)
    {
        _runner = runner;
        _repository = repository;
        _stability = stability;
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

    /// <summary>
    /// The SOP's diagnostic table, read across all its rows at once — which is the only way
    /// it distinguishes a feeding plant from chemical instability.
    /// </summary>
    [HttpGet("{growId:int}/stability")]
    [ProducesResponseType(typeof(StabilityAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<StabilityAssessmentDto> Stability(int growId)
    {
        if (_repository.GetGrow(growId) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        var assessment = _stability.Assess(_repository.GetMeasurementsForGrow(growId), DateTime.Now);

        return Ok(new StabilityAssessmentDto(
            assessment.Overall.ToString(),
            assessment.Headline,
            assessment.Detail,
            assessment.Signals
                .Select(signal => new StabilitySignalDto(
                    signal.Key, signal.Label, signal.Verdict.ToString(), signal.Observation))
                .ToList(),
            assessment.VisualChecks));
    }
}
