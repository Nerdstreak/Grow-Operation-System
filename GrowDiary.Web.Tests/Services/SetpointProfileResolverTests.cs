using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Welches Profil gilt: Grow → Hydro-System → Anbaustil.
/// </summary>
/// <remarks>
/// Zwei Ebenen mit verschiedener Aufgabe. Das System bestimmt den Standard,
/// weil DWC oder RDWC eine Eigenschaft der Hardware ist. Der Grow darf
/// abweichen, weil Sollwerte beschreiben, wie man diese Pflanze fährt.
/// </remarks>
public sealed class SetpointProfileResolverTests
{
    [Fact]
    public void WithoutAnyChoice_TheGrowingStyleDecides()
    {
        var rdwc = SetpointProfileResolver.Resolve(null, null, HydroStyle.RDWC);
        var dwc = SetpointProfileResolver.Resolve(null, null, HydroStyle.DWC);

        Assert.Equal("rdwc-default", rdwc.ProfileId);
        Assert.Equal(ProfileOrigin.Style, rdwc.Origin);
        Assert.Equal("dwc-default", dwc.ProfileId);
    }

    [Fact]
    public void TheSystemSetsTheDefault()
    {
        var result = SetpointProfileResolver.Resolve(null, "custom:3", HydroStyle.RDWC);

        Assert.Equal("custom:3", result.ProfileId);
        Assert.Equal(ProfileOrigin.System, result.Origin);
    }

    [Fact]
    public void TheGrowMayDiffer()
    {
        // Zwei Laeufe im selben Becken duerfen verschieden laufen.
        var result = SetpointProfileResolver.Resolve("custom:9", "custom:3", HydroStyle.RDWC);

        Assert.Equal("custom:9", result.ProfileId);
        Assert.Equal(ProfileOrigin.Grow, result.Origin);
    }

    [Fact]
    public void EmptyTextCountsAsNoChoice()
    {
        // Ein leeres Feld aus dem Formular darf nicht als Auswahl durchgehen.
        var result = SetpointProfileResolver.Resolve("  ", "", HydroStyle.DWC);

        Assert.Equal("dwc-default", result.ProfileId);
        Assert.Equal(ProfileOrigin.Style, result.Origin);
    }

    // ---------- Abweichungen anwenden ----------

    private static HydroTargetValues Basis() => new(
        PhMin: 6.0, PhMax: 6.1, EcMin: 0.6, EcMax: 0.8,
        OrpMin: 300, OrpMax: 400, WaterTempDayC: 21, WaterTempNightC: 19,
        VpdMin: 0.7, VpdMax: 0.9, PpfdMin: 500, PpfdMax: 600,
        Co2Min: 800, Co2Max: 1000);

    private static SetpointProfile Profile(string stage, Dictionary<string, double> felder) => new()
    {
        Id = 1, Name = "Meine Werte", BaseProfileId = "rdwc-default",
        Overrides = new() { [stage] = felder },
    };

    [Fact]
    public void OnlyChangedFieldsAreReplaced()
    {
        // Der Kern der Sache: was der Nutzer nicht angefasst hat, bleibt am
        // Wissen haengen und wandert mit, wenn wir es aktualisieren.
        var profile = Profile("Veg", new() { ["phMin"] = 5.8, ["phMax"] = 6.0 });

        var result = SetpointProfileResolver.Apply(Basis(), profile, GrowStage.Veg);

        Assert.Equal(5.8, result.PhMin);
        Assert.Equal(6.0, result.PhMax);
        Assert.Equal(0.6, result.EcMin);      // unveraendert
        Assert.Equal(0.7, result.VpdMin);     // unveraendert
        Assert.Equal(300, result.OrpMin);     // unveraendert
    }

    [Fact]
    public void OtherPhasesAreUntouched()
    {
        var profile = Profile("Veg", new() { ["phMin"] = 5.8 });

        var result = SetpointProfileResolver.Apply(Basis(), profile, GrowStage.Flower);

        Assert.Equal(Basis(), result);
    }

    [Fact]
    public void AProfileWithoutOverrides_ChangesNothing()
    {
        var leer = new SetpointProfile { Id = 1, Name = "Leer", BaseProfileId = "rdwc-default" };

        Assert.Equal(Basis(), SetpointProfileResolver.Apply(Basis(), leer, GrowStage.Veg));
    }

    [Fact]
    public void AnEmptyPhaseEntry_ChangesNothing()
    {
        var profile = Profile("Veg", new());

        Assert.Equal(Basis(), SetpointProfileResolver.Apply(Basis(), profile, GrowStage.Veg));
    }

    [Fact]
    public void EveryFieldCanBeOverridden()
    {
        var alle = new Dictionary<string, double>
        {
            ["phMin"] = 1, ["phMax"] = 2, ["ecMin"] = 3, ["ecMax"] = 4,
            ["orpMin"] = 5, ["orpMax"] = 6, ["waterTempDayC"] = 7, ["waterTempNightC"] = 8,
            ["vpdMin"] = 9, ["vpdMax"] = 10, ["ppfdMin"] = 11, ["ppfdMax"] = 12,
            ["co2Min"] = 13, ["co2Max"] = 14,
        };

        var result = SetpointProfileResolver.Apply(Basis(), Profile("Veg", alle), GrowStage.Veg);

        Assert.Equal(new HydroTargetValues(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14), result);
        // Und die Feldliste des Modells deckt genau diese ab — sonst waere ein
        // Feld in der Oberflaeche nicht editierbar.
        Assert.Equal(alle.Keys.OrderBy(k => k), SetpointProfile.Fields.OrderBy(k => k));
    }

    // ---------- Verweise ----------

    [Fact]
    public void CustomReferencesRoundTrip()
    {
        Assert.Equal("custom:7", SetpointProfile.Reference(7));
        Assert.Equal(7, SetpointProfile.IdFromReference("custom:7"));
    }

    [Theory]
    [InlineData("rdwc-default")]
    [InlineData("dwc-default")]
    [InlineData("custom:abc")]
    [InlineData("")]
    [InlineData(null)]
    public void NonCustomReferences_AreNotMistakenForOne(string? reference)
    {
        Assert.Null(SetpointProfile.IdFromReference(reference));
    }
}
