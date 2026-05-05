using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// Eine einzelne Messung als API-Response.
/// </summary>
public sealed record MeasurementDto(
    int Id,
    int GrowId,
    DateTime TakenAt,
    GrowStage Stage,
    ValueOrigin Source,
    string? Notes,
    double? AirTemperatureC,
    double? HumidityPercent,
    double? HeightCm,
    double? WaterAmountMl,
    double? RunoffAmountMl,
    double? IrrigationPh,
    double? IrrigationEc,
    double? DrainPh,
    double? DrainEc,
    double? ReservoirPh,
    double? ReservoirEc,
    double? ReservoirWaterTempC,
    double? ReservoirLevelCm,
    double? ReservoirLevelLiters,
    double? DissolvedOxygenMgL,
    double? OrpMv,
    double? TopOffLiters,
    double? AddbackEc,
    bool SolutionChange,
    double? PpfdMol,
    double? Co2Ppm
);
