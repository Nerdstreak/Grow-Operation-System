using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("knowledge")]
public sealed class KnowledgeController : Controller
{
    public KnowledgeController()
    {
    }

    [HttpGet("")]
    public IActionResult Index(string? key = null)
        => Redirect("/wissen");
}
