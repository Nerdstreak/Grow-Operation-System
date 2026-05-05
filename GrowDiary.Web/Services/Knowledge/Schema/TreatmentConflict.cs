using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class TreatmentConflict
{
    [JsonPropertyName("with")]
    public string With { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("minimumGapHours")]
    public int MinimumGapHours { get; set; }
}
