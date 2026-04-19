namespace GrowDiary.Web.Models;

public sealed class TentSensorDailyStat
{
    public int Id { get; set; }
    public int TentId { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Median { get; set; }   // P50
    public double P5 { get; set; }       // unteres Band
    public double P95 { get; set; }      // oberes Band
    public double Avg { get; set; }      // für Kompatibilität
    public int Count { get; set; }
    public string? Unit { get; set; }
}
