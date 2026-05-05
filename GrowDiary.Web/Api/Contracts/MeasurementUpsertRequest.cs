using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class MeasurementUpsertRequest
{
    [Required]
    public string TakenAtLocal { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");

    public GrowStage Stage { get; set; } = GrowStage.Veg;
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
}
