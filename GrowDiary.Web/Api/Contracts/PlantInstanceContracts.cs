using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed record PlantInstanceDto(
    int Id,
    int? StrainId,
    int? SetupId,
    int? GrowId,
    int? ParentPlantId,
    string Label,
    PlantRole PlantRole,
    PlantStatus PlantStatus,
    string? PhenoLabel,
    DateTime? StartedAt,
    DateTime? EndedAt,
    string? Notes,
    string? StrainName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed class CreatePlantInstanceRequest
{
    public int? StrainId { get; set; }
    public int? SetupId { get; set; }
    public int? GrowId { get; set; }
    public int? ParentPlantId { get; set; }

    [Required]
    public string Label { get; set; } = string.Empty;

    public PlantRole PlantRole { get; set; } = PlantRole.Production;
    public PlantStatus PlantStatus { get; set; } = PlantStatus.Planned;
    public string? PhenoLabel { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdatePlantInstanceRequest
{
    public int? StrainId { get; set; }
    public int? SetupId { get; set; }
    public int? GrowId { get; set; }
    public int? ParentPlantId { get; set; }

    [Required]
    public string Label { get; set; } = string.Empty;

    public PlantRole PlantRole { get; set; } = PlantRole.Production;
    public PlantStatus PlantStatus { get; set; } = PlantStatus.Planned;
    public string? PhenoLabel { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Notes { get; set; }
}
