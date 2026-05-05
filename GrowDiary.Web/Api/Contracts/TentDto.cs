namespace GrowDiary.Web.Api.Contracts;

public sealed record TentDto(
    int Id,
    string Name,
    string Kind,
    string TentType,
    string? Notes,
    int DisplayOrder,
    string AccentColor,
    int? WidthCm,
    int? DepthCm,
    int? TentHeightCm,
    string? LightType,
    int? LightWatt,
    string? LightController,
    string? LightControllerEntityId,
    int? ExhaustFanCount,
    int? ExhaustM3h,
    int? CirculationFanCount,
    string? HvacController,
    string? HvacControllerEntityId,
    bool Co2Available,
    string? CameraEntityId,
    int ActiveGrowCount,
    int ArchivedGrowCount,
    IReadOnlyList<TentSensorDto> Sensors
);

public sealed record TentSensorDto(
    int Id,
    int TentId,
    string MetricType,
    string HaEntityId,
    string? DisplayLabel,
    bool IsActive
);
