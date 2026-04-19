using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public static class PercentileCalculator
{
    /// <summary>
    /// Berechnet ein Perzentil aus einer sortierten Liste via linearer Interpolation.
    /// </summary>
    public static double Calculate(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            throw new ArgumentException("Keine Werte", nameof(sortedValues));
        if (sortedValues.Count == 1)
            return sortedValues[0];

        var index    = (percentile / 100.0) * (sortedValues.Count - 1);
        var lower    = (int)Math.Floor(index);
        var upper    = (int)Math.Ceiling(index);
        var fraction = index - lower;

        return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
    }

    public static TentSensorDailyStat ComputeStats(
        int tentId, string metricKey,
        DateOnly date, IReadOnlyList<double> values, string? unit)
    {
        if (values.Count == 0)
            throw new ArgumentException("Keine Werte", nameof(values));

        var sorted = values.OrderBy(v => v).ToList();
        return new TentSensorDailyStat
        {
            TentId    = tentId,
            MetricKey = metricKey,
            Date      = date,
            Min       = sorted[0],
            Max       = sorted[^1],
            Median    = Calculate(sorted, 50),
            P5        = Calculate(sorted, 5),
            P95       = Calculate(sorted, 95),
            Avg       = sorted.Average(),
            Count     = sorted.Count,
            Unit      = unit
        };
    }
}
