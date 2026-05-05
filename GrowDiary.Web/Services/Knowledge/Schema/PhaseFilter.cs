using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class PhaseFilter
{
    [JsonPropertyName("allowed")]
    public List<string> Allowed { get; set; } = [];

    [JsonPropertyName("blocked")]
    public List<string> Blocked { get; set; } = [];

    [JsonPropertyName("blockAfterFlowerWeek")]
    public int? BlockAfterFlowerWeek { get; set; }
}
