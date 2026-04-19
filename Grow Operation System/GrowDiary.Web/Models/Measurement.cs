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
    public double? OrpMv { get; set; }
    public double? TopOffLiters { get; set; }
    public double? AddbackEc { get; set; }
    public bool SolutionChange { get; set; }

    public double? PpfdMol { get; set; }
    public double? Co2Ppm { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
