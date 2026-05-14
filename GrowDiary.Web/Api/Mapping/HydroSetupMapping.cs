using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class HydroSetupMapping
{
    public static HydroSetupDto ToDto(this GrowSystem system) => new(
        Id: system.Id,
        Name: system.Name,
        TentId: system.TentId,
        TentName: system.TentName,
        HydroStyle: ParseHydroStyle(system.HydroStyle),
        PotCount: system.PotCount,
        PotSizeLiters: system.PotSizeLiters,
        ReservoirLiters: system.ReservoirLiters,
        TotalVolumeLiters: CalculateTotalVolumeLiters(system.PotCount, system.PotSizeLiters, system.ReservoirLiters),
        LayoutType: system.LayoutType,
        ReservoirPosition: system.ReservoirPosition,
        Status: system.Status,
        HasCirculationPump: system.HasCirculationPump,
        CirculationPumpNotes: system.CirculationPumpNotes,
        HasAirPump: system.HasAirPump,
        AirPumpNotes: system.AirPumpNotes,
        AirStoneCount: system.AirStoneCount,
        HasChiller: system.HasChiller,
        HasUvSterilizer: system.HasUvSterilizer,
        Notes: system.Notes,
        DisplayOrder: system.DisplayOrder,
        CreatedAtUtc: system.CreatedAtUtc,
        UpdatedAtUtc: system.UpdatedAtUtc
    );

    public static GrowSystem ToModel(this CreateHydroSetupRequest request) => new()
    {
        TentId = request.TentId,
        Name = request.Name.Trim(),
        HydroStyle = request.HydroStyle.ToString(),
        PotCount = request.HydroStyle == HydroStyle.DWC ? request.PotCount ?? 1 : request.PotCount,
        PotSizeLiters = request.PotSizeLiters,
        ReservoirLiters = request.ReservoirLiters,
        Status = HydroSetupStatus.Active,
        LayoutType = request.HydroStyle == HydroStyle.DWC ? HydroSetupLayoutType.SingleBucket : request.LayoutType,
        ReservoirPosition = request.HydroStyle == HydroStyle.DWC ? ReservoirPosition.None : request.ReservoirPosition,
        HasCirculationPump = request.HasCirculationPump,
        CirculationPumpNotes = Normalize(request.CirculationPumpNotes),
        HasAirPump = request.HasAirPump,
        AirPumpNotes = Normalize(request.AirPumpNotes),
        AirStoneCount = request.AirStoneCount,
        HasChiller = request.HasChiller,
        HasUvSterilizer = request.HasUvSterilizer,
        Notes = Normalize(request.Notes),
        DisplayOrder = request.DisplayOrder
    };

    public static GrowSystem ToModel(this UpdateHydroSetupRequest request, int id, DateTime createdAtUtc) => new()
    {
        Id = id,
        TentId = request.TentId,
        Name = request.Name.Trim(),
        HydroStyle = request.HydroStyle.ToString(),
        PotCount = request.HydroStyle == HydroStyle.DWC ? request.PotCount ?? 1 : request.PotCount,
        PotSizeLiters = request.PotSizeLiters,
        ReservoirLiters = request.ReservoirLiters,
        Status = request.Status,
        LayoutType = request.HydroStyle == HydroStyle.DWC ? HydroSetupLayoutType.SingleBucket : request.LayoutType,
        ReservoirPosition = request.HydroStyle == HydroStyle.DWC ? ReservoirPosition.None : request.ReservoirPosition,
        HasCirculationPump = request.HasCirculationPump,
        CirculationPumpNotes = Normalize(request.CirculationPumpNotes),
        HasAirPump = request.HasAirPump,
        AirPumpNotes = Normalize(request.AirPumpNotes),
        AirStoneCount = request.AirStoneCount,
        HasChiller = request.HasChiller,
        HasUvSterilizer = request.HasUvSterilizer,
        Notes = Normalize(request.Notes),
        DisplayOrder = request.DisplayOrder,
        CreatedAtUtc = createdAtUtc
    };

    public static double? CalculateTotalVolumeLiters(int? potCount, double? potSizeLiters, double? reservoirLiters)
    {
        var total = (potCount ?? 0) * (potSizeLiters ?? 0) + (reservoirLiters ?? 0);
        return total > 0 ? Math.Round(total, 2) : null;
    }

    private static HydroStyle ParseHydroStyle(string value)
        => Enum.TryParse<HydroStyle>(value, out var parsed) ? parsed : HydroStyle.None;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
