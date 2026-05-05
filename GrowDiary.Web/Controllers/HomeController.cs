using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels.Live;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistantService;
    private readonly GrowDashboardComposer _composer;
    private readonly GrowAlertService _growAlertService;

    public HomeController(
        GrowRepository repository,
        HomeAssistantService homeAssistantService,
        GrowDashboardComposer composer,
        GrowAlertService growAlertService)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
        _composer = composer;
        _growAlertService = growAlertService;
    }

    [HttpGet("/mvc-home-legacy")]
    public IActionResult Index() => Redirect("/");

    [HttpGet("/api/live/home")]
    public async Task<IActionResult> Live(CancellationToken cancellationToken)
    {
        var settings = _repository.GetHomeAssistantSettings();
        var tents = _repository.GetTents().Where(tent => tent.ActiveGrowCount > 0).ToList();
        var payload = new List<TentLivePayload>();

        foreach (var tent in tents)
        {
            var measurements = _repository.GetMeasurementsForTent(tent.Id);
            var states = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
            var metrics = _composer.BuildTentMetrics(tent, states, measurements);
            var alerts = tent.ActiveGrows
                .SelectMany(grow => _growAlertService.BuildAlertsForGrow(grow))
                .Take(3)
                .ToList();
            var tone = GrowAlertService.ResolveStateTone(alerts, settings.IsConfigured);

            payload.Add(new TentLivePayload
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

        return Json(new { refreshedAtUtc = DateTime.UtcNow, tents = payload });
    }

    [HttpGet("/Home/Error")]
    public IActionResult Error() => Problem(title: "Unerwarteter Fehler.");
}
