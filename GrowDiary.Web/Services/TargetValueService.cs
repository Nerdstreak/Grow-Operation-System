using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;

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

public sealed class TargetValueService
{
    /// <summary>
    /// EC-Multiplikator für DWC gegenüber RDWC.
    /// DWC hat weniger Puffervolumen und braucht 30–70 % höhere EC.
    /// Startwert: 1.3 (konservativ, kann pro Grow kalibriert werden).
    /// </summary>
    public const double DwcEcMultiplier = 1.3;

    private readonly Dictionary<GrowStage, HydroTargetValues> _rdwcTargets;

    public TargetValueService(KnowledgeBaseLoader knowledgeBase)
    {
        _rdwcTargets = LoadRdwcTargets(knowledgeBase);
    }

    /// <summary>
    /// Gibt Sollwerte für den angegebenen HydroStyle und GrowStage zurück.
    /// Gibt null zurück wenn keine Sollwerte für diese Kombination vorliegen (z.B. Dry, Cure).
    /// DWC-EC wird automatisch mit DwcEcMultiplier hochgerechnet.
    /// </summary>
    public HydroTargetValues? GetTargets(HydroStyle hydroStyle, GrowStage stage)
    {
        if (!_rdwcTargets.TryGetValue(stage, out var targets))
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

    private static Dictionary<GrowStage, HydroTargetValues> LoadRdwcTargets(KnowledgeBaseLoader kb)
    {
        var setpoint = kb.Setpoints.FirstOrDefault(s => s.Id == "rdwc-default");
        if (setpoint is null)
        {
            return new Dictionary<GrowStage, HydroTargetValues>();
        }

        var result = new Dictionary<GrowStage, HydroTargetValues>();
        foreach (var (stageName, sp) in setpoint.Stages)
        {
            if (Enum.TryParse<GrowStage>(stageName, ignoreCase: true, out var stage))
            {
                result[stage] = new HydroTargetValues(
                    PhMin: sp.PhMin, PhMax: sp.PhMax,
                    EcMin: sp.EcMin, EcMax: sp.EcMax,
                    OrpMin: sp.OrpMin, OrpMax: sp.OrpMax,
                    WaterTempDayC: sp.WaterTempDayC, WaterTempNightC: sp.WaterTempNightC,
                    VpdMin: sp.VpdMin, VpdMax: sp.VpdMax,
                    PpfdMin: sp.PpfdMin, PpfdMax: sp.PpfdMax,
                    Co2Min: sp.Co2Min, Co2Max: sp.Co2Max
                );
            }
        }
        return result;
    }
}
