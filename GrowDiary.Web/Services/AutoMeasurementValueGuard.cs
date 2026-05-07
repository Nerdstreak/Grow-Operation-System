using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public enum AutoMeasurementValueSeverity
{
    None,
    Warning,
    Reject
}

public sealed record AutoMeasurementValueGuardResult(
    bool IsValid,
    AutoMeasurementValueSeverity Severity,
    string? Message);

public sealed class AutoMeasurementValueGuard
{
    public AutoMeasurementValueGuardResult Check(AutoMeasurementField field, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return Reject(field, value, "Wert ist nicht numerisch plausibel.");
        }

        return field switch
        {
            AutoMeasurementField.AirTemperatureC => CheckRange(field, value, 0, 50, 10, 40),
            AutoMeasurementField.HumidityPercent => CheckRange(field, value, 0, 100),
            AutoMeasurementField.ReservoirPh => CheckRange(field, value, 3.0, 9.0, 5.0, 7.0),
            AutoMeasurementField.ReservoirEc => CheckRange(field, value, 0, 5.0, warningMin: null, warningMax: 3.0),
            AutoMeasurementField.ReservoirWaterTempC => CheckRange(field, value, 0, 35, 15, 26),
            AutoMeasurementField.ReservoirLevelLiters => CheckRange(field, value, 0, null),
            AutoMeasurementField.ReservoirLevelCm => CheckRange(field, value, 0, null),
            AutoMeasurementField.DissolvedOxygenMgL => CheckRange(field, value, 0, 20, warningMin: 4, warningMax: null),
            AutoMeasurementField.OrpMv => CheckRange(field, value, -500, 1200, 250, 650),
            AutoMeasurementField.PpfdMol => CheckRange(field, value, 0, 2500, warningMin: null, warningMax: 1500),
            AutoMeasurementField.Co2Ppm => CheckRange(field, value, 0, 5000, warningMin: null, warningMax: 2000),
            _ => Valid()
        };
    }

    private static AutoMeasurementValueGuardResult CheckRange(
        AutoMeasurementField field,
        double value,
        double? rejectMin,
        double? rejectMax,
        double? warningMin = null,
        double? warningMax = null)
    {
        if (rejectMin.HasValue && value < rejectMin.Value)
        {
            return Reject(field, value, $"Wert liegt unter dem Hard-Limit {Format(rejectMin.Value)}.");
        }

        if (rejectMax.HasValue && value > rejectMax.Value)
        {
            return Reject(field, value, $"Wert liegt ueber dem Hard-Limit {Format(rejectMax.Value)}.");
        }

        if (warningMin.HasValue && value < warningMin.Value)
        {
            return Warning(field, value, $"Wert liegt unter dem Warnbereich {Format(warningMin.Value)}.");
        }

        if (warningMax.HasValue && value > warningMax.Value)
        {
            return Warning(field, value, $"Wert liegt ueber dem Warnbereich {Format(warningMax.Value)}.");
        }

        return Valid();
    }

    private static AutoMeasurementValueGuardResult Valid()
        => new(true, AutoMeasurementValueSeverity.None, null);

    private static AutoMeasurementValueGuardResult Warning(AutoMeasurementField field, double value, string reason)
        => new(true, AutoMeasurementValueSeverity.Warning, $"{field} Wert {Format(value)}: {reason}");

    private static AutoMeasurementValueGuardResult Reject(AutoMeasurementField field, double value, string reason)
        => new(false, AutoMeasurementValueSeverity.Reject, $"{field} Wert {Format(value)}: {reason}");

    private static string Format(double value)
        => value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
}
