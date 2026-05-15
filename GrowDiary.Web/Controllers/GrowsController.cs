using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("grows")]
public sealed class GrowsController : Controller
{
    private readonly GrowRepository _repository;
    private readonly JournalRepository _journalRepository;

    public GrowsController(
        GrowRepository repository,
        TaskRepository taskRepository,
        JournalRepository journalRepository,
        AuditRepository auditRepository)
    {
        _repository = repository;
        _journalRepository = journalRepository;
    }

    // Redirect-Shims fuer alte Bookmarks
    [HttpGet("create")]
    public IActionResult Create(int? templateId = null) => Redirect("/grows/new");

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id) => Redirect($"/grows/{id}/setup");

    [HttpGet("measurements/{measurementId:int}/edit")]
    public IActionResult EditMeasurement(int measurementId) => Redirect($"/grows/measurements/{measurementId}/edit");

    [HttpGet("compare")]
    public IActionResult Compare(int? leftGrowId = null, int? rightGrowId = null)
        => Redirect(BuildCompareUrl(leftGrowId, rightGrowId));

    [HttpGet("{id:int}/addback")]
    public IActionResult Addback(int id) => Redirect($"/grows/{id}/addback");

    [HttpGet("{id:int}/harvest")]
    public IActionResult Harvest(int id) => Redirect($"/grows/{id}/harvest");

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

    public static GrowStage DetermineStageFromWeekInfo(GrowWeekInfo weekInfo) =>
        weekInfo.State switch
        {
            GrowCounterState.WaitingForGermination => GrowStage.Seedling,
            GrowCounterState.WaitingForRooting     => GrowStage.Clone,
            GrowCounterState.Vegetating            => GrowStage.Veg,
            GrowCounterState.Flowering             => GrowStage.Flower,
            GrowCounterState.Autoflowering         => weekInfo.AutoflowerWeek <= 4 ? GrowStage.Veg : GrowStage.Flower,
            _                                      => GrowStage.Veg
        };

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
