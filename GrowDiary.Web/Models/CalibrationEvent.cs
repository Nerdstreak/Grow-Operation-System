namespace GrowDiary.Web.Models;

public sealed class CalibrationEvent
{
    public int Id { get; set; }
    public int HardwareItemId { get; set; }
    public CalibrationEventType CalibrationType { get; set; } = CalibrationEventType.Ph;
    public CalibrationEventStatus Status { get; set; } = CalibrationEventStatus.Planned;
    public CalibrationResult Result { get; set; } = CalibrationResult.Unknown;
    public string Title { get; set; } = string.Empty;
    public string? ReferenceSolution { get; set; }
    public decimal? ReferenceValue { get; set; }
    public decimal? BeforeValue { get; set; }
    public decimal? AfterValue { get; set; }
    public decimal? TemperatureC { get; set; }

    /// <summary>
    /// Die einzelnen Abgleiche als JSON — pH 4 und pH 7, oft auch 10.
    /// </summary>
    /// <remarks>
    /// <para>Die Zusammenfassung steht weiter in <see cref="ReferenceValue"/>,
    /// <see cref="BeforeValue"/> und <see cref="AfterValue"/>: ältere
    /// Kalibrierungen ohne Punkte bleiben damit lesbar, und Auswertungen
    /// rechnen mit einer Zahl weiter.</para>
    ///
    /// <para>Dasselbe Muster wie <c>HarvestEntry.PlantWeightsJson</c>. Erst aus
    /// zwei Punkten ergibt sich die Steilheit — siehe
    /// <c>Kalibrierpunkte.SteilheitProzent</c>.</para>
    /// </remarks>
    public string? PointsJson { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? NextDueAtUtc { get; set; }
    public int? GrowTaskId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
