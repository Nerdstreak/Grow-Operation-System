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
    public async Task<IActionResult> Index(int? selected, CancellationToken cancellationToken)
    {
        var settings = _repository.GetHomeAssistantSettings();
        var tents = _repository.GetTents().Where(t => t.ActiveGrowCount > 0).ToList();
        var cards = new List<TentDashboardCardViewModel>();

        foreach (var tent in tents)
        {
            var measurements = _repository.GetMeasurementsForTent(tent.Id);
            var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
            var sparklineFrom  = DateTime.UtcNow.AddDays(-2);
            var recentReadings = _sensorRepo.GetReadings(tent.Id, "temperature", sparklineFrom, DateTime.UtcNow)
                .Concat(_sensorRepo.GetReadings(tent.Id, "humidity",       sparklineFrom, DateTime.UtcNow))
                .Concat(_sensorRepo.GetReadings(tent.Id, "vpd",            sparklineFrom, DateTime.UtcNow))
                .ToList();
            var alerts = tent.ActiveGrows
                .SelectMany(BuildAlertsForGrow)
                .Take(6)
                .ToList();

            cards.Add(new TentDashboardCardViewModel
            {
                Tent = tent,
                LiveMetrics = _composer.BuildTentMetrics(tent, states, measurements),
                Alerts = alerts,
                ClimateSparkline = _composer.BuildTentClimateChart(measurements, recentReadings, Array.Empty<TentSensorDailyStat>(), sparklineFrom.ToLocalTime()),
                WaterSparkline   = _composer.BuildTentWaterChart(measurements, recentReadings, Array.Empty<TentSensorDailyStat>(), sparklineFrom.ToLocalTime()),
                LastMeasurementAt = measurements.OrderByDescending(m => m.TakenAt).Select(m => (DateTime?)m.TakenAt).FirstOrDefault()
            });
        }

        var selectedCard = cards.FirstOrDefault(c => c.Tent.Id == selected) ?? cards.FirstOrDefault();

        var model = new TentIndexViewModel
        {
            HomeAssistantConfigured = settings.IsConfigured,
            HomeAssistant = settings,
            Cards = cards,
            SelectedCard = selectedCard,
            SelectedTentId = selectedCard?.Tent.Id
        };

        return View(model);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(id);
        if (tent is null)
        {
            return NotFound();
        }

        var settings = _repository.GetHomeAssistantSettings();
        var measurements = _repository.GetMeasurementsForTent(id);
        var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
        var detailFrom   = DateTime.Today.AddDays(-30);
        var detailFromDo = DateOnly.FromDateTime(detailFrom);
        var detailToDo   = DateOnly.FromDateTime(DateTime.Today);
        var metricKeys   = new[] { "temperature", "humidity", "vpd", "reservoir-ph", "reservoir-ec", "reservoir-temp", "reservoir-level" };
        var dailyStats   = metricKeys
            .SelectMany(key => _sensorRepo.GetDailyStats(id, key, detailFromDo, detailToDo))
            .ToList();

        var hasStats = dailyStats.Any();
        DateTime chartFrom;
        IReadOnlyList<TentSensorReading> chartReadings;

        if (hasStats)
        {
            chartFrom = DateTime.Today.AddDays(-30);
            chartReadings = Array.Empty<TentSensorReading>();
        }
        else
        {
            // Noch keine Aggregate – letzte 7 Tage als Rohdaten
            chartFrom = DateTime.UtcNow.AddDays(-7);
            var recentFrom = DateTime.UtcNow.AddDays(-7);
            chartReadings = metricKeys
                .SelectMany(key => _sensorRepo.GetReadings(
                    id, key, recentFrom, DateTime.UtcNow))
                .ToList();
            dailyStats = new List<TentSensorDailyStat>();
        }

        var alerts = tent.ActiveGrows
            .SelectMany(BuildAlertsForGrow)
            .Take(8)
            .ToList();

        var hydroGrow = tent.ActiveGrows.FirstOrDefault(g => g.IrrigationType == IrrigationType.ActiveHydro);
        var currentStage = hydroGrow is not null
            ? (_repository.GetLatestMeasurement(hydroGrow.Id)?.Stage ?? GrowStage.Veg)
            : GrowStage.Veg;
        var targets = hydroGrow is not null
            ? TargetValueService.GetTargets(hydroGrow.HydroStyle, currentStage)
            : null;

        var model = new TentDetailsViewModel
        {
            Tent = tent,
            ActiveGrows = tent.ActiveGrows,
            ArchivedGrows = _repository.GetArchivedGrowsForTent(id).Take(6).ToList(),
            LiveMetrics = _composer.BuildTentMetrics(tent, states, measurements),
            ClimateChart = _composer.BuildTentClimateChart(measurements, chartReadings, dailyStats, chartFrom),
            WaterChart   = _composer.BuildTentWaterChart(measurements, chartReadings, dailyStats, chartFrom),
            ActivityChart = _composer.BuildActivityChart(measurements),
            Alerts = alerts,
            HomeAssistant = settings,
            HasHydroGrow = hydroGrow is not null,
            VpdTargetMin = targets?.VpdMin ?? 0,
            VpdTargetMax = targets?.VpdMax ?? 0,
            PhTargetMin  = targets?.PhMin  ?? 0,
            PhTargetMax  = targets?.PhMax  ?? 0,
            EcTargetMin  = targets?.EcMin  ?? 0,
            EcTargetMax  = targets?.EcMax  ?? 0,
            WaterTempTargetMin = targets?.WaterTempNightC ?? 0,
            WaterTempTargetMax = targets?.WaterTempDayC   ?? 0
        };

        return View(model);
    }

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
