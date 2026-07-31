using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Das Leitungswasser-Profil und seine Wirkung.
/// </summary>
/// <remarks>
/// Das Feld <c>WaterSource</c> am Grow existierte seit jeher und wurde von
/// keinem Dienst gelesen — es war Deko. Diese Tests halten fest, dass es jetzt
/// eine Aufgabe hat: es beantwortet die Wasserfrage der Abläufe vor.
/// </remarks>
public sealed class WaterProfileTests
{
    [Theory]
    [InlineData(WaterSource.Tap, "soft")]
    [InlineData(WaterSource.Mixed, "soft")]
    [InlineData(WaterSource.RO, "ro")]
    public void TheGrowsWaterSourceAnswersTheSopQuestion(WaterSource quelle, string erwartet)
    {
        // Die Ablaeufe kennen zwei Wege: "ro" und "soft" (Weichwasser ODER
        // gemischtes Leitungswasser) — deshalb landen Tap und Mixed beide auf
        // soft. Ein dritter Weg fuer hartes Wasser existiert im Quellmaterial
        // nicht; sollte er dazukommen, muss diese Zuordnung mitwachsen.
        Assert.Equal(erwartet, SopInstancesApiController.WasserVorschlag(quelle));
    }

    [Fact]
    public void NoWaterSourceMeansNoSuggestion()
    {
        // Lieber keine Vorauswahl als eine geratene.
        Assert.Null(SopInstancesApiController.WasserVorschlag(null));
    }

    [Fact]
    public void AnEmptyProfileKnowsItIsEmpty()
    {
        Assert.False(new WaterProfile().HasAnyValue);
        Assert.True(new WaterProfile { ConductivityUsCm = 276 }.HasAnyValue);
        Assert.True(new WaterProfile { Disinfection = "Chlordioxid" }.HasAnyValue);
    }
}
