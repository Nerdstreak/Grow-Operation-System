namespace GrowDiary.Web.Api.Contracts;

/// <summary>The score sheet for one plant. Everything optional — it fills up over the run.</summary>
public sealed record PhenoEvaluationDto(
    int PlantInstanceId,
    int? VigorScore,
    string? InternodeSpacing,
    int? BranchingScore,
    int? LeafToBudScore,
    double? HeightAtFlipCm,
    List<string> TrainingMethods,
    int? TrainingResponseScore,
    int? StressToleranceScore,
    int? PestResistanceScore,
    int? FloweringDays,
    double? HeightAtHarvestCm,
    double? WetYieldG,
    double? DryYieldG,
    int? BudDensityScore,
    int? ResinScore,
    int? TrimEaseScore,
    int? AromaScore,
    string? AromaNotes,
    int? FlavorScore,
    int? EffectScore,
    string? EffectNotes,
    double? ThcPercent,
    double? CbdPercent,
    string? TerpeneNotes,
    double? ManualOverallScore,
    bool IsKeeper,
    bool ConfirmedInSecondRun,
    string? Notes,
    double? StretchFactor);

/// <summary>The weighted result, broken down so the total is explainable.</summary>
public sealed record PhenoScoreDto(
    double? Total,
    double? Yield,
    double? Quality,
    double? Potency,
    double? Resilience,
    double? Structure,
    bool IsManual);

public sealed record PhenoPlantDto(
    int PlantInstanceId,
    string Label,
    string? PhenoLabel,
    string? StrainName,
    int? StrainId,
    string PlantRole,
    string PlantStatus,
    int? ParentPlantId,
    PhenoEvaluationDto? Evaluation,
    PhenoScoreDto Score);

/// <summary>How much each trait bucket counts, in percent-ish units.</summary>
public sealed record PhenoWeightsDto(
    double Yield,
    double Quality,
    double Potency,
    double Resilience,
    double Structure);

public sealed record PhenoHuntDto(
    int GrowId,
    PhenoWeightsDto Weights,
    IReadOnlyList<PhenoPlantDto> Plants);
