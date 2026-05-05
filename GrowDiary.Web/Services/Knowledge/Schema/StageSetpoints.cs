using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class StageSetpoints
{
    [JsonPropertyName("phMin")]
    public double PhMin { get; set; }

    [JsonPropertyName("phMax")]
    public double PhMax { get; set; }

    [JsonPropertyName("ecMin")]
    public double EcMin { get; set; }

    [JsonPropertyName("ecMax")]
    public double EcMax { get; set; }

    [JsonPropertyName("orpMin")]
    public double OrpMin { get; set; }

    [JsonPropertyName("orpMax")]
    public double OrpMax { get; set; }

    [JsonPropertyName("waterTempDayC")]
    public double WaterTempDayC { get; set; }

    [JsonPropertyName("waterTempNightC")]
    public double WaterTempNightC { get; set; }

    [JsonPropertyName("vpdMin")]
    public double VpdMin { get; set; }

    [JsonPropertyName("vpdMax")]
    public double VpdMax { get; set; }

    [JsonPropertyName("ppfdMin")]
    public double PpfdMin { get; set; }

    [JsonPropertyName("ppfdMax")]
    public double PpfdMax { get; set; }

    [JsonPropertyName("co2Min")]
    public double Co2Min { get; set; }

    [JsonPropertyName("co2Max")]
    public double Co2Max { get; set; }
}
