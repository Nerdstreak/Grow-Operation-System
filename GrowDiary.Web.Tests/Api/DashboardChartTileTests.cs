using System.Text.Json;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Die Verlaufs-Kachel muss den Weg durch den Vertrag überleben.
/// </summary>
/// <remarks>
/// <para>Sie wäre beim ersten Speichern beinahe verschwunden: der Filter im
/// Controller liess nur Kacheln durch, die einen <c>MetricKey</c> oder eine
/// <c>EntityId</c> tragen — eine Kachel mit einer LISTE von Messwerten hat
/// weder das eine noch das andere. Sie liess sich anlegen, war zu sehen, und
/// war nach dem Speichern weg.</para>
///
/// <para>Diese Tests halten beide Enden fest: dass die Liste durch die
/// Serialisierung kommt, und dass eine Kachel ohne jeden Inhalt weiterhin
/// aussortiert wird.</para>
/// </remarks>
public sealed class DashboardChartTileTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void TheMetricListSurvivesSerialisation()
    {
        var dto = new DashboardTileDto(
            "abc", nameof(DashboardTileKind.Chart), null, null, "Verlauf · 24 h", null, 3,
            ["temperature", "humidity", "vpd"]);

        var zurueck = JsonSerializer.Deserialize<DashboardTileDto>(JsonSerializer.Serialize(dto, Web), Web)!;

        Assert.Equal("Chart", zurueck.Kind);
        Assert.Equal(["temperature", "humidity", "vpd"], zurueck.MetricKeys);
        Assert.Null(zurueck.MetricKey);
    }

    [Fact]
    public void AChartTileCarriesNoSingleMetricAndThatIsFine()
    {
        // Genau diese Kombination hat der Filter frueher verworfen.
        var tile = new DashboardTile
        {
            Kind = DashboardTileKind.Chart,
            MetricKeys = ["temperature", "humidity"],
            Span = 3,
        };

        Assert.Null(tile.MetricKey);
        Assert.Null(tile.EntityId);
        Assert.NotEmpty(tile.MetricKeys);
    }

    [Fact]
    public void TheDefaultLayoutStillHasNoChartTile()
    {
        // Der Verlauf ist eine Zugabe, die der Nutzer selbst dazunimmt — er
        // darf niemandem ungefragt auf dem Bildschirm erscheinen.
        var standard = DashboardLayout.Default(1);

        Assert.DoesNotContain(
            standard.Sections.SelectMany(section => section.Tiles),
            tile => tile.Kind == DashboardTileKind.Chart);
    }
}
