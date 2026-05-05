using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public abstract class KnowledgeFileMetadata
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
