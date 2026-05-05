using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class SopTrigger
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("intervalDays")]
    public int? IntervalDays { get; set; }

    [JsonPropertyName("warningAfterDays")]
    public int? WarningAfterDays { get; set; }

    [JsonPropertyName("criticalAfterDays")]
    public int? CriticalAfterDays { get; set; }

    [JsonPropertyName("symptomTags")]
    public List<string>? SymptomTags { get; set; }
}
