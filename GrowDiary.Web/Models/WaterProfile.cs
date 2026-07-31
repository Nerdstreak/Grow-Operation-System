namespace GrowDiary.Web.Models;

/// <summary>
/// What comes out of the tap, according to the city's water report.
/// </summary>
/// <remarks>
/// <para>The fields mirror an actual German Trinkwasserbericht (modelled on the
/// EBW Solingen annual report), not a wish list: conductivity, pH, the two
/// hardness figures, the handful of ions that matter for a reservoir, and the
/// disinfectant. Everything is optional — reports differ, and a half-filled
/// profile is more useful than a rejected one.</para>
///
/// <para>One profile, app-wide. The water in the pipe does not change per grow;
/// a grow decides via its <see cref="GrowRun.WaterSource"/> whether the profile
/// applies (Tap/Mixed) or not (RO).</para>
/// </remarks>
public sealed class WaterProfile
{
    /// <summary>Where the numbers come from, e.g. "EBW Solingen — Werk Glüder, Jahresmittel 2025".</summary>
    public string SourceLabel { get; set; } = string.Empty;

    /// <summary>Electrical conductivity in µS/cm — the report's unit, not mS/cm.</summary>
    /// <remarks>
    /// Stored as printed so the user can copy the number straight from the PDF.
    /// The UI converts to EC (mS/cm) for display next to reservoir values.
    /// </remarks>
    public double? ConductivityUsCm { get; set; }

    public double? Ph { get; set; }

    /// <summary>Gesamthärte in °dH.</summary>
    public double? TotalHardnessDh { get; set; }

    /// <summary>Karbonathärte in °dH — the pH buffer.</summary>
    public double? CarbonateHardnessDh { get; set; }

    public double? CalciumMgL { get; set; }
    public double? MagnesiumMgL { get; set; }
    public double? SodiumMgL { get; set; }
    public double? NitrateMgL { get; set; }
    public double? SulfateMgL { get; set; }
    public double? ChlorideMgL { get; set; }

    /// <summary>The disinfectant named in the report, e.g. "Chlordioxid".</summary>
    public string? Disinfection { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Is there anything in here worth showing?</summary>
    public bool HasAnyValue =>
        ConductivityUsCm is not null || Ph is not null || TotalHardnessDh is not null ||
        CarbonateHardnessDh is not null || CalciumMgL is not null || MagnesiumMgL is not null ||
        SodiumMgL is not null || NitrateMgL is not null || SulfateMgL is not null ||
        ChlorideMgL is not null || !string.IsNullOrWhiteSpace(Disinfection);
}
