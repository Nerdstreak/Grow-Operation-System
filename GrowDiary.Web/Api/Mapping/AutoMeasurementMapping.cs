using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class AutoMeasurementMapping
{
    public static AutoMeasurementConfigDto ToDto(this AutoMeasurementConfig config) => new(
        Id: config.Id,
        GrowId: config.GrowId,
        TentId: config.TentId,
        Name: config.Name,
        Status: config.Status,
        TriggerKind: config.TriggerKind,
        DelayMinutes: config.DelayMinutes,
        WindowMinutes: config.WindowMinutes,
        CreatedAtUtc: config.CreatedAtUtc,
        UpdatedAtUtc: config.UpdatedAtUtc
    );

    public static AutoMeasurementConfig ToModel(this CreateAutoMeasurementConfigRequest request) => new()
    {
        GrowId = request.GrowId,
        TentId = request.TentId,
        Name = request.Name.Trim(),
        Status = request.Status,
        TriggerKind = request.TriggerKind,
        DelayMinutes = request.DelayMinutes,
        WindowMinutes = request.WindowMinutes
    };

    public static void ApplyTo(this UpdateAutoMeasurementConfigRequest request, AutoMeasurementConfig config)
    {
        config.TentId = request.TentId;
        config.Name = request.Name.Trim();
        config.Status = request.Status;
        config.TriggerKind = request.TriggerKind;
        config.DelayMinutes = request.DelayMinutes;
        config.WindowMinutes = request.WindowMinutes;
    }

    public static AutoMeasurementFieldMappingDto ToDto(this AutoMeasurementFieldMapping mapping) => new(
        Id: mapping.Id,
        ConfigId: mapping.ConfigId,
        MeasurementField: mapping.MeasurementField,
        MetricKey: mapping.MetricKey,
        Aggregation: mapping.Aggregation,
        IsRequired: mapping.IsRequired,
        CreatedAtUtc: mapping.CreatedAtUtc,
        UpdatedAtUtc: mapping.UpdatedAtUtc
    );

    public static AutoMeasurementFieldMapping ToModel(this AutoMeasurementFieldMappingUpsertRequest request) => new()
    {
        MeasurementField = request.MeasurementField,
        MetricKey = request.MetricKey.Trim(),
        Aggregation = request.Aggregation,
        IsRequired = request.IsRequired
    };

    public static AutoMeasurementRunDto ToDto(this AutoMeasurementRun run) => new(
        Id: run.Id,
        ConfigId: run.ConfigId,
        GrowId: run.GrowId,
        TriggerKind: run.TriggerKind,
        ScheduledForUtc: run.ScheduledForUtc,
        MeasurementId: run.MeasurementId,
        Status: run.Status,
        ErrorMessage: run.ErrorMessage,
        CreatedAtUtc: run.CreatedAtUtc,
        UpdatedAtUtc: run.UpdatedAtUtc
    );
}
