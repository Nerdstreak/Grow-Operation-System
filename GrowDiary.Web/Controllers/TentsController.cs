using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels;
using GrowDiary.Web.ViewModels.Live;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[Route("tents")]
public sealed class TentsController : Controller
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistantService;
    private readonly GrowDashboardComposer _composer;
    private readonly RecommendationEngine _recommendationEngine;
    private readonly AppPaths _paths;
    private readonly SensorReadingRepository _sensorRepo;

    public TentsController(GrowRepository repository, HomeAssistantService homeAssistantService, GrowDashboardComposer composer, RecommendationEngine recommendationEngine, AppPaths paths, SensorReadingRepository sensorRepo)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
        _composer = composer;
        _recommendationEngine = recommendationEngine;
        _paths = paths;
        _sensorRepo = sensorRepo;
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

        var settings = _repository.GetHomeAssistantSettings();
        var measurements = _repository.GetMeasurementsForTent(id);
        var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
        var metrics = _composer.BuildTentMetrics(tent, states, measurements);
        var alerts = tent.ActiveGrows.SelectMany(BuildAlertsForGrow).Take(8).ToList();
        var tone = alerts.Any(a => a.Severity == "danger") ? "critical"
            : alerts.Any(a => a.Severity == "warning") ? "attention"
            : settings.IsConfigured ? "healthy"
            : "neutral";

        return Json(new TentLivePayload
        {
            TentId = tent.Id,
            StateTone = tone,
            StateLabel = tone switch
            {
                "critical" => "kritisch",
                "attention" => "beobachten",
                "healthy" => "stabil",
                _ => "neutral"
            },
            CameraUrl = settings.IsConfigured && !string.IsNullOrWhiteSpace(tent.CameraEntityId)
                ? Url.Action("CameraSnapshot", "Tents", new { id = tent.Id, t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() })
                : null,
            RefreshedAtUtc = DateTime.UtcNow,
            Metrics = metrics.Select(ToPayload).ToList()
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

        var settings = _repository.GetHomeAssistantSettings();
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

        var settings = _repository.GetHomeAssistantSettings();
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

    private IReadOnlyList<RecommendationCard> BuildAlertsForGrow(GrowRun grow)
    {
        var latest = _repository.GetLatestMeasurement(grow.Id);
        var previous = latest is null ? null : _repository.GetPreviousMeasurement(grow.Id, latest.TakenAt, latest.Id);
        var lastSolutionChangeAt = _repository.GetMeasurementsForGrow(grow.Id)
            .Where(x => x.SolutionChange)
            .OrderByDescending(x => x.TakenAt)
            .Select(x => (DateTime?)x.TakenAt)
            .FirstOrDefault();
        return _recommendationEngine.Evaluate(grow, latest, previous, lastSolutionChangeAt)
            .Take(2)
            .ToList();
    }

    private static MetricPayload ToPayload(MetricCard metric)
        => new()
        {
            Key = metric.Key,
            Label = metric.Label,
            Value = metric.Value,
            Unit = metric.Unit,
            Tone = metric.Tone,
            Hint = metric.Hint
        };
}
