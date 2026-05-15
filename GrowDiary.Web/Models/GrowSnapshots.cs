namespace GrowDiary.Web.Models;

public sealed record GrowTentSnapshot(
    int Id,
    string Name,
    string Kind,
    TentType TentType,
    TentStatus Status,
    int? WidthCm,
    int? DepthCm,
    int? TentHeightCm,
    string? LightType,
    int? LightWatt,
    int? ExhaustFanCount,
    int? ExhaustM3h,
    int? CirculationFanCount,
    bool Co2Available,
    IReadOnlyList<GrowTentSensorSnapshot> Sensors);

public sealed record GrowTentSensorSnapshot(
    int Id,
    SensorMetricType MetricType,
    string HaEntityId,
    string? DisplayLabel,
    bool IsActive);

public sealed record GrowHydroSetupSnapshot(
    int Id,
    int? TentId,
    string? TentName,
    string Name,
    string HydroStyle,
    int? PotCount,
    double? PotSizeLiters,
    double? ReservoirLiters,
    double? TotalVolumeLiters,
    HydroSetupStatus Status,
    HydroSetupLayoutType LayoutType,
    ReservoirPosition ReservoirPosition,
    bool HasCirculationPump,
    string? CirculationPumpNotes,
    bool HasAirPump,
    string? AirPumpNotes,
    int? AirStoneCount,
    bool HasChiller,
    bool HasUvSterilizer,
    string? Notes);
