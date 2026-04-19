using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class ChartService
{
    public ChartSeries BuildSeries(string title, string yLabel, params (string Label, string Color, IEnumerable<(DateTime timestamp, double? value)> points)[] lines)
    {
        var series = new ChartSeries { Title = title, YLabel = yLabel };

        foreach (var line in lines)
        {
            var chartLine = new ChartLine
            {
                Label = line.Label,
                Color = line.Color,
                Points = line.points
                    .Where(x => x.value.HasValue)
                    .OrderBy(x => x.timestamp)
                    .Select(x => new ChartPoint { Timestamp = x.timestamp, Value = x.value!.Value })
                    .ToList()
            };

            if (chartLine.Points.Count > 0)
            {
                series.Lines.Add(chartLine);
            }
        }

        return series;
    }
}
