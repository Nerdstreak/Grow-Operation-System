using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class SetpointDefinition : KnowledgeFileMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("systemType")]
    public string SystemType { get; set; } = string.Empty;

    [JsonPropertyName("programKey")]
    public string? ProgramKey { get; set; }

    [JsonPropertyName("stages")]
    public Dictionary<string, StageSetpoints> Stages { get; set; } = [];
}
