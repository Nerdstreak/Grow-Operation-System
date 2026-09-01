using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Ein kalibriertes Volumen wird nicht noch einmal dazugerechnet.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Der Kalibrier-Assistent misst, was
/// beim Füllen des <b>ganzen</b> Systems durch die Wasseruhr gelaufen ist —
/// Töpfe, Rohre und Reservoir zusammen. <c>Finish</c> schreibt diese Zahl nach
/// <c>ReservoirLiters</c>, und <c>CalculateTotalVolumeLiters</c> rechnet
/// darauf das geschätzte Topfvolumen <b>noch einmal</b> obendrauf.</para>
///
/// <para>Bei vier Töpfen à 20 L und gemessenen 160 L stand danach
/// 4 × 20 + 160 = <b>240 L</b> als Betriebsvolumen — 50 % zu viel, obwohl der
/// Nutzer gerade nachgemessen hat. Die Zahl steht auf der Hydro-Karte und geht
/// in die Addback-Rechnung.</para>
///
/// <para><b>Die Regel.</b> Wo gemessen wurde, gilt die Messung. Eine Schätzung
/// aus Topfzahl mal Topfgröße ist genau so lange richtig, wie niemand
/// nachgesehen hat.</para>
/// </remarks>
public sealed class GemessenesVolumenSchlaegtDieSchaetzungTests
{
    /// <summary>Nach der Kalibrierung zählt die gemessene Zahl — allein.</summary>
    [Fact]
    public void NachDerKalibrierung_ZaehltDieGemesseneZahl()
    {
        var system = KalibriertesSystem(topfZahl: 4, topfGroesse: 20, gemessen: 160);

        var dto = system.ToDto();

        Assert.True(dto.TotalVolumeLiters is not null, "Das System hat gar kein Betriebsvolumen.");
        Assert.True(Math.Abs(dto.TotalVolumeLiters!.Value - 160) < 0.01,
            $"Gemessen wurden 160 L, angezeigt werden {dto.TotalVolumeLiters:0.#} L. "
            + "Das geschaetzte Topfvolumen wurde auf die Messung draufgerechnet — die Zahl "
            + "steht auf der Hydro-Karte und geht in die Addback-Rechnung.");
    }

    /// <summary>
    /// Ohne Kalibrierung bleibt es bei der Schätzung.
    /// </summary>
    /// <remarks>
    /// Sonst wäre die Reparatur oben eine Verschlimmbesserung: wer nie
    /// kalibriert hat, braucht die Rechnung aus Topfzahl und Reservoir.
    /// </remarks>
    [Fact]
    public void OhneKalibrierung_BleibtEsBeiDerSchaetzung()
    {
        var system = new GrowSystem
        {
            Name = "RDWC",
            HydroStyle = nameof(GrowDiary.Web.Models.HydroStyle.RDWC),
            PotCount = 4,
            PotSizeLiters = 20,
            ReservoirLiters = 60,
        };

        var dto = system.ToDto();

        Assert.True(dto.TotalVolumeLiters is not null, "Ohne Kalibrierung fehlt das Volumen ganz.");
        Assert.True(Math.Abs(dto.TotalVolumeLiters!.Value - 140) < 0.01,
            $"Ohne Kalibrierung sollten 4 x 20 + 60 = 140 L herauskommen, es sind "
            + $"{dto.TotalVolumeLiters:0.#} L.");
    }

    /// <summary>
    /// Und die beiden Wege unterscheiden sich wirklich.
    /// </summary>
    /// <remarks>
    /// Mengenwächter: kämen Schätzung und Messung ohnehin auf dieselbe Zahl,
    /// bewiese der erste Fall nichts.
    /// </remarks>
    [Fact]
    public void DieBeidenWegeKommenAufVerschiedeneZahlen()
    {
        var kalibriert = KalibriertesSystem(topfZahl: 4, topfGroesse: 20, gemessen: 160).ToDto();
        var geschaetzt = new GrowSystem
        {
            Name = "RDWC", HydroStyle = nameof(GrowDiary.Web.Models.HydroStyle.RDWC),
            PotCount = 4, PotSizeLiters = 20, ReservoirLiters = 160,
        }.ToDto();

        Assert.True(kalibriert.TotalVolumeLiters != geschaetzt.TotalVolumeLiters,
            "Kalibriert und geschaetzt ergeben dieselbe Zahl — dann zeigt der erste Fall "
            + "den Fehler gar nicht.");
    }

    private static GrowSystem KalibriertesSystem(int topfZahl, double topfGroesse, double gemessen)
        => new()
        {
            Name = "RDWC",
            HydroStyle = nameof(GrowDiary.Web.Models.HydroStyle.RDWC),
            PotCount = topfZahl,
            PotSizeLiters = topfGroesse,
            // Was der Assistent schreibt: die gemessene Gesamtmenge.
            ReservoirLiters = gemessen,
            LevelSensorEmptyRaw = 2,
            LevelSensorFullRaw = 48,
            LevelSensorFullLiters = gemessen,
            LevelCalibratedAtUtc = DateTime.UtcNow.AddDays(-1),
        };
}
