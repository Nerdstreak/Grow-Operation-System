using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
[Produces("application/json")]
public sealed class KnowledgeApiController : ApiControllerBase
{
    private readonly CultivationKnowledgeService _knowledgeService;

    public KnowledgeApiController(CultivationKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(KnowledgeOverviewDto), StatusCodes.Status200OK)]
    public ActionResult<KnowledgeOverviewDto> Overview()
        => Ok(new KnowledgeOverviewDto(
            Programs: _knowledgeService.GetPrograms().Select(program => program.ToDto()).ToList(),
            Playbooks: _knowledgeService.GetMediumPlaybooks().Select(playbook => playbook.ToDto()).ToList()
        ));
}
