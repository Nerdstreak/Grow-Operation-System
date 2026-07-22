using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Single gateway for every push Grow OS sends. Callers say what category a message is;
/// this checks the central settings (a notify service is configured, the category is on,
/// and it is not quiet hours) and then pushes through Home Assistant.
/// </summary>
public sealed class NotificationService
{
    private readonly NotificationSettingsRepository _settingsRepo;
    private readonly GrowRepository _growRepository;
    private readonly HomeAssistantService _homeAssistant;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        NotificationSettingsRepository settingsRepo,
        GrowRepository growRepository,
        HomeAssistantService homeAssistant,
        ILogger<NotificationService> logger)
    {
        _settingsRepo = settingsRepo;
        _growRepository = growRepository;
        _homeAssistant = homeAssistant;
        _logger = logger;
    }

    public NotificationSettings GetSettings() => _settingsRepo.GetNotificationSettings();

    /// <summary>
    /// Sends a push if the category is enabled and it is not quiet hours. Returns false
    /// (silently) when notifications are unconfigured, the category is off, or it is quiet.
    /// </summary>
    public async Task<bool> SendAsync(NotificationCategory category, string title, string message, CancellationToken cancellationToken = default)
    {
        var settings = _settingsRepo.GetNotificationSettings();
        if (!settings.IsConfigured || !settings.IsCategoryEnabled(category) || settings.IsQuietHour(DateTime.Now.Hour))
        {
            return false;
        }

        var haSettings = _growRepository.GetEffectiveHomeAssistantSettings();
        var sent = await _homeAssistant.SendNotificationAsync(haSettings, settings.NotifyService!, title, message, cancellationToken);
        if (sent)
        {
            _logger.LogInformation("Benachrichtigung gesendet ({Category}): {Title}", category, title);
        }

        return sent;
    }

    /// <summary>
    /// Sends the daily digest. Unlike <see cref="SendAsync"/> this ignores quiet hours —
    /// the user picks the digest time deliberately, so it must arrive even at, say, 5:30.
    /// </summary>
    public async Task<bool> SendDigestAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var settings = _settingsRepo.GetNotificationSettings();
        if (!settings.IsConfigured)
        {
            return false;
        }

        var haSettings = _growRepository.GetEffectiveHomeAssistantSettings();
        return await _homeAssistant.SendNotificationAsync(haSettings, settings.NotifyService!, title, message, cancellationToken);
    }
}
