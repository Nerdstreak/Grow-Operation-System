using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Regression for the bug where mapped RDWC/DWC reservoir sensors stayed blank on the live
/// dashboard: the reservoir metric cards were only built when a grow was flagged active-hydro
/// or a past measurement existed, ignoring a mapped sensor that Home Assistant was actively
/// reporting. They must now show as soon as the sensor is mapped and HA returns a value.
/// </summary>
public sealed class GrowDashboardComposerReservoirTests
{
    // BuildTentMetrics and its helpers never touch the injected services, so null is safe here.
    private static readonly GrowDashboardComposer Composer = new(null!, null!, null!, null!);

    private static Tent TentWithoutActiveHydro() => new() { Id = 1, Name = "Zelt-RDWC", ActiveGrows = new() };

    private static HomeAssistantState State(double value) => new() { State = value.ToString("0.00"), NumericValue = value };

    [Fact]
    public void MappedReservoirSensors_ShowOnLive_WithoutActiveGrowOrMeasurement()
    {
        var states = new Dictionary<string, HomeAssistantState>
        {
            ["temperature"] = State(21.7),
            ["reservoir-ph"] = State(6.1),
            ["reservoir-ec"] = State(1.8),
            ["reservoir-temp"] = State(20.0),
        };

        var cards = Composer.BuildTentMetrics(TentWithoutActiveHydro(), states, new List<Measurement>());

        // The card must exist and carry the live HA value (not the "–" placeholder).
        // Value formatting is culture-dependent, so assert on presence, not the exact string.
        var ph = Assert.Single(cards, card => card.Key == "reservoir-ph");
        Assert.False(string.IsNullOrEmpty(ph.Value));
        Assert.NotEqual("–", ph.Value);
        Assert.Contains(cards, card => card.Key == "reservoir-ec" && card.Value != "–");
        Assert.Contains(cards, card => card.Key == "reservoir-temp" && card.Value != "–");
    }

    [Fact]
    public void UnmappedReservoir_WithoutHydroOrMeasurement_StaysHidden()
    {
        var states = new Dictionary<string, HomeAssistantState> { ["temperature"] = State(21.7) };

        var cards = Composer.BuildTentMetrics(TentWithoutActiveHydro(), states, new List<Measurement>());

        Assert.DoesNotContain(cards, card => card.Key == "reservoir-ph");
        Assert.DoesNotContain(cards, card => card.Key == "reservoir-ec");
        // Climate is always built.
        Assert.Contains(cards, card => card.Key == "temperature");
    }

    [Fact]
    public void ActiveHydroGrow_ShowsReservoir_EvenWithoutMappedState()
    {
        var tent = new Tent
        {
            Id = 1,
            Name = "Zelt-RDWC",
            ActiveGrows = new() { new GrowRun { Id = 1, IrrigationType = IrrigationType.ActiveHydro } },
        };

        var cards = Composer.BuildTentMetrics(tent, new Dictionary<string, HomeAssistantState>(), new List<Measurement>());

        Assert.Contains(cards, card => card.Key == "reservoir-ph");
    }
}
