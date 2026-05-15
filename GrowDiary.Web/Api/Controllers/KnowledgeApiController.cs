using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Services.Knowledge.Schema;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
[Produces("application/json")]
public sealed class KnowledgeApiController : ApiControllerBase
{
    private readonly CultivationKnowledgeService _knowledgeService;
    private readonly KnowledgeBaseLoader _knowledgeBase;

    public KnowledgeApiController(CultivationKnowledgeService knowledgeService, KnowledgeBaseLoader knowledgeBase)
    {
        _knowledgeService = knowledgeService;
        _knowledgeBase = knowledgeBase;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(KnowledgeOverviewDto), StatusCodes.Status200OK)]
    public ActionResult<KnowledgeOverviewDto> Overview()
        => Ok(new KnowledgeOverviewDto(
            Programs: _knowledgeService.GetPrograms().Select(program => program.ToDto()).ToList(),
            Playbooks: _knowledgeService.GetMediumPlaybooks().Select(playbook => playbook.ToDto()).ToList()
        ));

    [HttpGet("treatments")]
    [ProducesResponseType(typeof(IReadOnlyList<TreatmentDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TreatmentDefinition>> GetTreatments()
        => Ok(_knowledgeBase.Treatments);

    [HttpGet("treatments/{id}")]
    [ProducesResponseType(typeof(TreatmentDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<TreatmentDefinition> GetTreatment(string id)
    {
        var item = _knowledgeBase.Treatments.FirstOrDefault(t => t.Id == id);
        return item is null ? NotFoundError("treatment_not_found", $"Treatment mit Id {id} existiert nicht.") : Ok(item);
    }

    [HttpGet("sops")]
    [ProducesResponseType(typeof(IReadOnlyList<SopDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SopDefinition>> GetSops()
        => Ok(_knowledgeBase.Sops);

    [HttpGet("sops/{id}")]
    [ProducesResponseType(typeof(SopDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<SopDefinition> GetSop(string id)
    {
        var item = _knowledgeBase.Sops.FirstOrDefault(t => t.Id == id);
        return item is null ? NotFoundError("sop_not_found", $"SOP mit Id {id} existiert nicht.") : Ok(item);
    }

    [HttpGet("setpoints")]
    [ProducesResponseType(typeof(IReadOnlyList<SetpointDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SetpointDefinition>> GetSetpoints()
        => Ok(_knowledgeBase.Setpoints);

    [HttpGet("pathogens")]
    [ProducesResponseType(typeof(IReadOnlyList<PathogenDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PathogenDefinition>> GetPathogens()
        => Ok(_knowledgeBase.Pathogens);

    [HttpGet("symptoms")]
    [ProducesResponseType(typeof(IReadOnlyList<SymptomDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SymptomDefinition>> GetSymptoms()
        => Ok(_knowledgeBase.Symptoms);

    [HttpGet("wear")]
    [ProducesResponseType(typeof(IReadOnlyList<WearTemplateDefinition>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WearTemplateDefinition>> GetWearTemplates()
        => Ok(_knowledgeBase.WearTemplates);
}
