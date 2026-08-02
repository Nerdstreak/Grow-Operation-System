using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Wasser-Ampel: aus Zahlen im Bericht werden Sätze.
/// </summary>
/// <remarks>
/// <para>Diese Tests halten die Schwellen an ihren Quellen fest. Eine Ampel,
/// die grundlos rot zeigt, schickt den Nutzer für ein paar hundert Euro
/// Osmoseanlage kaufen; eine, die grundlos grün zeigt, lässt ihn ein Jahr lang
/// gegen den pH kämpfen. Beides wäre schlimmer als gar keine Ampel.</para>
/// </remarks>
public sealed class WasserAmpelTests
{
    private static AmpelPunkt Punkt(WasserAmpel ampel, string feld)
        => ampel.Punkte.Single(p => p.Feld == feld);

    [Fact]
    public void SolingerTapWaterComesOutGreen()
    {
        // Das echte Wasser des Betreibers (EBW Solingen): weich, wenig Puffer,
        // niedriger Start-EC. Wenn die Ampel HIER anschlägt, ist sie kaputt.
        var ampel = WasserAmpelService.Bewerten(new WaterProfile
        {
            ConductivityUsCm = 280,
            Ph = 7.6,
            TotalHardnessDh = 5.6,
            CarbonateHardnessDh = 3.5,
            CalciumMgL = 32,
            MagnesiumMgL = 5,
            SodiumMgL = 9,
            ChlorideMgL = 24,
        });

        Assert.Equal("hinweis", ampel.Stufe); // nur der pH von 7,6 liegt knapp daneben
        Assert.Equal("gut", Punkt(ampel, "totalHardnessDh").Stufe);
        Assert.Equal("gut", Punkt(ampel, "carbonateHardnessDh").Stufe);
        Assert.Equal("gut", Punkt(ampel, "conductivityUsCm").Stufe);
        Assert.Equal("hinweis", Punkt(ampel, "ph").Stufe);
        Assert.Contains("Karbonathärte", Punkt(ampel, "ph").Aussage);
    }

    [Fact]
    public void HardBufferedWaterIsTheCaseTheTrafficLightExistsFor()
    {
        // 10 °dH Karbonathaerte sind ~179 mg/L CaCO3 — ueber der 150er-Grenze.
        var ampel = WasserAmpelService.Bewerten(new WaterProfile
        {
            ConductivityUsCm = 1100,
            TotalHardnessDh = 21,
            CarbonateHardnessDh = 10,
        });

        Assert.Equal("warnung", ampel.Stufe);
        Assert.Equal("warnung", Punkt(ampel, "carbonateHardnessDh").Stufe);
        Assert.Equal("warnung", Punkt(ampel, "totalHardnessDh").Stufe);
        Assert.Equal("warnung", Punkt(ampel, "conductivityUsCm").Stufe);
        Assert.Contains("hart", Punkt(ampel, "totalHardnessDh").Wert);
    }

    [Fact]
    public void EveryRatedValueNamesItsSource()
    {
        var ampel = WasserAmpelService.Bewerten(new WaterProfile
        {
            ConductivityUsCm = 400, Ph = 6.5, TotalHardnessDh = 7, CarbonateHardnessDh = 4,
            CalciumMgL = 30, MagnesiumMgL = 8, SodiumMgL = 10, ChlorideMgL = 20, NitrateMgL = 12,
        });

        Assert.All(ampel.Punkte, p => Assert.False(string.IsNullOrWhiteSpace(p.Quelle)));

        // Die gesetzliche Einordnung gehoert an die Haerte, die Gartenbau-Grenzen
        // an den Rest — keine Quelle darf zur Dekoration werden.
        Assert.Contains("WRMG", Punkt(ampel, "totalHardnessDh").Quelle);
        Assert.Contains("Penn State", Punkt(ampel, "carbonateHardnessDh").Quelle);
        Assert.Contains("150 mg/L", Punkt(ampel, "carbonateHardnessDh").Quelle);
    }

    [Fact]
    public void SoftWaterIsAnAdvantageWithAFeedProgramAndANoteWithout()
    {
        var weich = new WaterProfile { TotalHardnessDh = 4 };

        // Mit CalMag im Programm ist weiches Wasser der Idealfall — die
        // Gartenbau-Untergrenze gilt hier NICHT, weil dort das Giesswasser die
        // Calcium-Quelle ist und im RDWC der Duenger.
        var mitDuenger = WasserAmpelService.Bewerten(weich, duengerLiefertCalMag: true);
        Assert.Equal("gut", Punkt(mitDuenger, "totalHardnessDh").Stufe);

        var ohneDuenger = WasserAmpelService.Bewerten(weich, duengerLiefertCalMag: false);
        Assert.Equal("hinweis", Punkt(ohneDuenger, "totalHardnessDh").Stufe);
        Assert.Contains("CalMag", Punkt(ohneDuenger, "totalHardnessDh").Aussage);
    }

    [Fact]
    public void CalciumAndMagnesiumGetNoVerdictOnlyArithmetic()
    {
        // Im Gartenbau waeren 30 mg/L Calcium ein Mangel — im Kreislauf mit
        // vollstaendigem Duenger ist es schlicht ein Startwert.
        var ampel = WasserAmpelService.Bewerten(new WaterProfile { CalciumMgL = 30, NitrateMgL = 15 });

        Assert.Equal("gut", ampel.Stufe);
        Assert.Equal("gut", Punkt(ampel, "calciumMgL").Stufe);
        Assert.Contains("Kein Grenzwert", Punkt(ampel, "calciumMgL").Quelle);
        Assert.Contains("rechne es bei der Düngung mit", Punkt(ampel, "nitrateMgL").Aussage);
    }

    [Fact]
    public void UnfilledFieldsStaySilent()
    {
        // Ein halb ausgefuellter Bericht ist der Normalfall. Was nicht dasteht,
        // wird nicht bewertet — und schon gar nicht als „unbekannt = Risiko".
        var ampel = WasserAmpelService.Bewerten(new WaterProfile { ConductivityUsCm = 300 });

        Assert.Single(ampel.Punkte);
        Assert.Equal("conductivityUsCm", ampel.Punkte[0].Feld);
        Assert.Equal("gut", ampel.Stufe);
    }

    [Fact]
    public void SodiumAndChlorideCarryTheirOwnLimits()
    {
        var ampel = WasserAmpelService.Bewerten(new WaterProfile { SodiumMgL = 65, ChlorideMgL = 45 });

        Assert.Equal("warnung", Punkt(ampel, "sodiumMgL").Stufe);
        Assert.Equal("hinweis", Punkt(ampel, "chlorideMgL").Stufe);
        Assert.Contains("50 mg/L", Punkt(ampel, "sodiumMgL").Quelle);
    }
}
