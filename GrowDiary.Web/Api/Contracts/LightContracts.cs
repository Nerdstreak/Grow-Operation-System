using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record LightScheduleDto(
    int Id,
    int TentId,
    string Name,
    bool IsActive,
    string LightsOnTime,
    string LightsOffTime,
    string? TimeZoneId,
    LightSource Source,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreateLightScheduleRequest
{
    public int TentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string LightsOnTime { get; set; } = "08:00";
    public string LightsOffTime { get; set; } = "20:00";
    public string? TimeZoneId { get; set; }
    public LightSource Source { get; set; } = LightSource.Manual;
}

public sealed class UpdateLightScheduleRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string LightsOnTime { get; set; } = "08:00";
    public string LightsOffTime { get; set; } = "20:00";
    public string? TimeZoneId { get; set; }
    public LightSource Source { get; set; } = LightSource.Manual;
}

public sealed record LightTransitionEventDto(
    int Id,
    int TentId,
    LightTransitionKind Kind,
    DateTime OccurredAtUtc,
    LightSource Source,
    string? RawState,
    DateTime CreatedAtUtc
);
