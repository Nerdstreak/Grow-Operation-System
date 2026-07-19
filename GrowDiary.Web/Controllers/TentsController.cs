using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels.Live;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("tents")]
public sealed class TentsController : Controller
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistantService;
    private readonly GrowDashboardComposer _composer;
    private readonly GrowAlertService _growAlertService;
    private readonly AppPaths _paths;

    public TentsController(GrowRepository repository, HomeAssistantService homeAssistantService, GrowDashboardComposer composer, GrowAlertService growAlertService, AppPaths paths)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
        _composer = composer;
        _growAlertService = growAlertService;
        _paths = paths;
    }

    [HttpGet("")]
    public IActionResult Index(int? selected, CancellationToken cancellationToken)
        => Redirect("/zelte");

    [HttpGet("{id:int}")]
    public IActionResult Details(int id, CancellationToken cancellationToken)
        => Redirect($"/zelte/{id}");

    [HttpGet("/api/live/tents/{id:int}")]
    public async Task<IActionResult> Live(int id, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(id);
        if (tent is null)
        {
            return NotFound();
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var measurements = _repository.GetMeasurementsForTent(id);
        var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
        var metrics = _composer.BuildTentMetrics(tent, states, measurements);
        var alerts = tent.ActiveGrows
            .SelectMany(grow => _growAlertService.BuildAlertsForGrow(grow, maxCount: 2))
            .Take(8)
            .ToList();
        var tone = GrowAlertService.ResolveStateTone(alerts, settings.IsConfigured);

        return Json(new TentLivePayload
        {
            TentId = tent.Id,
            StateTone = tone,
            StateLabel = GrowAlertService.ResolveStateLabel(tone),
            CameraUrl = settings.IsConfigured && !string.IsNullOrWhiteSpace(tent.CameraEntityId)
                ? Url.Action("CameraSnapshot", "Tents", new { id = tent.Id, t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() })
                : null,
            RefreshedAtUtc = DateTime.UtcNow,
            Metrics = metrics.Select(metric => metric.ToPayload()).ToList()
        });
    }

    [HttpGet("{id:int}/camera.jpg")]
    public async Task<IActionResult> CameraSnapshot(int id, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(id);
        if (tent is null || string.IsNullOrWhiteSpace(tent.CameraEntityId))
        {
            return NotFound();
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var snapshot = await _homeAssistantService.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
        if (snapshot is null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        return File(snapshot.Value.Bytes, snapshot.Value.ContentType);
    }

    /// <summary>
    /// Live-Standbild direkt aus HA – wird per JS alle 5 s neu geladen.
    /// </summary>
    [HttpGet("{id:int}/camera-stream")]
    public async Task<IActionResult> CameraStream(int id, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(id);
        if (tent is null || string.IsNullOrWhiteSpace(tent.CameraEntityId))
        {
            return NotFound();
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var snapshot = await _homeAssistantService.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
        if (snapshot is null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        return File(snapshot.Value.Bytes, snapshot.Value.ContentType);
    }

    /// <summary>
    /// Letztes täglich gespeichertes Kamera-Bild aus App_Data/snapshots/{tentId}/.
    /// Fallback für das Ops-Dashboard.
    /// </summary>
    [HttpGet("{id:int}/latest-snapshot")]
    public IActionResult LatestSnapshot(int id)
    {
        var snapshotDir = Path.Combine(_paths.ContentRootPath, "App_Data", "snapshots", id.ToString());
        if (!Directory.Exists(snapshotDir))
        {
            return NotFound();
        }

        var latest = Directory.GetFiles(snapshotDir, "*.jpg")
            .OrderByDescending(f => f)
            .FirstOrDefault();

        if (latest is null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "public, max-age=300";
        return PhysicalFile(latest, "image/jpeg");
    }
}
