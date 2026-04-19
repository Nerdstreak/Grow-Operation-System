using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("knowledge")]
public sealed class KnowledgeController : Controller
{
    private readonly CultivationKnowledgeService _knowledgeService;

    public KnowledgeController(CultivationKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    [HttpGet("")]
    public IActionResult Index(string? key = null)
        => Redirect("/wissen");
}
