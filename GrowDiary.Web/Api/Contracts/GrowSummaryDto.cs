using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// Kompakte Darstellung eines Grow-Runs für Listen und Übersichten.
/// Enthält alles, was man für Cards oder Tabellen braucht.
/// </summary>
public sealed record GrowSummaryDto(
    int Id,
    string Name,
    string? Strain,
    string? Breeder,
    GrowStatus Status,
    HydroStyle HydroStyle,
    GrowEnvironment Environment,
    SeedType SeedType,
    StartMaterial StartMaterial,
    int? PlantCount,
    int? TentId,
    int? SystemId,
    int? SetupId,
    string? TentName,
    string? HydroSetupName,
    DateTime StartDate,
    DateTime? EndDate,
    int? StrainId,
    DateTime? FlipDate,
    /// <summary>Geplante Veg-Dauer in Tagen ab Start — daraus der geplante Flip.</summary>
    int? PlannedVegDays,
    string? SetpointProfileId,
    int? BreederFlowerWeeksMin,
    int? BreederFlowerWeeksMax,
    DateTime? GerminatedAt,
    DateTime? RootedAt,
    DateTime? VegStartedAt,
    DateTime? FinishStartedAt,
    /// <summary>Die Phase von HEUTE, aus dem Resolver — auch die Listen zeigen sie.</summary>
    string CurrentStage,
    int MeasurementCount,
    string? LatestPhotoPath,
    GrowStage? LatestStage,
    double? LatestReservoirPh,
    double? LatestReservoirEc,
    DateTime? LatestMeasurementAt
);
