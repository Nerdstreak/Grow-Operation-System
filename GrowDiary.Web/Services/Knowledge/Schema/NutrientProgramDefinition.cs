using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class NutrientProgramDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("bestFor")]
    public string BestFor { get; set; } = string.Empty;

    [JsonPropertyName("waterGuidance")]
    public string WaterGuidance { get; set; } = string.Empty;

    [JsonPropertyName("phGuidance")]
    public string PhGuidance { get; set; } = string.Empty;

    [JsonPropertyName("ecGuidance")]
    public string EcGuidance { get; set; } = string.Empty;

    [JsonPropertyName("scheduleStyle")]
    public string ScheduleStyle { get; set; } = string.Empty;

    [JsonPropertyName("officialHighlights")]
    public string OfficialHighlights { get; set; } = string.Empty;

    [JsonPropertyName("practiceNotes")]
    public string PracticeNotes { get; set; } = string.Empty;

    [JsonPropertyName("stages")]
    public List<NutrientStage> Stages { get; set; } = [];

    [JsonPropertyName("tips")]
    public List<string> Tips { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    public List<string> SearchTerms { get; set; } = [];
}
