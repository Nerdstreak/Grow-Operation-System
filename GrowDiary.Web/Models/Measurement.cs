namespace GrowDiary.Web.Models;

public sealed class Measurement
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.Now;
    public GrowStage Stage { get; set; } = global::GrowDiary.Web.Models.GrowStage.Veg;
    public ValueOrigin Source { get; set; } = ValueOrigin.Manual;
    public string? Notes { get; set; }

    public double? AirTemperatureC { get; set; }
    public double? HumidityPercent { get; set; }
    public double? HeightCm { get; set; }

    public double? WaterAmountMl { get; set; }
    public double? RunoffAmountMl { get; set; }
    public double? IrrigationPh { get; set; }
    public double? IrrigationEc { get; set; }
    public double? DrainPh { get; set; }
    public double? DrainEc { get; set; }

    public double? ReservoirPh { get; set; }
    public double? ReservoirEc { get; set; }
    public double? ReservoirWaterTempC { get; set; }
    public double? ReservoirLevelCm { get; set; }
    public double? ReservoirLevelLiters { get; set; }
    public double? DissolvedOxygenMgL { get; set; }

    /// <summary>
    /// Luftstrom auf Blattniveau in m/min, mit dem Anemometer gemessen.
    /// </summary>
    /// <remarks>
    /// Gehört zum VPD dazu und wird trotzdem fast nie erfasst: der Luftstrom
    /// bricht die feuchte Grenzschicht am Blatt auf. Steht sie, misst das
    /// Hygrometer im Zelt einen Wert, den das Blatt gar nicht erlebt.
    /// Richtwerte aus der Wissensbasis (airflow-at-leaf-level): 60–90 m/min für
    /// die meisten Systeme, 90–120 für RDWC.
    /// </remarks>
    public double? AirflowAtLeafMPerMin { get; set; }

    /// <summary>
    /// Wie stark das Wasser im System zirkuliert.
    /// </summary>
    /// <remarks>
    /// Bewusst KEINE Zahl. Die Quelle (water-flow-moderate) sagt „moderat, nicht
    /// stark" und nennt keinen Durchsatz — ein Feld in L/min würde eine Genauigkeit
    /// vortäuschen, die es nicht gibt, und jeden dazu bringen, eine Zahl zu
    /// erfinden. Drei Stufen sind genau das, was sich beurteilen lässt.
    /// </remarks>
    public WaterFlowLevel? WaterFlow { get; set; }
    public double? OrpMv { get; set; }
    public double? TopOffLiters { get; set; }
    public double? AddbackEc { get; set; }
    public bool SolutionChange { get; set; }

    public double? PpfdMol { get; set; }
    public double? Co2Ppm { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
