using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class MaintenanceEventMapping
{
    public static MaintenanceEventDto ToDto(this MaintenanceEvent item) => new(
        Id: item.Id,
        HardwareItemId: item.HardwareItemId,
        EventType: item.EventType,
        Status: item.Status,
        Result: item.Result,
        Title: item.Title,
        Description: item.Description,
        DueAtUtc: item.DueAtUtc,
        PerformedAtUtc: item.PerformedAtUtc,
        NextDueAtUtc: item.NextDueAtUtc,
        GrowTaskId: item.GrowTaskId,
        SopInstanceId: item.SopInstanceId,
        Notes: item.Notes,
        CreatedAtUtc: item.CreatedAtUtc,
        UpdatedAtUtc: item.UpdatedAtUtc
    );

    public static MaintenanceEvent ToModel(this CreateMaintenanceEventRequest request) => new()
    {
        HardwareItemId = request.HardwareItemId,
        EventType = request.EventType,
        Status = request.Status,
        Result = request.Result,
        Title = request.Title.Trim(),
        Description = NormalizeOptional(request.Description),
        DueAtUtc = request.DueAtUtc,
        PerformedAtUtc = request.PerformedAtUtc,
        NextDueAtUtc = request.NextDueAtUtc,
        SopInstanceId = request.SopInstanceId,
        Notes = NormalizeOptional(request.Notes)
    };

    public static void ApplyTo(this UpdateMaintenanceEventRequest request, MaintenanceEvent item)
    {
        item.HardwareItemId = request.HardwareItemId;
        item.EventType = request.EventType;
        item.Status = request.Status;
        item.Result = request.Result;
        item.Title = request.Title.Trim();
        item.Description = NormalizeOptional(request.Description);
        item.DueAtUtc = request.DueAtUtc;
        item.PerformedAtUtc = request.PerformedAtUtc;
        item.NextDueAtUtc = request.NextDueAtUtc;
        item.GrowTaskId = request.GrowTaskId;
        item.SopInstanceId = request.SopInstanceId;
        item.Notes = NormalizeOptional(request.Notes);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
