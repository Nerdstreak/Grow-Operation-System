namespace GrowDiary.Web.Models;

public sealed class GrowRun
{
    public int Id { get; set; }
    public int? TentId { get; set; }

    /// <summary>
    /// Eigenes Sollwert-Profil für diesen Lauf; null heisst „vom System geerbt".
    /// </summary>
    /// <remarks>
    /// Sollwerte beschreiben, wie man DIESE Pflanze fährt — eine Sorte, die mehr
    /// verträgt, ein Versuch. Zwei Läufe im selben Becken dürfen abweichen.
    /// </remarks>
    public string? SetpointProfileId { get; set; }
    public int? SystemId { get; set; }
    public int? SetupId { get; set; }
    public string? TentName { get; set; }
    public string? HydroSetupName { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Verweis in die Sorten-Bibliothek. <see cref="Strain"/> und
    /// <see cref="Breeder"/> bleiben als Text daneben stehen: sie halten fest,
    /// was zum Zeitpunkt des Laufs galt, auch wenn die Sorte spaeter umbenannt
    /// oder geloescht wird.
    /// </summary>
    public int? StrainId { get; set; }
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

    /// <summary>
    /// Das Duengerprogramm dieses Laufs — die Id eines nutrient-programs aus dem
    /// Wissen (etwa <c>athena</c>), null heisst „keins gewaehlt".
    /// </summary>
    /// <remarks>
    /// Am Grow, nicht global: ein Vergleichslauf darf ein anderes Programm
    /// fahren. Traegt das Programm ein Wochen-Chart, kann der Mischplan beim
    /// Ansetzen konkrete Milliliter nennen.
    /// </remarks>
    public string? FeedProgramId { get; set; }
    public SeedType SeedType { get; set; } = SeedType.Feminized;
    public StartMaterial StartMaterial { get; set; } = StartMaterial.Seed;
    public GerminationMethod? GerminationMethod { get; set; }
    public string? CloneSource { get; set; }
    public bool CloneIsRooted { get; set; }
    /// <summary>
    /// Wie lange vegetativ gewachsen werden soll, in Tagen ab Startdatum.
    /// Die Absicht, nicht die Beobachtung: daraus ergibt sich der geplante
    /// Flip-Termin, solange <see cref="FlipDate"/> noch leer ist.
    /// </summary>
    public int? PlannedVegDays { get; set; }
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

    /// <summary>
    /// Wann der Saemling zur Veg wurde — beobachtet, nicht gerechnet.
    /// </summary>
    /// <remarks>
    /// Der Uebergang haengt nicht am Kalender, sondern am Aussehen: echte
    /// gezackte Blaetter statt der zwei runden Keimblaetter, dickerer Stengel,
    /// regelmaessig neue Blattpaare, Seitentriebe an den Knoten, spuerbar mehr
    /// Wasserverbrauch. Typisch ein bis drei Wochen nach der Keimung — aber eben
    /// typisch, nicht sicher.
    ///
    /// Solange hier nichts steht, schaetzt <see cref="Services.GrowStageResolver"/>
    /// ueber die Tage. Steht etwas, gewinnt es: wer hingesehen hat, weiss es
    /// besser als jede Rechnung. Dieselbe Regel wie beim Flip.
    /// </remarks>
    public DateTime? VegStartedAt { get; set; }

    /// <summary>
    /// Wann das Finish (Spuelen) begann — beobachtet, nicht gerechnet.
    /// </summary>
    /// <remarks>
    /// Real entscheidet der Blick auf die Trichome, nicht die Breeder-Wochen:
    /// milchig mit ersten bernsteinfarbenen heisst bald ernten, also spuelen.
    /// Ohne Eintrag schaetzt der Resolver weiter ueber die Breeder-Angabe.
    /// </remarks>
    public DateTime? FinishStartedAt { get; set; }
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
    // Latest NON-NULL reservoir values across all measurements, so a partial
    // auto-measurement (e.g. only temp/humidity) does not blank pH/EC in summaries.
    public double? LatestReservoirPh { get; set; }
    public double? LatestReservoirEc { get; set; }

    public GrowthProfile Profile => new(HydroStyle);

    public bool IsArchived => Status is GrowStatus.Completed or GrowStatus.Aborted;
}
