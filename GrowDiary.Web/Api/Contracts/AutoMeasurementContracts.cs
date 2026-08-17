using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record AutoMeasurementConfigDto(
    int Id,
    int GrowId,
    int? TentId,
    string Name,
    AutoMeasurementStatus Status,
    AutoMeasurementTriggerKind TriggerKind,
    int? DelayMinutes,
    int WindowMinutes,
    bool CaptureSnapshot,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreateAutoMeasurementConfigRequest
{
    public int GrowId { get; set; }
    public int? TentId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public AutoMeasurementStatus Status { get; set; } = AutoMeasurementStatus.Enabled;
    public AutoMeasurementTriggerKind TriggerKind { get; set; } = AutoMeasurementTriggerKind.Manual;
    public int? DelayMinutes { get; set; }
    public int WindowMinutes { get; set; } = 20;
    public bool CaptureSnapshot { get; set; }
}

public sealed class UpdateAutoMeasurementConfigRequest
{
    public int? TentId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public AutoMeasurementStatus Status { get; set; } = AutoMeasurementStatus.Enabled;
    public AutoMeasurementTriggerKind TriggerKind { get; set; } = AutoMeasurementTriggerKind.Manual;
    public int? DelayMinutes { get; set; }
    public int WindowMinutes { get; set; } = 20;
    public bool CaptureSnapshot { get; set; }
}

public sealed record AutoMeasurementFieldMappingDto(
    int Id,
    int ConfigId,
    AutoMeasurementField MeasurementField,
    string MetricKey,
    AutoMeasurementAggregation Aggregation,
    bool IsRequired,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class AutoMeasurementFieldMappingUpsertRequest
{
    public AutoMeasurementField MeasurementField { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public AutoMeasurementAggregation Aggregation { get; set; } = AutoMeasurementAggregation.Latest;
    public bool IsRequired { get; set; }
}

public sealed class ReplaceAutoMeasurementFieldMappingsRequest
{
    public List<AutoMeasurementFieldMappingUpsertRequest> Mappings { get; set; } = [];
}

