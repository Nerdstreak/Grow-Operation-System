using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.ViewModels;

public sealed class MeasurementFormViewModel
{
    public int? Id { get; set; }
    public int GrowId { get; set; }
    public string GrowName { get; set; } = string.Empty;
    public MediumType MediumType { get; set; }
    public FeedingStyle FeedingStyle { get; set; }
    public HydroStyle HydroStyle { get; set; }
    public string? MediumDetail { get; set; }
    public string? IrrigationStyle { get; set; }
    public string TakenAtLocal { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
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

    // Welche Felder wurden von HA vorausgefüllt (für Badge im View)
    public HashSet<string> HaPrefilled { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PhotoTag PhotoTag { get; set; } = PhotoTag.Overview;
    public string? PhotoCaption { get; set; }
    public bool UseAsReferenceShot { get; set; }
    public List<IFormFile> Photos { get; set; } = new();
    public List<PhotoAsset> ExistingPhotos { get; set; } = new();

    public GrowthProfile Profile => new(HydroStyle);

    public static MeasurementFormViewModel ForGrow(GrowRun grow)
    {
        return new MeasurementFormViewModel
        {
            GrowId = grow.Id,
            GrowName = grow.Name,
            MediumType = grow.MediumType,
            FeedingStyle = grow.FeedingStyle,
            HydroStyle = grow.HydroStyle,
            MediumDetail = grow.MediumDetail,
            IrrigationStyle = grow.IrrigationStyle
        };
    }

    public static MeasurementFormViewModel ForGrowWithHa(
        GrowRun grow,
        Dictionary<string, HomeAssistantState> haStates)
    {
        var vm = ForGrow(grow);

        void Fill(string key, Action<double> setter)
        {
            if (haStates.TryGetValue(key, out var state) && state.NumericValue.HasValue)
            {
                setter(Math.Round(state.NumericValue.Value, 3));
                vm.HaPrefilled.Add(key);
            }
        }

        Fill("temperature",      v => vm.AirTemperatureC             = v);
        Fill("humidity",         v => vm.HumidityPercent             = v);
        Fill("reservoir-ph",     v => vm.ReservoirPh                 = v);
        Fill("reservoir-ec",     v => vm.ReservoirEc                 = v);
        Fill("reservoir-temp",   v => vm.ReservoirWaterTempC         = v);
        Fill("reservoir-level",  v => vm.ReservoirLevelLiters        = v);
        Fill("orp",              v => vm.OrpMv                       = v);
        Fill("dissolved-oxygen", v => vm.DissolvedOxygenMgL          = v);
        Fill("co2",              v => vm.Co2Ppm                      = v);
        Fill("ppfd",             v => vm.PpfdMol                     = v);

        return vm;
    }

    public static MeasurementFormViewModel FromMeasurement(GrowRun grow, Measurement measurement, IEnumerable<PhotoAsset>? existingPhotos = null)
    {
        var firstPhoto = existingPhotos?.FirstOrDefault();
        return new MeasurementFormViewModel
        {
            Id = measurement.Id,
            GrowId = grow.Id,
            GrowName = grow.Name,
            MediumType = grow.MediumType,
            FeedingStyle = grow.FeedingStyle,
            HydroStyle = grow.HydroStyle,
            MediumDetail = grow.MediumDetail,
            IrrigationStyle = grow.IrrigationStyle,
            TakenAtLocal = measurement.TakenAt.ToString("yyyy-MM-ddTHH:mm"),
            Stage = measurement.Stage,
            Source = measurement.Source,
            Notes = measurement.Notes,
            AirTemperatureC = measurement.AirTemperatureC,
            HumidityPercent = measurement.HumidityPercent,
            HeightCm = measurement.HeightCm,
            WaterAmountMl = measurement.WaterAmountMl,
            RunoffAmountMl = measurement.RunoffAmountMl,
            IrrigationPh = measurement.IrrigationPh,
            IrrigationEc = measurement.IrrigationEc,
            DrainPh = measurement.DrainPh,
            DrainEc = measurement.DrainEc,
            ReservoirPh = measurement.ReservoirPh,
            ReservoirEc = measurement.ReservoirEc,
            ReservoirWaterTempC = measurement.ReservoirWaterTempC,
            ReservoirLevelCm = measurement.ReservoirLevelCm,
            ReservoirLevelLiters = measurement.ReservoirLevelLiters,
            DissolvedOxygenMgL = measurement.DissolvedOxygenMgL,
            OrpMv = measurement.OrpMv,
            TopOffLiters = measurement.TopOffLiters,
            AddbackEc = measurement.AddbackEc,
            SolutionChange = measurement.SolutionChange,
            PpfdMol = measurement.PpfdMol,
            Co2Ppm = measurement.Co2Ppm,
            PhotoTag = firstPhoto?.Tag ?? PhotoTag.Overview,
            PhotoCaption = firstPhoto?.Caption,
            UseAsReferenceShot = firstPhoto?.IsReferenceShot ?? false,
            ExistingPhotos = existingPhotos?.ToList() ?? new List<PhotoAsset>()
        };
    }

    public Measurement ToMeasurement()
    {
        return new Measurement
        {
            Id = Id ?? 0,
            GrowId = GrowId,
            TakenAt = DateTime.Parse(TakenAtLocal),
            Stage = Stage,
            Source = Source,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            AirTemperatureC = AirTemperatureC,
            HumidityPercent = HumidityPercent,
            HeightCm = HeightCm,
            WaterAmountMl = WaterAmountMl,
            RunoffAmountMl = RunoffAmountMl,
            IrrigationPh = IrrigationPh,
            IrrigationEc = IrrigationEc,
            DrainPh = DrainPh,
            DrainEc = DrainEc,
            ReservoirPh = ReservoirPh,
            ReservoirEc = ReservoirEc,
            ReservoirWaterTempC = ReservoirWaterTempC,
            ReservoirLevelCm = ReservoirLevelCm,
            ReservoirLevelLiters = ReservoirLevelLiters,
            DissolvedOxygenMgL = DissolvedOxygenMgL,
            OrpMv = OrpMv,
            TopOffLiters = TopOffLiters,
            AddbackEc = AddbackEc,
            SolutionChange = SolutionChange,
            PpfdMol = PpfdMol,
            Co2Ppm = Co2Ppm
        };
    }
}
