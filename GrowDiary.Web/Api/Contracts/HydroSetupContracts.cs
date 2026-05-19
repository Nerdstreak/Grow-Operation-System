using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record HydroSetupDto(
    int Id,
    string Name,
    int? TentId,
    string? TentName,
    HydroStyle HydroStyle,
    int? PotCount,
    double? PotSizeLiters,
    double? ReservoirLiters,
    double? TotalVolumeLiters,
    HydroSetupLayoutType LayoutType,
    ReservoirPosition ReservoirPosition,
    HydroSetupStatus Status,
    bool HasCirculationPump,
    string? CirculationPumpNotes,
    bool HasAirPump,
    string? AirPumpNotes,
    int? AirStoneCount,
    bool HasChiller,
    bool HasUvSterilizer,
    string? Notes,
    int DisplayOrder,
    int ActiveGrowCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public class CreateHydroSetupRequest
{
    public int? TentId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public HydroStyle HydroStyle { get; set; } = HydroStyle.RDWC;
    public int? PotCount { get; set; }
    public double? PotSizeLiters { get; set; }
    public double? ReservoirLiters { get; set; }
    public HydroSetupLayoutType LayoutType { get; set; } = HydroSetupLayoutType.SingleBucket;
    public ReservoirPosition ReservoirPosition { get; set; } = ReservoirPosition.None;
    public bool HasCirculationPump { get; set; }
    public string? CirculationPumpNotes { get; set; }
    public bool HasAirPump { get; set; }
    public string? AirPumpNotes { get; set; }
    public int? AirStoneCount { get; set; }
    public bool HasChiller { get; set; }
    public bool HasUvSterilizer { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; } = 99;
}

public sealed class UpdateHydroSetupRequest : CreateHydroSetupRequest
{
    public HydroSetupStatus Status { get; set; } = HydroSetupStatus.Active;
}
