using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class TreatmentApplication
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("timing")]
    public string? Timing { get; set; }

    [JsonPropertyName("frequency")]
    public string? Frequency { get; set; }

    [JsonPropertyName("durationStandard")]
    public string? DurationStandard { get; set; }

    [JsonPropertyName("durationSevere")]
    public string? DurationSevere { get; set; }
}
