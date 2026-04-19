namespace GrowDiary.Web.Models;

public sealed class GrowthProfile
{
    public GrowthProfile(HydroStyle hydroStyle)
    {
        HydroStyle = hydroStyle;
    }

    public HydroStyle HydroStyle { get; }

    public bool IsHydro => true;
    public bool IsAutopot => false;
    public bool IsSoilOrganic => false;
    public bool IsSoilMineral => false;
    public bool IsCoco => false;
    public bool IsLivingSoil => false;
    public bool UsesAmbientMetrics => true;
    public bool UsesHeight => true;
    public bool UsesWaterAmount => false;
    public bool UsesRunoffAmount => false;
    public bool UsesIrrigationPh => false;
    public bool UsesIrrigationEc => false;
    public bool UsesDrainPh => false;
    public bool UsesDrainEc => false;
    public bool UsesReservoirPh => true;
    public bool UsesReservoirEc => true;
    public bool UsesReservoirTemp => true;
    public bool UsesReservoirLevel => true;
    public bool UsesReservoirDissolvedOxygen => true;
    public bool UsesReservoirOrp => true;
    public bool UsesTopOff => true;
    public bool UsesAddbackEc => true;

    public string Label => $"{HydroStyle} · Hydro";
}
