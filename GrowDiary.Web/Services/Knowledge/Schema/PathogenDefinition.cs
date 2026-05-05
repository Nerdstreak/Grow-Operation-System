using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class PathogenDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("scientificName")]
    public string? ScientificName { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("symptoms")]
    public List<string> Symptoms { get; set; } = [];

    [JsonPropertyName("treatable")]
    public bool Treatable { get; set; }

    [JsonPropertyName("treatmentSopId")]
    public string? TreatmentSopId { get; set; }

    [JsonPropertyName("preventiveSopId")]
    public string? PreventiveSopId { get; set; }

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public List<KnowledgeSource> Sources { get; set; } = [];
}
