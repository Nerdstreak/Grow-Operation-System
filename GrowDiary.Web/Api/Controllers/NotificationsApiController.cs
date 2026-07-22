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
    private readonly HardwareRepository _hardware;
    private readonly NotificationService _notifications;

    public NotificationsApiController(
        NotificationSettingsRepository settingsRepo,
        GrowRepository repository,
        HomeAssistantService homeAssistant,
        HardwareRepository hardware,
        NotificationService notifications)
    {
        _settingsRepo = settingsRepo;
        _repository = repository;
        _homeAssistant = homeAssistant;
        _hardware = hardware;
        _notifications = notifications;
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

    /// <summary>
    /// Runs the real calibration-reminder path now and reports the outcome, so the user can
    /// verify sensor reminders actually reach the phone — and understand why one didn't
    /// (no phone saved, category off, quiet hours, or nothing due).
    /// </summary>
    [HttpPost("test-calibration")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestCalibration(CancellationToken cancellationToken)
    {
        var settings = _settingsRepo.GetNotificationSettings();
        var configured = settings.IsConfigured;
        var categoryEnabled = settings.IsCategoryEnabled(NotificationCategory.Calibration);
        var quietNow = settings.IsQuietHour(DateTime.Now.Hour);
        var due = _hardware.GetDueCalibrationEvents(DateTime.UtcNow)
            .Where(e => e.Status == CalibrationEventStatus.Planned)
            .ToList();

        bool sent = false;
        string message;
        if (!configured)
        {
            message = "Kein Push-Handy gespeichert. Trag oben deinen Push-Dienst ein und speichere — sonst kommt keine Erinnerung an.";
        }
        else if (!categoryEnabled)
        {
            message = "„Kalibrierung fällig“ ist ausgeschaltet. Schalte es unten ein, damit Erinnerungen rausgehen.";
        }
        else if (quietNow)
        {
            message = "Gerade ist Ruhezeit — jetzt würde keine Erinnerung gesendet. Deine Einrichtung ist aber in Ordnung.";
        }
        else
        {
            var body = CalibrationReminderService.BuildDueMessage(due)
                ?? "Test: So sieht eine Kalibrierungs-Erinnerung aus. Aktuell ist nichts fällig.";
            sent = await _notifications.SendAsync(NotificationCategory.Calibration, "🌱 Grow OS · Kalibrierung", body, cancellationToken);
            message = sent
                ? (due.Count > 0
                    ? $"{due.Count} Kalibrierung(en) fällig — Erinnerung ans Handy gesendet."
                    : "Test-Erinnerung ans Handy gesendet — Push funktioniert.")
                : "Einrichtung sieht ok aus, aber Home Assistant hat den Push nicht angenommen. Stimmt der Dienstname?";
        }

        return Ok(new
        {
            ok = sent,
            configured,
            categoryEnabled,
            quietNow,
            dueCount = due.Count,
            message,
        });
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
