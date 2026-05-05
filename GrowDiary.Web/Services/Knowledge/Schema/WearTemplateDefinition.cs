using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class WearTemplateDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("expectedLifespanDays")]
    public int ExpectedLifespanDays { get; set; }

    [JsonPropertyName("replacementTriggers")]
    public List<string> ReplacementTriggers { get; set; } = [];

    [JsonPropertyName("inspectionIntervalDays")]
    public int? InspectionIntervalDays { get; set; }
}
