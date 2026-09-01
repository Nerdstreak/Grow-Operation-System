using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Ein eigenes Sollwert-Profil nimmt keine unmöglichen Zahlen an.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> <c>SetpointProfilesApiController</c>
/// prüfte Name und Basisprofil — die <b>Werte</b> gar nicht. Angenommen wurde
/// alles, was eine endliche Zahl ist.</para>
///
/// <para><b>Zwei Wege, die daran hängen:</b></para>
/// <list type="bullet">
///   <item><b>Vertauschte Grenzen.</b> <c>phMin 6,5</c> mit <c>phMax 5,5</c>
///   landet über <c>TargetValueService.GetTargets</c> im Urteil
///   <c>wert &lt; min ? Below : wert &gt; max ? Above : InTarget</c> — <b>jede</b>
///   Messung ist damit „daneben", egal welcher Wert. Dieselbe Klasse wie die
///   vertauschten Alarmgrenzen, die heute schon abgelehnt werden.</item>
///   <item><b>Werte, die es nicht geben kann.</b>
///   <c>waterTempNightC</c> geht über <c>NachtabsenkungService</c> und
///   <c>NachtabsenkungWriter</c> an das echte Zielgerät in Home Assistant —
///   also an den Kühler im Zelt. −40 °C oder 400 °C wurden bis hierher
///   durchgereicht.</item>
/// </list>
///
/// <para><b>Die Grenzen stehen nicht hier.</b> Sie kommen aus
/// <see cref="MeasurementSanityService.PhysikalischeGrenzen"/> — derselben
/// Tabelle, die beim Speichern einer Messung sperrt. Zwei Tabellen für dieselbe
/// Frage wären zwei Wahrheiten.</para>
/// </remarks>
public sealed class SollwertProfilNimmtKeinenUnsinnTests
{
    /// <summary>
    /// Jedes Min/Max-Paar der Profiltabelle hat eine Messgrösse mit Grenzen.
    /// </summary>
    /// <remarks>
    /// Die eigentliche Zählung: kommt ein Feld dazu, ohne dass jemand die
    /// Zuordnung pflegt, fällt das hier auf — und nicht erst, wenn ein
    /// unmöglicher Wert am Kühler ankommt.
    /// </remarks>
    [Fact]
    public void JedesProfilfeld_KennntSeineMessgroesse()
    {
        var felder = SetpointProfile.Fields;

        // Mengenwaechter: ohne Grundmenge prueft die Schleife nichts.
        Assert.True(felder.Count >= 10,
            $"Nur {felder.Count} Profilfelder gefunden — die Grundmenge stimmt nicht.");

        var ohne = felder
            .Where(feld => SetpointProfilGrenzen.MessgroesseFuer(feld) is null)
            .ToList();

        Assert.True(ohne.Count == 0,
            "Diese Profilfelder haben keine zugeordnete Messgroesse und damit keine "
            + "physikalische Grenze: " + string.Join(", ", ohne)
            + ". Ein Feld ohne Grenze wird ungeprueft gespeichert — und waterTempNightC "
            + "geht von dort an den echten Kuehler.");
    }

    /// <summary>Ein Wert ausserhalb der Physik wird abgelehnt.</summary>
    [Theory]
    [InlineData("phMin", -3.0)]
    [InlineData("phMax", 20.0)]
    [InlineData("waterTempNightC", -40.0)]
    [InlineData("waterTempDayC", 400.0)]
    [InlineData("ecMax", 99.0)]
    [InlineData("co2Max", 90000.0)]
    public void EinUnmoeglicherWert_WirdAbgelehnt(string feld, double wert)
    {
        var maengel = SetpointProfilGrenzen.Pruefe(
            new Dictionary<string, Dictionary<string, double>>
            {
                ["Flower"] = new() { [feld] = wert },
            });

        Assert.True(maengel.Count > 0,
            $"{feld} = {wert} wurde angenommen. Diese Zahl kann es nicht geben, und "
            + "waterTempNightC geht von hier an das Zielgeraet in Home Assistant.");
    }

    /// <summary>Vertauschte Grenzen werden abgelehnt.</summary>
    [Fact]
    public void VertauschteGrenzen_WerdenAbgelehnt()
    {
        var maengel = SetpointProfilGrenzen.Pruefe(
            new Dictionary<string, Dictionary<string, double>>
            {
                ["Flower"] = new() { ["phMin"] = 6.5, ["phMax"] = 5.5 },
            });

        Assert.True(maengel.Count > 0,
            "phMin 6,5 mit phMax 5,5 wurde angenommen. Danach ist JEDE pH-Messung "
            + "„daneben\", egal welcher Wert — die Rechnung lautet "
            + "wert < min ? Below : wert > max ? Above : InTarget.");
    }

    /// <summary>
    /// Ein gewöhnliches Profil geht durch.
    /// </summary>
    /// <remarks>
    /// Die Gegenrichtung: eine Prüfung, die alles ablehnt, besteht die Fälle
    /// darüber ebenfalls — und macht die Seite unbenutzbar.
    /// </remarks>
    [Fact]
    public void EinGewoehnlichesProfil_GehtDurch()
    {
        var maengel = SetpointProfilGrenzen.Pruefe(
            new Dictionary<string, Dictionary<string, double>>
            {
                ["Flower"] = new()
                {
                    ["phMin"] = 5.8, ["phMax"] = 6.2,
                    ["ecMin"] = 1.0, ["ecMax"] = 1.2,
                    ["waterTempDayC"] = 20, ["waterTempNightC"] = 18,
                },
            });

        Assert.True(maengel.Count == 0,
            "Ein gewoehnliches RDWC-Bluete-Profil wurde abgelehnt: "
            + string.Join(" | ", maengel.Select(m => $"{m.Feld}: {m.Meldung}")));
    }
}
