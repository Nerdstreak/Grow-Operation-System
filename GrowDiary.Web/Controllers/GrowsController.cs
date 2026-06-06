using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

// Schlanke Redirect-Shims fuer alte /grows-Bookmarks. Die eigentliche
// Grow-Funktionalitaet liegt in der React-App + den /api/grows-Endpunkten.
[Route("grows")]
public sealed class GrowsController : Controller
{
    [HttpGet("create")]
    public IActionResult Create(int? templateId = null) => Redirect("/grows/new");

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id) => Redirect($"/grows/{id}/setup");

    [HttpGet("compare")]
    public IActionResult Compare(int? leftGrowId = null, int? rightGrowId = null)
        => Redirect(BuildCompareUrl(leftGrowId, rightGrowId));

    [HttpGet("{id:int}/export")]
    public IActionResult Export(int id) => Redirect($"/api/exports/grows/{id}");

    [HttpPost("{id:int}/confirm-germination")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmGermination(int id) => LegacyMutationDisabled();

    [HttpPost("{id:int}/confirm-rooting")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmRooting(int id) => LegacyMutationDisabled();

    [HttpPost("{id:int}/flip-to-flower")]
    [ValidateAntiForgeryToken]
    public IActionResult FlipToFlower(int id) => LegacyMutationDisabled();

    private IActionResult LegacyMutationDisabled()
        => StatusCode(
            StatusCodes.Status410Gone,
            GrowDiary.Web.Api.Contracts.ApiErrorFactory.Create(
                "legacy_mvc_mutation_disabled",
                "Diese alte MVC-POST-Route wurde deaktiviert. Nutze die versionierten API-Endpunkte oder die aktuelle React/PWA-Oberfläche.",
                StatusCodes.Status410Gone,
                traceId: HttpContext?.TraceIdentifier));

    private static string BuildCompareUrl(int? leftGrowId, int? rightGrowId)
    {
        var query = new List<string>();
        if (leftGrowId.HasValue)
        {
            query.Add($"leftGrowId={leftGrowId.Value}");
        }

        if (rightGrowId.HasValue)
        {
            query.Add($"rightGrowId={rightGrowId.Value}");
        }

        return query.Count == 0
            ? "/analyse"
            : $"/analyse?{string.Join("&", query)}";
    }
}
