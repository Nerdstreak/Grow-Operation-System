using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class SopDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("intervalDays")]
    public int? IntervalDays { get; set; }

    [JsonPropertyName("durationDays")]
    public int? DurationDays { get; set; }

    [JsonPropertyName("estimatedDurationMinutes")]
    public int? EstimatedDurationMinutes { get; set; }

    [JsonPropertyName("applicableSetups")]
    public List<string> ApplicableSetups { get; set; } = [];

    [JsonPropertyName("triggers")]
    public List<SopTrigger> Triggers { get; set; } = [];

    [JsonPropertyName("requiredMaterials")]
    public List<string> RequiredMaterials { get; set; } = [];

    [JsonPropertyName("steps")]
    public List<SopStepDefinition> Steps { get; set; } = [];

    [JsonPropertyName("sources")]
    public List<KnowledgeSource> Sources { get; set; } = [];
}
