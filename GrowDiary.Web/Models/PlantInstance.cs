namespace GrowDiary.Web.Models;

public sealed class PlantInstance
{
    public int Id { get; set; }
    public int? StrainId { get; set; }
    public int? SetupId { get; set; }
    public int? GrowId { get; set; }
    public int? ParentPlantId { get; set; }
    public string Label { get; set; } = string.Empty;
    public PlantRole PlantRole { get; set; } = PlantRole.Production;
    public PlantStatus PlantStatus { get; set; } = PlantStatus.Planned;
    public string? PhenoLabel { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Notes { get; set; }
    public string? StrainName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
