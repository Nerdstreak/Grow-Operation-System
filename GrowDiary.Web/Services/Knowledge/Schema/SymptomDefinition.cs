using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class SymptomDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("possibleCauses")]
    public List<string> PossibleCauses { get; set; } = [];

    [JsonPropertyName("suggestedTreatmentIds")]
    public List<string> SuggestedTreatmentIds { get; set; } = [];

    [JsonPropertyName("suggestedSopIds")]
    public List<string> SuggestedSopIds { get; set; } = [];

    [JsonPropertyName("diagnosticChecks")]
    public List<string> DiagnosticChecks { get; set; } = [];
}
