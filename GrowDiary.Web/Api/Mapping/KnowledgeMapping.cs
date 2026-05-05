using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class KnowledgeMapping
{
    public static NutrientProgramDto ToDto(this NutrientProgram program) => new(
        Key: program.Key,
        Name: program.Name,
        Manufacturer: program.Manufacturer,
        Category: program.Category,
        Summary: program.Summary,
        BestFor: program.BestFor,
        WaterGuidance: program.WaterGuidance,
        PhGuidance: program.PhGuidance,
        EcGuidance: program.EcGuidance,
        Stages: program.Stages.Select(stage => new NutrientProgramStageDto(
            stage.Stage,
            stage.Dose,
            stage.Target,
            stage.Notes)).ToList(),
        Tips: program.Tips.ToList()
    );

    public static MediumPlaybookDto ToDto(this MediumPlaybook playbook) => new(
        Key: playbook.Key,
        Title: playbook.Title,
        Summary: playbook.Summary,
        FocusPoints: playbook.FocusPoints.ToList(),
        RedFlags: playbook.RedFlags.ToList()
    );
}
