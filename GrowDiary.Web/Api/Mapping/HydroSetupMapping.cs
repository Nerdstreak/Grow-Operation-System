using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Api.Mapping;

public static class HydroSetupMapping
{
    public static HydroSetupDto ToDto(this GrowSystem system) => new(
        Id: system.Id,
        Name: system.Name,
        TentId: system.TentId,
        TentName: system.TentName,
        HydroStyle: ParseHydroStyle(system.HydroStyle),
        SetpointProfileId: system.SetpointProfileId,
        PotCount: system.PotCount,
        PotSizeLiters: system.PotSizeLiters,
        ReservoirLiters: system.ReservoirLiters,
        LevelSensorEmptyRaw: system.LevelSensorEmptyRaw,
        LevelSensorFullRaw: system.LevelSensorFullRaw,
        LevelSensorFullLiters: system.LevelSensorFullLiters,
        LevelCalibratedAtUtc: system.LevelCalibratedAtUtc,
        TotalVolumeLiters: BetriebsvolumenLiter(system),
        LayoutType: system.LayoutType,
        ReservoirPosition: system.ReservoirPosition,
        Status: system.Status,
        HasCirculationPump: system.HasCirculationPump,
        CirculationPumpNotes: system.CirculationPumpNotes,
        HasAirPump: system.HasAirPump,
        AirPumpNotes: system.AirPumpNotes,
        AirPumpLitersPerHour: system.AirPumpLitersPerHour,
        Aeration: AerationCheck.Beurteilen(
            system.AirPumpLitersPerHour,
            BetriebsvolumenLiter(system)),
        AirStoneCount: system.AirStoneCount,
        HasChiller: system.HasChiller,
        HasUvSterilizer: system.HasUvSterilizer,
        Notes: system.Notes,
        DisplayOrder: system.DisplayOrder,
        ActiveGrowCount: system.ActiveGrowCount,
        CreatedAtUtc: system.CreatedAtUtc,
        UpdatedAtUtc: system.UpdatedAtUtc
    );

    public static GrowSystem ToModel(this CreateHydroSetupRequest request) => new()
    {
        TentId = request.TentId,
        Name = request.Name.Trim(),
        HydroStyle = request.HydroStyle.ToString(),
        SetpointProfileId = string.IsNullOrWhiteSpace(request.SetpointProfileId) ? null : request.SetpointProfileId,
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
        AirPumpLitersPerHour = request.AirPumpLitersPerHour,
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
        SetpointProfileId = string.IsNullOrWhiteSpace(request.SetpointProfileId) ? null : request.SetpointProfileId,
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
        AirPumpLitersPerHour = request.AirPumpLitersPerHour,
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

    /// <summary>
    /// Das Betriebsvolumen eines Systems — gemessen schlägt geschätzt.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (01.09.2026).</b> Der Kalibrier-Assistent misst, was
    /// beim Füllen des <b>ganzen</b> Systems durch die Wasseruhr gelaufen ist
    /// — Töpfe, Rohre und Reservoir zusammen — und schreibt die Zahl nach
    /// <see cref="GrowSystem.ReservoirLiters"/>. Die Schätzung darüber rechnete
    /// das Topfvolumen dann <b>noch einmal</b> obendrauf: bei vier Töpfen à
    /// 20 L und gemessenen 160 L standen danach 240 L da — 50 % zu viel,
    /// obwohl der Nutzer gerade nachgemessen hatte.</para>
    ///
    /// <para>Wo gemessen wurde, gilt die Messung. Die Schätzung ist genau so
    /// lange richtig, wie niemand nachgesehen hat.</para>
    /// </remarks>
    public static double? BetriebsvolumenLiter(GrowSystem system)
        => system.LevelCalibratedAtUtc is not null && system.LevelSensorFullLiters is { } gemessen && gemessen > 0
            ? Math.Round(gemessen, 2)
            : CalculateTotalVolumeLiters(system.PotCount, system.PotSizeLiters, system.ReservoirLiters);

    private static HydroStyle ParseHydroStyle(string value)
        => Enum.TryParse<HydroStyle>(value, out var parsed) ? parsed : HydroStyle.None;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
