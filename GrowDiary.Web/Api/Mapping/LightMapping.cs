using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class LightMapping
{
    public static LightScheduleDto ToDto(this LightSchedule schedule) => new(
        Id: schedule.Id,
        TentId: schedule.TentId,
        Name: schedule.Name,
        IsActive: schedule.IsActive,
        LightsOnTime: schedule.LightsOnTime,
        LightsOffTime: schedule.LightsOffTime,
        TimeZoneId: schedule.TimeZoneId,
        Source: schedule.Source,
        CreatedAtUtc: schedule.CreatedAtUtc,
        UpdatedAtUtc: schedule.UpdatedAtUtc
    );

    public static LightSchedule ToModel(this CreateLightScheduleRequest request) => new()
    {
        TentId = request.TentId,
        Name = request.Name.Trim(),
        IsActive = request.IsActive,
        LightsOnTime = request.LightsOnTime.Trim(),
        LightsOffTime = request.LightsOffTime.Trim(),
        TimeZoneId = NormalizeOptional(request.TimeZoneId),
        Source = request.Source
    };

    public static void ApplyTo(this UpdateLightScheduleRequest request, LightSchedule schedule)
    {
        schedule.Name = request.Name.Trim();
        schedule.IsActive = request.IsActive;
        schedule.LightsOnTime = request.LightsOnTime.Trim();
        schedule.LightsOffTime = request.LightsOffTime.Trim();
        schedule.TimeZoneId = NormalizeOptional(request.TimeZoneId);
        schedule.Source = request.Source;
    }

    public static LightTransitionEventDto ToDto(this LightTransitionEvent transition) => new(
        Id: transition.Id,
        TentId: transition.TentId,
        Kind: transition.Kind,
        OccurredAtUtc: transition.OccurredAtUtc,
        Source: transition.Source,
        RawState: transition.RawState,
        CreatedAtUtc: transition.CreatedAtUtc
    );

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
