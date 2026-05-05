namespace GrowDiary.Web.Api.Contracts;

public sealed record KnowledgeOverviewDto(
    IReadOnlyList<NutrientProgramDto> Programs,
    IReadOnlyList<MediumPlaybookDto> Playbooks
);

public sealed record NutrientProgramDto(
    string Key,
    string Name,
    string Manufacturer,
    string Category,
    string Summary,
    string BestFor,
    string WaterGuidance,
    string PhGuidance,
    string EcGuidance,
    IReadOnlyList<NutrientProgramStageDto> Stages,
    IReadOnlyList<string> Tips
);

public sealed record NutrientProgramStageDto(
    string Stage,
    string Dose,
    string Target,
    string Notes
);

public sealed record MediumPlaybookDto(
    string Key,
    string Title,
    string Summary,
    IReadOnlyList<string> FocusPoints,
    IReadOnlyList<string> RedFlags
);
