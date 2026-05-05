using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class TreatmentDosage
{
    [JsonPropertyName("standard")]
    public string Standard { get; set; } = string.Empty;

    [JsonPropertyName("severe")]
    public string? Severe { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }
}
