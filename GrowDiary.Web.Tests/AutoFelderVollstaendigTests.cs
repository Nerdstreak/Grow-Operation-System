using System.Reflection;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Jedes automatisch geschriebene Feld hat eine Sperre — und keine ist weiter
/// als die Physik.
///
/// <para><b>Der Anlass.</b> Es gab drei physikalische Grenztabellen, die sich
/// widersprachen: die des Handwegs, die des Auto-Wegs, und eine dritte, die als
/// „eine Tabelle, zwei Leser" gedacht war und für ihren eigenen Nachbarn tot
/// blieb. Bei 7 von 11 Auto-Feldern wichen die Zahlen ab. Beim ORP in beide
/// Richtungen: 1100 mV kam automatisch durch, war von Hand aber gesperrt.</para>
///
/// <para><b>Warum Teilmenge und nicht Gleichheit.</b> Was unbeaufsichtigt
/// geschrieben wird, darf strenger geprüft werden als das, was jemand tippt und
/// dabei ansieht. Bei sechs der sieben abweichenden Felder ist das Auto-Band das
/// engere — sie gleichzusetzen wäre eine Lockerung, und ein defekter Sensor
/// dürfte danach pH 13 und 55 °C schreiben.</para>
/// </summary>
public sealed class AutoFelderVollstaendigTests
{
    private readonly AutoMeasurementValueGuard _guard = new();

    /// <summary>Felder ohne physikalische Obergrenze — jeweils mit Grund.</summary>
    private static readonly Dictionary<AutoMeasurementField, string> GewollteAusnahmen = new()
    {
        [AutoMeasurementField.ReservoirLevelLiters] = "Nach oben offen — Tankgrößen reichen von 20 bis über 1000 Liter",
        [AutoMeasurementField.ReservoirLevelCm] = "Nach oben offen — hängt an der Bauhöhe des Behälters",
    };

    /// <summary>Ein Wert, den es für dieses Feld nicht geben kann.</summary>
    private static double UnmoeglicherWert(AutoMeasurementField feld) => feld switch
    {
        AutoMeasurementField.HumidityPercent => 500,
        AutoMeasurementField.ReservoirPh => 99,
        AutoMeasurementField.DissolvedOxygenMgL => 900,
        AutoMeasurementField.OrpMv => 99999,
        AutoMeasurementField.Co2Ppm => -500,
        _ => 99999,
    };

    public static IEnumerable<object[]> AutoFelder()
        => Enum.GetValues<AutoMeasurementField>().Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(AutoFelder))]
    public void Jedes_Auto_Feld_lehnt_physikalisch_Unmoegliches_ab(AutoMeasurementField feld)
    {
        if (GewollteAusnahmen.ContainsKey(feld)) return;

        var ergebnis = _guard.Check(feld, UnmoeglicherWert(feld));

        Assert.False(ergebnis.IsValid,
            $"{feld} nimmt den unmöglichen Wert {UnmoeglicherWert(feld)} widerspruchslos an. "
            + "Entweder in AutoMeasurementValueGuard.PhysikSchluessel eintragen "
            + "oder mit Grund in GewollteAusnahmen aufnehmen.");
    }

    [Theory]
    [MemberData(nameof(AutoFelder))]
    public void Kein_Auto_Band_ist_weiter_als_die_Physik(AutoMeasurementField feld)
    {
        // Die Richtung ist wichtig: das Auto-Band DARF enger sein (unbeaufsichtigt
        // geschrieben), aber nie weiter. Weiter hiesse, dass ein Sensor etwas
        // schreiben darf, was der Nutzer von Hand nicht eintragen könnte.
        var schluessel = PhysikSchluesselFuerTest(feld);
        if (schluessel is null) return;

        var g = MeasurementSanityService.PhysikalischeGrenzen[schluessel];

        // Knapp ausserhalb der Physik — muss abgelehnt werden, egal wie das
        // eigene Band aussieht.
        Assert.False(_guard.Check(feld, g.Min - 0.01).IsValid, $"{feld} lässt {g.Min - 0.01} zu, die Physik erlaubt erst ab {g.Min}.");
        Assert.False(_guard.Check(feld, g.Max + 0.01).IsValid, $"{feld} lässt {g.Max + 0.01} zu, die Physik erlaubt nur bis {g.Max}.");
    }

    [Fact]
    public void Der_Test_sieht_seine_Grundmengen()
    {
        // Sonst prüft er nichts und ist trotzdem grün — die Falle, in die seine
        // Vorgänger gelaufen sind.
        Assert.True(Enum.GetValues<AutoMeasurementField>().Length >= 11);
        Assert.True(MeasurementSanityService.PhysikalischeGrenzen.Count >= 10);
    }

    /// <summary>
    /// Dieselbe Zuordnung wie im Wächter — bewusst hier nachgebaut.
    /// </summary>
    /// <remarks>
    /// Der Wächter hält sie privat, und öffentlich zu machen, was nur ein Test
    /// braucht, wäre der falsche Weg herum. Läuft die Zuordnung auseinander,
    /// schlägt der Test darüber an: er prüft gegen die Grenzen, die der Wächter
    /// tatsächlich anwendet, nicht gegen die, die hier stehen.
    /// </remarks>
    private static string? PhysikSchluesselFuerTest(AutoMeasurementField feld) => feld switch
    {
        AutoMeasurementField.AirTemperatureC => "air-temp",
        AutoMeasurementField.HumidityPercent => "humidity",
        AutoMeasurementField.ReservoirPh => "ph",
        AutoMeasurementField.ReservoirEc => "ec",
        AutoMeasurementField.ReservoirWaterTempC => "water-temp",
        AutoMeasurementField.DissolvedOxygenMgL => "do",
        AutoMeasurementField.OrpMv => "orp",
        AutoMeasurementField.PpfdMol => "ppfd",
        AutoMeasurementField.Co2Ppm => "co2",
        _ => null,
    };
}
