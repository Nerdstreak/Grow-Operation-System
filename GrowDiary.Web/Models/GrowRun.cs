namespace GrowDiary.Web.Models;

public sealed class GrowRun
{
    public int Id { get; set; }
    public int? TentId { get; set; }
    public int? SystemId { get; set; }
    public int? SetupId { get; set; }
    public string? TentName { get; set; }
    public string? HydroSetupName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Strain { get; set; }
    public string? Breeder { get; set; }
    public GrowStatus Status { get; set; } = GrowStatus.Planning;
    public MediumType MediumType { get; set; } = MediumType.Hydro;
    public FeedingStyle FeedingStyle { get; set; } = FeedingStyle.None;
    public HydroStyle HydroStyle { get; set; } = HydroStyle.None;
    public GrowEnvironment Environment { get; set; } = GrowEnvironment.Indoor;
    public string? Light { get; set; }
    public string? ContainerSize { get; set; }
    public string? ReservoirSize { get; set; }
    public string? MediumDetail { get; set; }
    public string? IrrigationStyle { get; set; }
    public IrrigationType IrrigationType { get; set; } = IrrigationType.ActiveHydro;
    public WaterSource WaterSource { get; set; } = WaterSource.Tap;
    public SeedType SeedType { get; set; } = SeedType.Feminized;
    public StartMaterial StartMaterial { get; set; } = StartMaterial.Seed;
    public GerminationMethod? GerminationMethod { get; set; }
    public string? CloneSource { get; set; }
    public bool CloneIsRooted { get; set; }
    public int? BreederFlowerWeeksMin { get; set; }
    public int? BreederFlowerWeeksMax { get; set; }
    public int? PlantCount { get; set; }
    public int? PhenoNumber { get; set; }
    public PropagationMedium? PropagationMedium { get; set; }
    public bool HasChiller { get; set; }
    public GrowEntryPoint EntryPoint { get; set; } = GrowEntryPoint.Germination;
    public int? DaysAlreadyInPhase { get; set; }
    public int? AutoflowerDaysSinceGermination { get; set; }
    public DateTime? FlipDate { get; set; }
    public DateTime? GerminatedAt { get; set; }
    public DateTime? RootedAt { get; set; }
    public string? Nutrients { get; set; }
    public string? Notes { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Immutable JSON snapshot of the tent at grow creation time.
    /// Used for export/comparison stability when the live tent changes later.
    /// </summary>
    public string? TentSnapshotJson { get; set; }

    /// <summary>
    /// Immutable JSON snapshot of the DWC/RDWC HydroSetup at grow creation time.
    /// Used for export/comparison stability when the live HydroSetup changes later.
    /// </summary>
    public string? HydroSetupSnapshotJson { get; set; }

    public DateTime? SnapshotsCapturedAtUtc { get; set; }

    public int MeasurementCount { get; set; }
    public string? LatestPhotoPath { get; set; }
    public Measurement? LatestMeasurement { get; set; }

    public GrowthProfile Profile => new(HydroStyle);

    public bool IsArchived => Status is GrowStatus.Completed or GrowStatus.Aborted;
}
