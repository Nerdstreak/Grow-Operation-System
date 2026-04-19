using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels;
using GrowDiary.Web.ViewModels.Live;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistantService;
    private readonly GrowDashboardComposer _composer;
    private readonly RecommendationEngine _recommendationEngine;
    private readonly TaskRepository _taskRepository;
    private readonly SensorReadingRepository _sensorRepo;

    public HomeController(
        GrowRepository repository,
        HomeAssistantService homeAssistantService,
        GrowDashboardComposer composer,
        RecommendationEngine recommendationEngine,
        TaskRepository taskRepository,
        SensorReadingRepository sensorRepo)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
        _composer = composer;
        _recommendationEngine = recommendationEngine;
        _taskRepository = taskRepository;
        _sensorRepo = sensorRepo;
    }

    // Route deaktiviert – wird von Blazor Home.razor übernommen
    [HttpGet("/mvc-home-legacy")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = _repository.GetHomeAssistantSettings();
        var tents = _repository.GetTents().Where(t => t.ActiveGrowCount > 0).ToList();

        var cards = new List<TentDashboardCardViewModel>();
        var allDeviations = new List<GrowDeviation>();

        foreach (var tent in tents)
        {
            var measurements = _repository.GetMeasurementsForTent(tent.Id);
            var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);

            var alerts = tent.ActiveGrows
                .SelectMany(grow => BuildAlertsForGrow(grow))
                .Take(3)
                .ToList();

            var sparklineFrom  = DateTime.UtcNow.AddDays(-2);
            var recentReadings = _sensorRepo.GetReadings(tent.Id, "temperature", sparklineFrom, DateTime.UtcNow)
                .Concat(_sensorRepo.GetReadings(tent.Id, "humidity",       sparklineFrom, DateTime.UtcNow))
                .Concat(_sensorRepo.GetReadings(tent.Id, "vpd",            sparklineFrom, DateTime.UtcNow))
                .Concat(_sensorRepo.GetReadings(tent.Id, "reservoir-ph",   sparklineFrom, DateTime.UtcNow))
                .Concat(_sensorRepo.GetReadings(tent.Id, "reservoir-ec",   sparklineFrom, DateTime.UtcNow))
                .ToList();

            foreach (var grow in tent.ActiveGrows)
            {
                allDeviations.AddRange(_composer.BuildDeviationsForGrow(grow, measurements));
            }

            cards.Add(new TentDashboardCardViewModel
            {
                Tent = tent,
                LiveMetrics = _composer.BuildTentMetrics(tent, states, measurements),
                Alerts = alerts,
                ClimateSparkline = _composer.BuildTentClimateChart(measurements, recentReadings, Array.Empty<TentSensorDailyStat>(), sparklineFrom.ToLocalTime()),
                WaterSparkline   = _composer.BuildTentWaterChart(measurements, recentReadings, Array.Empty<TentSensorDailyStat>(), sparklineFrom.ToLocalTime()),
                LastMeasurementAt = measurements.OrderByDescending(x => x.TakenAt).Select(x => (DateTime?)x.TakenAt).FirstOrDefault()
            });
        }

        var model = new HomeDashboardViewModel
        {
            Stats = _repository.GetDashboardStats(),
            Tents = cards,
            NeedsAttention = _repository.GetActiveGrows().Where(g => BuildAlertsForGrow(g).Any(a => a.Severity is "danger" or "warning")).Take(5).ToList(),
            HomeAssistantConfigured = settings.IsConfigured,
            DueSoonTasks = _taskRepository.GetDueSoon(10),
            RecentPhotos = _repository.GetRecentPhotos(8),
            ActiveDeviations = allDeviations
        };

        return View(model);
    }

    [HttpGet("/api/live/home")]
    public async Task<IActionResult> Live(CancellationToken cancellationToken)
    {
        var settings = _repository.GetHomeAssistantSettings();
        var tents = _repository.GetTents().Where(t => t.ActiveGrowCount > 0).ToList();
        var payload = new List<TentLivePayload>();

        foreach (var tent in tents)
        {
            var measurements = _repository.GetMeasurementsForTent(tent.Id);
            var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
            var metrics = _composer.BuildTentMetrics(tent, states, measurements);
            var alerts = tent.ActiveGrows.SelectMany(BuildAlertsForGrow).Take(3).ToList();

            var tone = alerts.Any(a => a.Severity == "danger") ? "critical"
                : alerts.Any(a => a.Severity == "warning") ? "attention"
                : settings.IsConfigured ? "healthy"
                : "neutral";

            payload.Add(new TentLivePayload
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

        return Json(new { refreshedAtUtc = DateTime.UtcNow, tents = payload });
    }

    [HttpGet("/Home/Error")]
    public IActionResult Error() => View();

    private IReadOnlyList<RecommendationCard> BuildAlertsForGrow(GrowRun grow)
    {
        var latest = _repository.GetLatestMeasurement(grow.Id);
        var previous = latest is null ? null : _repository.GetPreviousMeasurement(grow.Id, latest.TakenAt, latest.Id);
        var lastSolutionChangeAt = _repository.GetMeasurementsForGrow(grow.Id)
            .Where(x => x.SolutionChange)
            .OrderByDescending(x => x.TakenAt)
            .Select(x => (DateTime?)x.TakenAt)
            .FirstOrDefault();
        return _recommendationEngine.Evaluate(grow, latest, previous, lastSolutionChangeAt);
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
