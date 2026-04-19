using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("settings")]
public sealed class SettingsController : Controller
{
    private readonly GrowRepository _repository;
    private readonly TemplateRepository _templateRepository;
    private readonly AppPaths _paths;

    public SettingsController(GrowRepository repository, TemplateRepository templateRepository, AppPaths paths)
    {
        _repository = repository;
        _templateRepository = templateRepository;
        _paths = paths;
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
    {
        if (!System.IO.File.Exists(_paths.DatabasePath))
        {
            return NotFound();
        }
        return PhysicalFile(_paths.DatabasePath, "application/octet-stream", $"grow-diary-backup-{DateTime.Now:yyyyMMdd-HHmm}.db");
    }
}
