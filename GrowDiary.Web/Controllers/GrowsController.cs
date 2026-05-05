using System.Text.Json;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("grows")]
public sealed class GrowsController : Controller
{
    private readonly GrowRepository _repository;
    private readonly TaskRepository _taskRepository;
    private readonly JournalRepository _journalRepository;
    private readonly AuditRepository _auditRepository;

    public GrowsController(
        GrowRepository repository,
        TaskRepository taskRepository,
        JournalRepository journalRepository,
        AuditRepository auditRepository)
    {
        _repository = repository;
        _taskRepository = taskRepository;
        _journalRepository = journalRepository;
        _auditRepository = auditRepository;
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
    public IActionResult Export(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFound();
        }

        var payload = new
        {
            grow,
            tent = _repository.GetTentForGrow(id),
            measurements = _repository.GetMeasurementsForGrow(id),
            photos = _repository.GetPhotosForGrow(id),
            tasks = _taskRepository.GetForGrow(id),
            journal = _journalRepository.GetForGrow(id),
            audit = _auditRepository.GetRecentForGrow(id, 50)
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"grow-{id}-{DateTime.Now:yyyyMMdd-HHmm}.json");
    }

    [HttpPost("{id:int}/confirm-germination")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmGermination(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();
        if (grow.StartMaterial != StartMaterial.Seed)
            return BadRequest("Keimungsbestaetigung ist nur fuer Samen-Grows moeglich.");
        if (grow.GerminatedAt.HasValue)
            return Redirect($"/grows/{id}");

        grow.GerminatedAt = DateTime.Now;
        if (grow.Status == GrowStatus.Planning) grow.Status = GrowStatus.Running;
        _repository.UpdateGrow(grow);
        _journalRepository.Create(new JournalEntry
        {
            GrowId = id,
            EntryType = JournalEntryType.GerminationConfirmed,
            Body = "Keimung bestaetigt.",
            OccurredAtUtc = DateTime.UtcNow
        });
        TempData["Flash"] = "Keimung bestaetigt.";
        return Redirect($"/grows/{id}");
    }

    [HttpPost("{id:int}/confirm-rooting")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmRooting(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();
        if (grow.StartMaterial != StartMaterial.Clone)
            return BadRequest("Bewurzelungsbestaetigung ist nur fuer Stecklinge moeglich.");
        if (grow.RootedAt.HasValue)
            return Redirect($"/grows/{id}");

        grow.RootedAt = DateTime.Now;
        grow.CloneIsRooted = true;
        if (grow.Status == GrowStatus.Planning) grow.Status = GrowStatus.Running;
        _repository.UpdateGrow(grow);
        _journalRepository.Create(new JournalEntry
        {
            GrowId = id,
            EntryType = JournalEntryType.CloneRooted,
            Body = "Bewurzelung bestaetigt.",
            OccurredAtUtc = DateTime.UtcNow
        });
        TempData["Flash"] = "Bewurzelung bestaetigt.";
        return Redirect($"/grows/{id}");
    }

    [HttpPost("{id:int}/flip-to-flower")]
    [ValidateAntiForgeryToken]
    public IActionResult FlipToFlower(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();
        if (grow.SeedType == SeedType.Autoflower)
            return BadRequest("Autoflower braucht keinen Flip.");
        if (grow.FlipDate.HasValue)
            return Redirect($"/grows/{id}");

        grow.FlipDate = DateTime.Today;
        _repository.UpdateGrow(grow);
        _journalRepository.Create(new JournalEntry
        {
            GrowId = id,
            EntryType = JournalEntryType.FlipToFlower,
            Body = "Auf 12/12 geflippt.",
            OccurredAtUtc = DateTime.UtcNow
        });
        TempData["Flash"] = "Flip zu 12/12 eingetragen.";
        return Redirect($"/grows/{id}");
    }

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
