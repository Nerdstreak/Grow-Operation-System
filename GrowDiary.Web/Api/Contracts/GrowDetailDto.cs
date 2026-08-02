using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// Vollständige Detaildaten eines Grow-Runs für die GrowDetail-Ansicht.
/// Measurements, Tasks und Journal kommen später über eigene Endpoints.
/// </summary>
public sealed record GrowDetailDto(
    int Id,
    int? SystemId,
    int? SetupId,
    string Name,
    string? Strain,
    string? Breeder,
    GrowStatus Status,
    MediumType MediumType,
    FeedingStyle FeedingStyle,
    HydroStyle HydroStyle,
    IrrigationType IrrigationType,
    WaterSource WaterSource,
    string? FeedProgramId,
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
    int? PlannedVegDays,
    string? SetpointProfileId,
    int? StrainId,
    int? PlantCount,
    int? PhenoNumber,
    int? TentId,
    string? TentName,
    string? HydroSetupName,
    GrowEntryPoint EntryPoint,
    int? DaysAlreadyInPhase,
    int? AutoflowerDaysSinceGermination,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? FlipDate,
    DateTime? GerminatedAt,
    DateTime? RootedAt,
    /// <summary>Wann der Saemling zur Veg wurde — beobachtet, nicht gerechnet.</summary>
    DateTime? VegStartedAt,
    /// <summary>Wann das Finish (Spuelen) begann — beobachtet am Trichom, nicht gerechnet.</summary>
    DateTime? FinishStartedAt,
    /// <summary>Die Phase von HEUTE, wie der Resolver sie sieht — damit Knöpfe und Kacheln aus derselben Quelle urteilen.</summary>
    string CurrentStage,
    string? Nutrients,
    string? Notes,
    int MeasurementCount,
    string? LatestPhotoPath,
    MeasurementDto? LatestMeasurement,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
