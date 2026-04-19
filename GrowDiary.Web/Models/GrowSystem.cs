namespace GrowDiary.Web.Models;

public sealed class GrowSystem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HydroStyle { get; set; } = string.Empty;
    public int? PotCount { get; set; }
    public double? PotSizeLiters { get; set; }
    public double? ReservoirLiters { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; } = 99;
    public DateTime CreatedAtUtc { get; set; }
}
