using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class GrowUpsertRequest
{
    public int? TemplateId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int? TentId { get; set; }
    public int? SystemId { get; set; }
    public string? Strain { get; set; }
    public string? Breeder { get; set; }
    public SeedType SeedType { get; set; } = SeedType.Feminized;
    public StartMaterial StartMaterial { get; set; } = StartMaterial.Seed;
    public GerminationMethod? GerminationMethod { get; set; }
    public string? CloneSource { get; set; }
    public bool CloneIsRooted { get; set; }
    public int? PhenoNumber { get; set; }
    public int? BreederFlowerWeeksMin { get; set; }
    public int? BreederFlowerWeeksMax { get; set; }
    public HydroStyle HydroStyle { get; set; } = HydroStyle.RDWC;
    public int? PlantCount { get; set; }
    public string? ReservoirSize { get; set; }
    public string? ContainerSize { get; set; }
    public PropagationMedium? PropagationMedium { get; set; }
    public string? Light { get; set; }
    public bool HasChiller { get; set; }
    public WaterSource WaterSource { get; set; } = WaterSource.RO;
    public string? Nutrients { get; set; }

    [Required]
    public string StartDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    public GrowEntryPoint EntryPoint { get; set; } = GrowEntryPoint.Germination;
    public int? DaysAlreadyInPhase { get; set; }
    public int? AutoflowerDaysSinceGermination { get; set; }
    public string? FlipDate { get; set; }
    public string? Notes { get; set; }
    public GrowStatus Status { get; set; } = GrowStatus.Planning;
    public GrowEnvironment Environment { get; set; } = GrowEnvironment.Indoor;
}
