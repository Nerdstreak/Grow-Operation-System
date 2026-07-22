using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class NotificationsApiController : ControllerBase
{
    private readonly NotificationSettingsRepository _settingsRepo;
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistant;

    public NotificationsApiController(
        NotificationSettingsRepository settingsRepo,
        GrowRepository repository,
        HomeAssistantService homeAssistant)
    {
        _settingsRepo = settingsRepo;
        _repository = repository;
        _homeAssistant = homeAssistant;
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<NotificationSettingsDto> GetSettings()
        => Ok(ToDto(_settingsRepo.GetNotificationSettings()));

    [HttpPut("settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<NotificationSettingsDto> SaveSettings([FromBody] NotificationSettingsDto request)
    {
        var settings = new NotificationSettings
        {
            NotifyService = string.IsNullOrWhiteSpace(request.NotifyService) ? null : request.NotifyService.Trim(),
            QuietHoursStartHour = NormalizeHour(request.QuietHoursStartHour),
            QuietHoursEndHour = NormalizeHour(request.QuietHoursEndHour),
            Thresholds = request.Thresholds,
            Calibration = request.Calibration,
            Maintenance = request.Maintenance,
            SensorOffline = request.SensorOffline,
            Risks = request.Risks,
            DailyDigest = request.DailyDigest,
            DigestHour = request.DigestHour is >= 0 and <= 23 ? request.DigestHour : 6,
            DigestMinute = request.DigestMinute is >= 0 and <= 59 ? request.DigestMinute : 0,
            DigestDetailed = request.DigestDetailed,
        };

        _settingsRepo.SaveNotificationSettings(settings);
        return Ok(ToDto(settings));
    }

    /// <summary>Lists the Home Assistant notify services the user can push to.</summary>
    [HttpGet("notify-services")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> NotifyServices(CancellationToken cancellationToken)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        return Ok(await _homeAssistant.GetNotifyServicesAsync(settings, cancellationToken));
    }

    /// <summary>Sends a test push (to the provided service, or the saved one) so the user can confirm it works.</summary>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Test([FromBody] NotificationTestRequest request, CancellationToken cancellationToken)
    {
        var service = string.IsNullOrWhiteSpace(request.NotifyService)
            ? _settingsRepo.GetNotificationSettings().NotifyService
            : request.NotifyService.Trim();

        if (string.IsNullOrWhiteSpace(service))
        {
            return Ok(new { ok = false, message = "Kein Push-Dienst gewählt." });
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var sent = await _homeAssistant.SendNotificationAsync(
            settings, service, "🌱 Grow OS", "Test-Benachrichtigung — deine Push-Nachrichten sind richtig eingerichtet.", cancellationToken);

        return Ok(new { ok = sent });
    }

    private static int? NormalizeHour(int? hour) => hour is >= 0 and <= 23 ? hour : null;

    private static NotificationSettingsDto ToDto(NotificationSettings settings) => new(
        settings.NotifyService,
        settings.QuietHoursStartHour,
        settings.QuietHoursEndHour,
        settings.Thresholds,
        settings.Calibration,
        settings.Maintenance,
        settings.SensorOffline,
        settings.Risks,
        settings.DailyDigest,
        settings.DigestHour,
        settings.DigestMinute,
        settings.DigestDetailed);
}
