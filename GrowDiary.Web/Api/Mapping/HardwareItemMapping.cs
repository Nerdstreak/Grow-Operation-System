using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class HardwareItemMapping
{
    public static HardwareItemDto ToDto(this HardwareItem item) => new(
        Id: item.Id,
        Name: item.Name,
        Category: item.Category,
        Status: item.Status,
        Criticality: item.Criticality,
        TentId: item.TentId,
        SetupId: item.SetupId,
        HydroSetupId: item.HydroSetupId,
        GrowId: item.GrowId,
        WearTemplateId: item.WearTemplateId,
        TentSensorId: item.TentSensorId,
        HaEntityId: item.HaEntityId,
        Manufacturer: item.Manufacturer,
        Model: item.Model,
        SerialNumber: item.SerialNumber,
        InstalledAtUtc: item.InstalledAtUtc,
        RetiredAtUtc: item.RetiredAtUtc,
        ExpectedLifespanDays: item.ExpectedLifespanDays,
        InspectionIntervalDays: item.InspectionIntervalDays,
        CalibrationIntervalDays: item.CalibrationIntervalDays,
        Notes: item.Notes,
        CreatedAtUtc: item.CreatedAtUtc,
        UpdatedAtUtc: item.UpdatedAtUtc
    );

    public static HardwareItem ToModel(this CreateHardwareItemRequest request) => new()
    {
        Name = request.Name?.Trim() ?? string.Empty,
        Category = request.Category?.Trim() ?? string.Empty,
        Status = request.Status,
        Criticality = request.Criticality,
        TentId = request.TentId,
        SetupId = request.SetupId,
        HydroSetupId = request.HydroSetupId,
        GrowId = request.GrowId,
        WearTemplateId = NormalizeOptional(request.WearTemplateId),
        TentSensorId = request.TentSensorId,
        HaEntityId = NormalizeOptional(request.HaEntityId),
        Manufacturer = NormalizeOptional(request.Manufacturer),
        Model = NormalizeOptional(request.Model),
        SerialNumber = NormalizeOptional(request.SerialNumber),
        InstalledAtUtc = request.InstalledAtUtc,
        RetiredAtUtc = request.RetiredAtUtc,
        ExpectedLifespanDays = request.ExpectedLifespanDays,
        InspectionIntervalDays = request.InspectionIntervalDays,
        CalibrationIntervalDays = request.CalibrationIntervalDays,
        Notes = NormalizeOptional(request.Notes)
    };

    public static void ApplyTo(this UpdateHardwareItemRequest request, HardwareItem item)
    {
        item.Name = request.Name.Trim();
        item.Category = request.Category.Trim();
        item.Status = request.Status;
        item.Criticality = request.Criticality;
        item.TentId = request.TentId;
        item.SetupId = request.SetupId;
        item.HydroSetupId = request.HydroSetupId;
        item.GrowId = request.GrowId;
        item.WearTemplateId = NormalizeOptional(request.WearTemplateId);
        item.TentSensorId = request.TentSensorId;
        item.HaEntityId = NormalizeOptional(request.HaEntityId);
        item.Manufacturer = NormalizeOptional(request.Manufacturer);
        item.Model = NormalizeOptional(request.Model);
        item.SerialNumber = NormalizeOptional(request.SerialNumber);
        item.InstalledAtUtc = request.InstalledAtUtc;
        item.RetiredAtUtc = request.RetiredAtUtc;
        item.ExpectedLifespanDays = request.ExpectedLifespanDays;
        item.InspectionIntervalDays = request.InspectionIntervalDays;
        item.CalibrationIntervalDays = request.CalibrationIntervalDays;
        item.Notes = NormalizeOptional(request.Notes);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
