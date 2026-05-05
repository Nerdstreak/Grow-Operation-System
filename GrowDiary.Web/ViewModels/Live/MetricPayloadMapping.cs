using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels.Live;

public static class MetricPayloadMapping
{
    public static MetricPayload ToPayload(this MetricCard metric)
        => new()
        {
            Key = metric.Key,
            Label = metric.Label,
            Value = metric.Value,
            Unit = metric.Unit,
            Tone = metric.Tone,
            Hint = metric.Hint
        };
}
