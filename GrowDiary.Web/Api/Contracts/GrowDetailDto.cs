using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// Vollständige Detaildaten eines Grow-Runs für die GrowDetail-Ansicht.
/// Measurements, Tasks und Journal kommen später über eigene Endpoints.
/// </summary>
public sealed record GrowDetailDto(
    int Id,
    string Name,
    string? Strain,
    string? Breeder,
    GrowStatus Status,
    MediumType MediumType,
    FeedingStyle FeedingStyle,
    HydroStyle HydroStyle,
    IrrigationType IrrigationType,
    WaterSource WaterSource,
    GrowEnvironment Environment,
    string? Light,
    string? ContainerSize,
    string? ReservoirSize,
    string? MediumDetail,
    string? IrrigationStyle,
    bool HasChiller,
    SeedType SeedType,
    StartMaterial StartMaterial,
    GerminationMethod? GerminationMethod,
    PropagationMedium? PropagationMedium,
    string? CloneSource,
    bool CloneIsRooted,
    int? BreederFlowerWeeksMin,
    int? BreederFlowerWeeksMax,
    int? PlantCount,
    int? PhenoNumber,
    int? TentId,
    string? TentName,
    GrowEntryPoint EntryPoint,
    int? DaysAlreadyInPhase,
    int? AutoflowerDaysSinceGermination,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? FlipDate,
    DateTime? GerminatedAt,
    DateTime? RootedAt,
    string? Nutrients,
    string? Notes,
    int MeasurementCount,
    string? LatestPhotoPath,
    MeasurementDto? LatestMeasurement,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
