using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class CalibrationEventMapping
{
    public static CalibrationEventDto ToDto(this CalibrationEvent item) => new(
        Id: item.Id,
        HardwareItemId: item.HardwareItemId,
        CalibrationType: item.CalibrationType,
        Status: item.Status,
        Result: item.Result,
        Title: item.Title,
        ReferenceSolution: item.ReferenceSolution,
        ReferenceValue: item.ReferenceValue,
        BeforeValue: item.BeforeValue,
        AfterValue: item.AfterValue,
        TemperatureC: item.TemperatureC,
        DueAtUtc: item.DueAtUtc,
        PerformedAtUtc: item.PerformedAtUtc,
        NextDueAtUtc: item.NextDueAtUtc,
        GrowTaskId: item.GrowTaskId,
        Notes: item.Notes,
        CreatedAtUtc: item.CreatedAtUtc,
        UpdatedAtUtc: item.UpdatedAtUtc
    );

    public static CalibrationEvent ToModel(this CreateCalibrationEventRequest request) => new()
    {
        HardwareItemId = request.HardwareItemId,
        CalibrationType = request.CalibrationType,
        Status = request.Status,
        Result = request.Result,
        Title = request.Title.Trim(),
        ReferenceSolution = NormalizeOptional(request.ReferenceSolution),
        ReferenceValue = request.ReferenceValue,
        BeforeValue = request.BeforeValue,
        AfterValue = request.AfterValue,
        TemperatureC = request.TemperatureC,
        DueAtUtc = request.DueAtUtc,
        PerformedAtUtc = request.PerformedAtUtc,
        NextDueAtUtc = request.NextDueAtUtc,
        Notes = NormalizeOptional(request.Notes)
    };

    public static void ApplyTo(this UpdateCalibrationEventRequest request, CalibrationEvent item)
    {
        item.HardwareItemId = request.HardwareItemId;
        item.CalibrationType = request.CalibrationType;
        item.Status = request.Status;
        item.Result = request.Result;
        item.Title = request.Title.Trim();
        item.ReferenceSolution = NormalizeOptional(request.ReferenceSolution);
        item.ReferenceValue = request.ReferenceValue;
        item.BeforeValue = request.BeforeValue;
        item.AfterValue = request.AfterValue;
        item.TemperatureC = request.TemperatureC;
        item.DueAtUtc = request.DueAtUtc;
        item.PerformedAtUtc = request.PerformedAtUtc;
        item.NextDueAtUtc = request.NextDueAtUtc;
        item.GrowTaskId = request.GrowTaskId;
        item.Notes = NormalizeOptional(request.Notes);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
