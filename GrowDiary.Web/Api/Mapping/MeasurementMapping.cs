using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class MeasurementMapping
{
    public static MeasurementDto ToDto(this Measurement measurement) => new(
        Id: measurement.Id,
        GrowId: measurement.GrowId,
        TakenAt: measurement.TakenAt,
        Stage: measurement.Stage,
        Source: measurement.Source,
        Notes: measurement.Notes,
        AirTemperatureC: measurement.AirTemperatureC,
        HumidityPercent: measurement.HumidityPercent,
        HeightCm: measurement.HeightCm,
        WaterAmountMl: measurement.WaterAmountMl,
        RunoffAmountMl: measurement.RunoffAmountMl,
        IrrigationPh: measurement.IrrigationPh,
        IrrigationEc: measurement.IrrigationEc,
        DrainPh: measurement.DrainPh,
        DrainEc: measurement.DrainEc,
        ReservoirPh: measurement.ReservoirPh,
        ReservoirEc: measurement.ReservoirEc,
        ReservoirWaterTempC: measurement.ReservoirWaterTempC,
        ReservoirLevelCm: measurement.ReservoirLevelCm,
        ReservoirLevelLiters: measurement.ReservoirLevelLiters,
        DissolvedOxygenMgL: measurement.DissolvedOxygenMgL,
        OrpMv: measurement.OrpMv,
        TopOffLiters: measurement.TopOffLiters,
        AddbackEc: measurement.AddbackEc,
        SolutionChange: measurement.SolutionChange,
        PpfdMol: measurement.PpfdMol,
        Co2Ppm: measurement.Co2Ppm
    );
}
