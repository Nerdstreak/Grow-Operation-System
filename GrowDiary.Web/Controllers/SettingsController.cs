using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("settings")]
public sealed class SettingsController : Controller
{
    private readonly GrowRepository _repository;

    public SettingsController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    public IActionResult Index()
        => Redirect("/einstellungen");

    [HttpPost(nameof(SaveHomeAssistant))]
    [ValidateAntiForgeryToken]
    public IActionResult SaveHomeAssistant(SettingsViewModel model)
    {
        _repository.SaveHomeAssistantSettings(model.HomeAssistant);
        TempData["Flash"] = "Home-Assistant-Einstellungen gespeichert.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost(nameof(SaveTent) + "/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult SaveTent(int id, GrowDiary.Web.Models.Tent tent)
    {
        tent.Id = id;
        _repository.UpdateTent(tent);
        TempData["Flash"] = $"Zelt „{tent.Name}“ gespeichert.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("backup")]
    public IActionResult BackupDatabase()
        => StatusCode(
            StatusCodes.Status410Gone,
            ApiErrorFactory.Create(
                "legacy_backup_disabled",
                "Der direkte SQLite-Download wurde deaktiviert. Nutze POST /api/system/backup, damit Backups ohne Secrets, DataProtectionKeys, Uploads und Logs erzeugt werden.",
                StatusCodes.Status410Gone,
                traceId: HttpContext?.TraceIdentifier));
}
