using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class TreatmentDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("targetSymptoms")]
    public List<string> TargetSymptoms { get; set; } = [];

    [JsonPropertyName("dosage")]
    public TreatmentDosage Dosage { get; set; } = new();

    [JsonPropertyName("application")]
    public TreatmentApplication Application { get; set; } = new();

    [JsonPropertyName("restrictions")]
    public List<string> Restrictions { get; set; } = [];

    [JsonPropertyName("conflicts")]
    public List<TreatmentConflict> Conflicts { get; set; } = [];

    [JsonPropertyName("phaseFilter")]
    public PhaseFilter? PhaseFilter { get; set; }

    [JsonPropertyName("hardwareRequirements")]
    public List<string> HardwareRequirements { get; set; } = [];

    [JsonPropertyName("sources")]
    public List<KnowledgeSource> Sources { get; set; } = [];

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    [JsonPropertyName("expectedTimeToEffect")]
    public string ExpectedTimeToEffect { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}
