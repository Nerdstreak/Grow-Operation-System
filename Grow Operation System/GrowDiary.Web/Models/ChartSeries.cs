namespace GrowDiary.Web.Models;

public sealed class ChartSeries
{
    public string Title { get; set; } = string.Empty;
    public string EmptyText { get; set; } = "Noch keine Verlaufsdaten";
    public string YLabel { get; set; } = string.Empty;
    public List<ChartLine> Lines { get; set; } = new();
}

public sealed class ChartLine
{
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "var(--chart-1)";
    public List<ChartPoint> Points { get; set; } = new();
}

public sealed class ChartPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}
