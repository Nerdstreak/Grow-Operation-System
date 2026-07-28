namespace GrowDiary.Web.Models;

public sealed class GrowSystem
{
    public int Id { get; set; }
    public int? TentId { get; set; }
    public string? TentName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HydroStyle { get; set; } = string.Empty;

    /// <summary>
    /// Sollwert-Profil dieses Systems; null heisst „nach Anbaustil".
    /// </summary>
    /// <remarks>
    /// Der Standard für jeden Grow darin: DWC oder RDWC ist eine Eigenschaft
    /// der Hardware, also einmal hier einstellen statt bei jedem Lauf neu.
    /// </remarks>
    public string? SetpointProfileId { get; set; }
    public int? PotCount { get; set; }
    public double? PotSizeLiters { get; set; }
    public double? ReservoirLiters { get; set; }

    /// <summary>
    /// Sensorwert bei LEEREM System — der Nullpunkt des Pegelsensors.
    /// </summary>
    /// <remarks>
    /// Ein eTape beginnt erst ein Stueck ueber der Unterkante zu messen und
    /// zeigt leer keine Null. Ohne diesen Punkt liefe die Umrechnung durch den
    /// Ursprung und waere unten am staerksten daneben — genau dort, wo der
    /// Fuellstand zaehlt.
    /// </remarks>
    public double? LevelSensorEmptyRaw { get; set; }

    /// <summary>Sensorwert bei VOLLEM System.</summary>
    public double? LevelSensorFullRaw { get; set; }

    /// <summary>
    /// Wie viel beim Fuellen wirklich hineinging — abgelesen an der Wasseruhr.
    /// </summary>
    /// <remarks>
    /// Bewusst getrennt von <see cref="ReservoirLiters"/>: das ist die Angabe
    /// aus dem Datenblatt oder der Schaetzung des Nutzers, dies hier ist ein
    /// gemessener Wert. Nur der gemessene taugt zum Umrechnen.
    /// </remarks>
    public double? LevelSensorFullLiters { get; set; }

    /// <summary>Wann zuletzt kalibriert wurde — damit man sieht, wie alt die Gerade ist.</summary>
    public DateTime? LevelCalibratedAtUtc { get; set; }
    public HydroSetupStatus Status { get; set; } = HydroSetupStatus.Active;
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
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int ActiveGrowCount { get; set; }
}
