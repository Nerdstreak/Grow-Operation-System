namespace GrowDiary.Web.Models;

public sealed class Tent
{
    public int Id { get; set; }

    /// <summary>Die Entität, in die der Wassertemperatur-Sollwert geschrieben wird.</summary>
    /// <remarks>
    /// Ein Thermostat (`climate.…`) oder ein Zahlenfeld (`number.…`,
    /// `input_number.…`). Bewusst getrennt vom Chiller-SENSOR: das eine liest,
    /// das andere stellt. Leer heisst: es wird nichts geschrieben.
    /// </remarks>
    public string? WaterTargetEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "Grow Tent";
    public TentType TentType { get; set; } = TentType.MultiPurpose;
    public TentStatus Status { get; set; } = TentStatus.Active;
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public string AccentColor { get; set; } = "#69b578";

    public int? WidthCm { get; set; }
    public int? DepthCm { get; set; }
    public int? TentHeightCm { get; set; }
    public string? LightType { get; set; }
    public int? LightWatt { get; set; }
    public LightControllerType? LightController { get; set; }
    public string? LightControllerEntityId { get; set; }
    public int? ExhaustFanCount { get; set; }
    public int? ExhaustM3h { get; set; }
    public int? CirculationFanCount { get; set; }
    public HvacControllerType? HvacController { get; set; }
    public string? HvacControllerEntityId { get; set; }
    public bool Co2Available { get; set; }

    /// <summary>
    /// Gibt es eine CO₂-ANREICHERUNG (Brenner, Flasche, Generator)?
    /// </summary>
    /// <remarks>
    /// Nicht dasselbe wie ein CO₂-Sensor: der misst nur. Ohne Anreicherung
    /// steht die Luft bei ~400–500 ppm, und ein Anreicherungsziel von
    /// 800–1400 ppm stuende fuer immer „daneben" — die Kachel bekommt dann
    /// kein Ziel statt ein unerreichbares.
    /// </remarks>
    public bool HasCo2Enrichment { get; set; }
    public string? CameraEntityId { get; set; }

    /// <summary>
    /// All camera entities for this tent, newline-separated (a tent can have several — e.g.
    /// one per plant). <see cref="CameraEntityId"/> mirrors the first one for backward
    /// compatibility (snapshot automation, camera-proxy default).
    /// </summary>
    public string? CameraEntityIds { get; set; }

    /// <summary>
    /// How many °C the leaf sits below air temperature, used for leaf VPD (0 = plain air VPD).
    ///
    /// Defaults to 2 °C, which is what the workshop material specifies for RDWC and what the
    /// reference VPD calculator uses in its worked example (air 28 °C, leaf 26 °C). It used
    /// to default to 0, so a new tent silently computed air VPD — a different number than
    /// the one every RDWC chart is drawn for.
    /// </summary>
    public double LeafTempOffsetC { get; set; } = DefaultLeafTempOffsetC;

    /// <summary>The documented RDWC leaf offset, per the workshop material and the Ben Green calculator.</summary>
    public const double DefaultLeafTempOffsetC = 2.0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int ActiveGrowCount { get; set; }
    public int ArchivedGrowCount { get; set; }
    public int ActiveSetupCount { get; set; }
    public int ArchivedSetupCount { get; set; }
    public List<GrowRun> ActiveGrows { get; set; } = new();
    public List<TentSensor> Sensors { get; set; } = new();
}
