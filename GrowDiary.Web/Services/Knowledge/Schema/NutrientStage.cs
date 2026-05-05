using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class NutrientStage
{
    [JsonPropertyName("stage")]
    public string Stage { get; set; } = string.Empty;

    [JsonPropertyName("dose")]
    public string Dose { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
