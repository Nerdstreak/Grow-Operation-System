using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>Welches Profil gilt — und warum.</summary>
public sealed record ResolvedProfile(string ProfileId, ProfileOrigin Origin);

/// <summary>Woher die Entscheidung kam.</summary>
public enum ProfileOrigin
{
    /// <summary>Am Grow selbst gewählt.</summary>
    Grow,
    /// <summary>Vom Hydro-System geerbt.</summary>
    System,
    /// <summary>Aus dem Anbaustil, weil nirgends etwas gewählt wurde.</summary>
    Style
}

/// <summary>
/// Die Kette: Grow → Hydro-System → Anbaustil.
/// </summary>
/// <remarks>
/// Zwei Ebenen mit verschiedener Aufgabe. Das <b>System</b> bestimmt den
/// Standard, weil DWC oder RDWC eine Eigenschaft der Hardware ist — einmal
/// eingestellt, erbt jeder Grow darin. Der <b>Grow</b> darf abweichen, weil
/// Sollwerte beschreiben, wie man DIESE Pflanze fährt: eine Sorte, die mehr
/// verträgt, ein Versuch, ein anderes Ziel. Zwei Läufe im selben Becken dürfen
/// verschieden laufen.
///
/// Über allem steht weiterhin der Grenzwert, den der Nutzer im Zelt einträgt —
/// siehe <see cref="UserTargets"/>. Das ist die kurze Leine für den schnellen
/// Eingriff, das Profil die lange Linie.
/// </remarks>
public static class SetpointProfileResolver
{
    public static ResolvedProfile Resolve(string? growProfileId, string? systemProfileId, HydroStyle style)
    {
        if (!string.IsNullOrWhiteSpace(growProfileId))
        {
            return new ResolvedProfile(growProfileId, ProfileOrigin.Grow);
        }

        if (!string.IsNullOrWhiteSpace(systemProfileId))
        {
            return new ResolvedProfile(systemProfileId, ProfileOrigin.System);
        }

        return new ResolvedProfile(TargetValueService.ProfileIdFor(style), ProfileOrigin.Style);
    }

    /// <summary>
    /// Legt die eigenen Werte des Nutzers über die Basis — nur für die Phase,
    /// um die es geht, und nur die Felder, die er wirklich angefasst hat.
    /// </summary>
    public static HydroTargetValues Apply(HydroTargetValues basis, SetpointProfile profile, GrowStage stage)
    {
        if (!profile.Overrides.TryGetValue(stage.ToString(), out var felder) || felder.Count == 0)
        {
            return basis;
        }

        double Wert(string feld, double standard) => felder.TryGetValue(feld, out var v) ? v : standard;

        return basis with
        {
            PhMin = Wert("phMin", basis.PhMin),
            PhMax = Wert("phMax", basis.PhMax),
            EcMin = Wert("ecMin", basis.EcMin),
            EcMax = Wert("ecMax", basis.EcMax),
            OrpMin = Wert("orpMin", basis.OrpMin),
            OrpMax = Wert("orpMax", basis.OrpMax),
            WaterTempDayC = Wert("waterTempDayC", basis.WaterTempDayC),
            WaterTempNightC = Wert("waterTempNightC", basis.WaterTempNightC),
            VpdMin = Wert("vpdMin", basis.VpdMin),
            VpdMax = Wert("vpdMax", basis.VpdMax),
            PpfdMin = Wert("ppfdMin", basis.PpfdMin),
            PpfdMax = Wert("ppfdMax", basis.PpfdMax),
            Co2Min = Wert("co2Min", basis.Co2Min),
            Co2Max = Wert("co2Max", basis.Co2Max),
        };
    }
}
