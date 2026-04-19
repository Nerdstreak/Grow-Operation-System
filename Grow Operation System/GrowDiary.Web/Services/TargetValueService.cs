using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Sollwerte pro Phase und Anbautyp – Basis: RDWC (Athena Blended / SKX-Growplan).
/// DWC EC wird über DwcEcMultiplier hochgerechnet.
/// </summary>
public sealed record HydroTargetValues(
    double PhMin,
    double PhMax,
    double EcMin,
    double EcMax,
    double OrpMin,
    double OrpMax,
    double WaterTempDayC,
    double WaterTempNightC,
    double VpdMin,
    double VpdMax,
    double PpfdMin,
    double PpfdMax,
    double Co2Min,
    double Co2Max
);

public static class TargetValueService
{
    /// <summary>
    /// EC-Multiplikator für DWC gegenüber RDWC.
    /// DWC hat weniger Puffervolumen und braucht 30–70 % höhere EC.
    /// Startwert: 1.3 (konservativ, kann pro Grow kalibriert werden).
    /// </summary>
    public const double DwcEcMultiplier = 1.3;

    private static readonly Dictionary<GrowStage, HydroTargetValues> RdwcTargets = new()
    {
        // Seedling / Clone: sehr schwache Lösung, empfindliche Wurzeln
        [GrowStage.Seedling] = new(
            PhMin: 6.0, PhMax: 6.2,
            EcMin: 0.2, EcMax: 0.4,
            OrpMin: 300, OrpMax: 400,
            WaterTempDayC: 22, WaterTempNightC: 20,
            VpdMin: 0.4, VpdMax: 0.5,
            PpfdMin: 200, PpfdMax: 300,
            Co2Min: 400, Co2Max: 500),

        [GrowStage.Clone] = new(
            PhMin: 6.0, PhMax: 6.2,
            EcMin: 0.2, EcMax: 0.4,
            OrpMin: 300, OrpMax: 400,
            WaterTempDayC: 22, WaterTempNightC: 20,
            VpdMin: 0.4, VpdMax: 0.5,
            PpfdMin: 200, PpfdMax: 300,
            Co2Min: 400, Co2Max: 500),

        // Veg (späte Veg, Woche 3–4): stärkere Lösung, höhere PPFD
        [GrowStage.Veg] = new(
            PhMin: 6.0, PhMax: 6.1,
            EcMin: 0.6, EcMax: 0.8,
            OrpMin: 300, OrpMax: 400,
            WaterTempDayC: 20, WaterTempNightC: 20,
            VpdMin: 0.7, VpdMax: 0.9,
            PpfdMin: 500, PpfdMax: 600,
            Co2Min: 800, Co2Max: 1000),

        // Transition (Woche 1 Blüte): pH leicht abgesenkt, EC steigt
        [GrowStage.Transition] = new(
            PhMin: 5.9, PhMax: 6.0,
            EcMin: 0.8, EcMax: 1.0,
            OrpMin: 300, OrpMax: 400,
            WaterTempDayC: 20, WaterTempNightC: 20,
            VpdMin: 1.0, VpdMax: 1.1,
            PpfdMin: 600, PpfdMax: 800,
            Co2Min: 1000, Co2Max: 1200),

        // Flower (früh–mitte, Woche 1–6): Hauptblüte
        [GrowStage.Flower] = new(
            PhMin: 5.9, PhMax: 6.0,
            EcMin: 1.0, EcMax: 1.2,
            OrpMin: 300, OrpMax: 400,
            WaterTempDayC: 20, WaterTempNightC: 18,
            VpdMin: 1.0, VpdMax: 1.2,
            PpfdMin: 800, PpfdMax: 1000,
            Co2Min: 1200, Co2Max: 1400),

        // Finish (Woche 7–8 / Spätblüte): pH weiter runter, EC-Peak dann abfallend
        [GrowStage.Finish] = new(
            PhMin: 5.6, PhMax: 5.8,
            EcMin: 1.1, EcMax: 1.6,
            OrpMin: 300, OrpMax: 400,
            WaterTempDayC: 18, WaterTempNightC: 16,
            VpdMin: 1.4, VpdMax: 1.6,
            PpfdMin: 500, PpfdMax: 1000,
            Co2Min: 400, Co2Max: 600)
    };

    /// <summary>
    /// Gibt Sollwerte für den angegebenen HydroStyle und GrowStage zurück.
    /// Gibt null zurück wenn keine Sollwerte für diese Kombination vorliegen (z.B. Dry, Cure).
    /// DWC-EC wird automatisch mit DwcEcMultiplier hochgerechnet.
    /// </summary>
    public static HydroTargetValues? GetTargets(HydroStyle hydroStyle, GrowStage stage)
    {
        if (!RdwcTargets.TryGetValue(stage, out var targets))
        {
            return null;
        }

        if (hydroStyle == HydroStyle.DWC)
        {
            return targets with
            {
                EcMin = Math.Round(targets.EcMin * DwcEcMultiplier, 2),
                EcMax = Math.Round(targets.EcMax * DwcEcMultiplier, 2)
            };
        }

        return targets;
    }
}
