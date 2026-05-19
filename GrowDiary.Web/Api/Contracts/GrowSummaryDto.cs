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
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? FlipDate,
    DateTime? GerminatedAt,
    DateTime? RootedAt,
    int MeasurementCount,
    string? LatestPhotoPath,
    GrowStage? LatestStage,
    double? LatestReservoirPh,
    double? LatestReservoirEc,
    DateTime? LatestMeasurementAt
);
