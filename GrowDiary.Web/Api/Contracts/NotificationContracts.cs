namespace GrowDiary.Web.Api.Contracts;

public sealed record NotificationSettingsDto(
    string? NotifyService,
    int? QuietHoursStartHour,
    int? QuietHoursEndHour,
    bool Thresholds,
    bool Calibration,
    bool Maintenance,
    bool SensorOffline,
    bool Risks);

public sealed record NotificationTestRequest(string? NotifyService);
