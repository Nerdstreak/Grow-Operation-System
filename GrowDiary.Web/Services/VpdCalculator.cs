namespace GrowDiary.Web.Services;

/// <summary>
/// Vapour pressure deficit. What the plant actually feels is <em>leaf</em> VPD: the leaf is
/// cooler than the air (transpiration, no IR under LED), typically by 1–3 °C. That offset is
/// configured per tent — with an offset of 0 this is the plain air VPD.
/// </summary>
public static class VpdCalculator
{
    /// <summary>Saturation vapour pressure in kPa (Magnus formula).</summary>
    public static double SaturationKpa(double temperatureC)
        => 0.6108 * Math.Exp((17.27 * temperatureC) / (temperatureC + 237.3));

    /// <summary>
    /// Leaf VPD in kPa from air temperature, relative humidity and the leaf-temperature
    /// offset (how many °C the leaf sits below the air). Returns null for implausible input.
    /// </summary>
    public static double? Calculate(double? airTemperatureC, double? humidityPercent, double leafOffsetC = 0)
    {
        if (airTemperatureC is not { } air || humidityPercent is not { } humidity)
        {
            return null;
        }

        if (humidity is < 0 or > 100)
        {
            return null;
        }

        // Actual vapour pressure comes from the air; the deficit is measured against what
        // the (cooler) leaf surface could hold.
        var actual = SaturationKpa(air) * (humidity / 100.0);
        var leaf = SaturationKpa(air - leafOffsetC);
        var vpd = leaf - actual;
        return vpd < 0 ? 0 : vpd;
    }
}
