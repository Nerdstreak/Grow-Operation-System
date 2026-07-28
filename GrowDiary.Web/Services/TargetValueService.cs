using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;

namespace GrowDiary.Web.Services;

/// <summary>
/// Sollwerte pro Phase und Anbautyp, aus den Profil-Dateien der Wissensbasis.
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
    /// EC-Aufschlag von DWC gegenüber RDWC — weniger Puffervolumen, also höhere EC.
    /// </summary>
    /// <remarks>
    /// Steht nur noch hier, weil die Nährstoff-Empfehlung ihn braucht. Die
    /// Zielwerte rechnen NICHT mehr damit: DWC hat ein eigenes Profil, in dem
    /// der Aufschlag schon je Phase eingerechnet ist. Beides zusammen ergäbe
    /// das 1,69-fache.
    /// </remarks>
    public const double DwcEcMultiplier = 1.3;

    /// <summary>Fällt ein Anbaustil durch, gilt RDWC — das ist das ausführlichste Profil.</summary>
    private const string FallbackProfileId = "rdwc-default";

    private readonly Dictionary<string, Dictionary<GrowStage, HydroTargetValues>> _profiles;
    private readonly Infrastructure.SetpointProfileRepository? _customProfiles;

    public TargetValueService(KnowledgeBaseLoader knowledgeBase, Infrastructure.SetpointProfileRepository? customProfiles = null)
    {
        _profiles = LoadProfiles(knowledgeBase);
        _customProfiles = customProfiles;
    }

    /// <summary>
    /// Welches Profil für einen Anbaustil gilt.
    /// </summary>
    /// <remarks>
    /// NFT, Aeroponik und „Sonstiges" haben noch kein eigenes Profil und
    /// bekommen RDWC. Das ist eine Annahme, keine Messung — sie steht hier an
    /// einer Stelle, statt sich über den Code zu verteilen.
    /// </remarks>
    public static string ProfileIdFor(HydroStyle hydroStyle) => hydroStyle switch
    {
        HydroStyle.DWC => "dwc-default",
        _ => FallbackProfileId,
    };

    /// <summary>Die geladenen Profile — für Anzeige und Auswahl.</summary>
    public IReadOnlyCollection<string> ProfileIds => _profiles.Keys;

    /// <summary>
    /// Gibt Sollwerte für den angegebenen HydroStyle und GrowStage zurück.
    /// Gibt null zurück wenn keine Sollwerte für diese Kombination vorliegen (z.B. Dry, Cure).
    /// DWC-EC wird automatisch mit DwcEcMultiplier hochgerechnet.
    /// </summary>
    public HydroTargetValues? GetTargets(HydroStyle hydroStyle, GrowStage stage)
        => GetTargets(ProfileIdFor(hydroStyle), stage);

    /// <summary>
    /// Die Sollwerte eines bestimmten Profils — mitgeliefert oder eigenes.
    /// </summary>
    /// <remarks>
    /// Ein eigenes Profil speichert nur seine Abweichungen. Die Basis kommt aus
    /// der Wissensbasis und wird bei jedem Abruf frisch daruntergelegt — so
    /// erreichen spaetere Verbesserungen auch den, der einmal etwas angepasst
    /// hat.
    /// </remarks>
    public HydroTargetValues? GetTargets(string profileId, GrowStage stage)
    {
        SetpointProfile? eigenes = null;
        var basisId = profileId;

        if (SetpointProfile.IdFromReference(profileId) is { } customId)
        {
            eigenes = _customProfiles?.Get(customId);
            // Geloeschtes Profil: lieber die Basis als ein leerer Bildschirm.
            basisId = eigenes?.BaseProfileId ?? FallbackProfileId;
        }

        if (!_profiles.TryGetValue(basisId, out var profile)
            && !_profiles.TryGetValue(FallbackProfileId, out profile))
        {
            return null;
        }

        if (!profile.TryGetValue(stage, out var targets)) return null;

        return eigenes is null ? targets : SetpointProfileResolver.Apply(targets, eigenes, stage);
    }

    /// <summary>
    /// Alle Profil-Dateien der Wissensbasis, nach Id.
    /// </summary>
    /// <remarks>
    /// Vorher wurde genau eine Datei gesucht („rdwc-default"). Ein zweites
    /// Profil danebenzulegen hätte nichts bewirkt — niemand hätte es gelesen.
    /// </remarks>
    private static Dictionary<string, Dictionary<GrowStage, HydroTargetValues>> LoadProfiles(KnowledgeBaseLoader kb)
    {
        var profiles = new Dictionary<string, Dictionary<GrowStage, HydroTargetValues>>(StringComparer.OrdinalIgnoreCase);
        foreach (var setpoint in kb.Setpoints)
        {
            if (string.IsNullOrWhiteSpace(setpoint.Id)) continue;
            profiles[setpoint.Id] = LoadStages(setpoint);
        }
        return profiles;
    }

    private static Dictionary<GrowStage, HydroTargetValues> LoadStages(Knowledge.Schema.SetpointDefinition setpoint)
    {
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
