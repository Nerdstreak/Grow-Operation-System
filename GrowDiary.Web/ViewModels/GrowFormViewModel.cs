using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GrowDiary.Web.ViewModels;

public sealed class GrowFormViewModel
{
    public int? Id { get; set; }
    public int? TemplateId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int? TentId { get; set; }
    public int? SystemId { get; set; }
    public List<SelectListItem> TentOptions { get; set; } = new();
    public List<GrowTemplate> Templates { get; set; } = new();

    // Schritt 1 – Genetik
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

    // Schritt 2 – System
    public HydroStyle HydroStyle { get; set; } = HydroStyle.RDWC;
    public int? PlantCount { get; set; }
    public string? ReservoirSize { get; set; }
    public string? ContainerSize { get; set; }
    public PropagationMedium? PropagationMedium { get; set; }
    public string? Light { get; set; }
    public bool HasChiller { get; set; }

    // Schritt 3 – Nährstoffe & Wasser
    public WaterSource WaterSource { get; set; } = WaterSource.RO;
    public string? Nutrients { get; set; }
    public List<string> NutrientSuggestions { get; set; } = new();

    // Schritt 4 – Einstiegspunkt
    [Required]
    public string StartDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public GrowEntryPoint EntryPoint { get; set; } = GrowEntryPoint.Germination;
    public int? DaysAlreadyInPhase { get; set; }
    public int? AutoflowerDaysSinceGermination { get; set; }
    public string? FlipDate { get; set; }
    public string? Notes { get; set; }

    // Feste Werte (RDWC/DWC-only App)
    public GrowStatus Status { get; set; } = GrowStatus.Planning;
    public GrowEnvironment Environment { get; set; } = GrowEnvironment.Indoor;
    public MediumType MediumType => MediumType.Hydro;
    public FeedingStyle FeedingStyle => FeedingStyle.None;
    public IrrigationType IrrigationType => IrrigationType.ActiveHydro;

    // Hilfseigenschaften
    public bool IsAutoflower => SeedType == SeedType.Autoflower;
    public bool NeedsFlipDate => !IsAutoflower && EntryPoint == GrowEntryPoint.Flower;
    public bool NeedsDaysInPhase => EntryPoint != GrowEntryPoint.Germination && !IsAutoflower;

    public static GrowFormViewModel FromGrow(GrowRun grow)
    {
        return new GrowFormViewModel
        {
            Id = grow.Id,
            TentId = grow.TentId,
            SystemId = grow.SystemId,
            Name = grow.Name,
            Strain = grow.Strain,
            Breeder = grow.Breeder,
            Status = grow.Status,
            HydroStyle = grow.HydroStyle == HydroStyle.None ? HydroStyle.RDWC : grow.HydroStyle,
            Environment = grow.Environment,
            Light = grow.Light,
            ContainerSize = grow.ContainerSize,
            ReservoirSize = grow.ReservoirSize,
            WaterSource = grow.WaterSource,
            SeedType = grow.SeedType,
            StartMaterial = grow.StartMaterial,
            GerminationMethod = grow.GerminationMethod,
            CloneSource = grow.CloneSource,
            CloneIsRooted = grow.CloneIsRooted,
            PhenoNumber = grow.PhenoNumber,
            BreederFlowerWeeksMin = grow.BreederFlowerWeeksMin,
            BreederFlowerWeeksMax = grow.BreederFlowerWeeksMax,
            PlantCount = grow.PlantCount,
            PropagationMedium = grow.PropagationMedium,
            HasChiller = grow.HasChiller,
            EntryPoint = grow.EntryPoint,
            DaysAlreadyInPhase = grow.DaysAlreadyInPhase,
            AutoflowerDaysSinceGermination = grow.AutoflowerDaysSinceGermination,
            FlipDate = grow.FlipDate?.ToString("yyyy-MM-dd"),
            Nutrients = grow.Nutrients,
            Notes = grow.Notes,
            StartDate = grow.StartDate.ToString("yyyy-MM-dd")
        };
    }

    public static GrowFormViewModel FromTemplate(GrowTemplate template)
    {
        return new GrowFormViewModel
        {
            TemplateId = template.Id,
            Name = template.Name,
            HydroStyle = template.HydroStyle == HydroStyle.None ? HydroStyle.RDWC : template.HydroStyle,
            Environment = template.Environment,
            Light = template.Light,
            ContainerSize = template.ContainerSize,
            ReservoirSize = template.ReservoirSize,
            Nutrients = template.Nutrients,
            Notes = template.Notes,
            Status = GrowStatus.Planning,
            WaterSource = WaterSource.RO
        };
    }

    public GrowRun ToGrow()
    {
        var run = new GrowRun
        {
            Id = Id ?? 0,
            TentId = TentId,
            SystemId = SystemId,
            Name = Name.Trim(),
            Strain = string.IsNullOrWhiteSpace(Strain) ? null : Strain.Trim(),
            Breeder = string.IsNullOrWhiteSpace(Breeder) ? null : Breeder.Trim(),
            Status = Status,
            MediumType = MediumType.Hydro,
            FeedingStyle = FeedingStyle.None,
            HydroStyle = HydroStyle,
            MediumDetail = HydroStyle.ToString(),
            Environment = Environment,
            Light = string.IsNullOrWhiteSpace(Light) ? null : Light.Trim(),
            ContainerSize = string.IsNullOrWhiteSpace(ContainerSize) ? null : ContainerSize.Trim(),
            ReservoirSize = string.IsNullOrWhiteSpace(ReservoirSize) ? null : ReservoirSize.Trim(),
            IrrigationStyle = null,
            IrrigationType = IrrigationType.ActiveHydro,
            WaterSource = WaterSource,
            SeedType = SeedType,
            StartMaterial = StartMaterial,
            GerminationMethod = StartMaterial == StartMaterial.Seed ? GerminationMethod : null,
            CloneSource = StartMaterial == StartMaterial.Clone ? (string.IsNullOrWhiteSpace(CloneSource) ? null : CloneSource.Trim()) : null,
            CloneIsRooted = StartMaterial == StartMaterial.Clone && CloneIsRooted,
            PhenoNumber = PhenoNumber,
            BreederFlowerWeeksMin = IsAutoflower ? null : BreederFlowerWeeksMin,
            BreederFlowerWeeksMax = IsAutoflower ? null : BreederFlowerWeeksMax,
            PlantCount = PlantCount,
            PropagationMedium = PropagationMedium,
            HasChiller = HasChiller,
            EntryPoint = EntryPoint,
            DaysAlreadyInPhase = NeedsDaysInPhase ? DaysAlreadyInPhase : null,
            AutoflowerDaysSinceGermination = IsAutoflower ? AutoflowerDaysSinceGermination : null,
            FlipDate = NeedsFlipDate && !string.IsNullOrWhiteSpace(FlipDate) ? DateTime.Parse(FlipDate) : null,
            Nutrients = string.IsNullOrWhiteSpace(Nutrients) ? null : Nutrients.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            StartDate = DateTime.Parse(StartDate)
        };

        // Steckling bereits bewurzelt: RootedAt auf StartDate setzen
        if (run.StartMaterial == StartMaterial.Clone
            && run.CloneIsRooted
            && run.RootedAt == null)
        {
            run.RootedAt = run.StartDate;
        }

        // Samen bereits in fortgeschrittener Phase: GerminatedAt setzen
        if (run.StartMaterial == StartMaterial.Seed
            && run.EntryPoint != GrowEntryPoint.Germination
            && run.GerminatedAt == null)
        {
            run.GerminatedAt = run.StartDate;
        }

        return run;
    }
}
