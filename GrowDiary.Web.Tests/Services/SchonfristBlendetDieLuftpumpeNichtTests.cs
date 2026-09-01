using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Schonfrist der Umwälzpumpe blendet den Luftpumpen-Alarm nicht.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> <c>Beurteilen</c> wandte <b>eine</b>
/// Schonfrist auf <b>beide</b> Pumpen an. Begründet ist die Einstellbarkeit
/// aber nur für die Umwälzung — im Code steht wörtlich: „Wer seine Umwälzung im
/// Intervall fährt, stellt sie höher."</para>
///
/// <para><b>Und die App rät selbst dazu.</b> Der Satz unter jeder
/// Umwälz-Warnung lautet: „Wenn das Absicht ist (Intervall-Betrieb), stell die
/// Schonfrist höher." Wer dem folgt und 240 Minuten einträgt, hat damit auch
/// den Alarm für die <b>Luftpumpe</b> um vier Stunden verzögert — den Alarm,
/// über den dieselbe Klasse schreibt: „Ohne Belüftung wird das Reservoir binnen
/// Stunden sauerstoffarm; Wurzelfäule kann einen Lauf in rund zwei Tagen
/// erledigen."</para>
///
/// <para>Fällt die Luftpumpe um 23 Uhr aus, kam die Meldung damit um 3 Uhr statt
/// um 23:15. Zulässig sind bis zu 720 Minuten — im schlimmsten Fall knapp zwölf
/// Stunden.</para>
///
/// <para><b>Die Regel.</b> Eine eigene Schonfrist darf den lebenswichtigen
/// Alarm nur <i>schärfer</i> stellen, nie stumpfer.</para>
/// </remarks>
public sealed class SchonfristBlendetDieLuftpumpeNichtTests
{
    private static readonly DateTime Jetzt = new(2026, 9, 1, 23, 15, 0, DateTimeKind.Utc);

    /// <summary>
    /// Die Luftpumpe steht seit einer Stunde — bei jeder erlaubten Schonfrist gemeldet.
    /// </summary>
    /// <remarks>
    /// 240 ist der Wert, zu dem die App im Meldungstext selbst rät; 720 ist die
    /// Obergrenze, die der Setter zulässt.
    /// </remarks>
    [Theory]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(240)]
    [InlineData(720)]
    public void LuftpumpeSteht_WirdGemeldet_EgalWieHochDieSchonfristSteht(int schonfrist)
    {
        var befunde = PumpWatchService.Beurteilen(
            Lage(luftAus: true, umwaelzungAus: false, seit: Jetzt.AddMinutes(-60)),
            Jetzt, schonfrist);

        var luft = befunde.FirstOrDefault(b => b.Schluessel == "pump-air");

        Assert.True(luft is not null,
            $"Die Luftpumpe steht seit 60 Minuten, und bei einer Schonfrist von {schonfrist} "
            + "Minuten meldet die App nichts. Genau diese Zahl schlaegt sie unter jeder "
            + "Umwaelz-Warnung selbst vor — wer dem folgt, schaltet den einzigen Alarm ab, "
            + "der in zwei Tagen den ganzen Lauf kostet.");
        Assert.True(luft!.Stufe == "kritisch",
            $"Der Luftpumpen-Ausfall ist mit Stufe „{luft.Stufe}\" gemeldet statt „kritisch\".");
    }

    /// <summary>
    /// Eine strengere Schonfrist gilt auch für die Luftpumpe.
    /// </summary>
    /// <remarks>
    /// Wer 5 Minuten einträgt, will frü&#x68;er gewarnt werden — nicht später. Die
    /// Deckelung darf nur nach oben wirken.
    /// </remarks>
    [Fact]
    public void EineStrengereSchonfrist_GiltAuchFuerDieLuftpumpe()
    {
        var befunde = PumpWatchService.Beurteilen(
            Lage(luftAus: true, umwaelzungAus: false, seit: Jetzt.AddMinutes(-7)),
            Jetzt, schonfristMinuten: 5);

        Assert.Contains(befunde, b => b.Schluessel == "pump-air");
    }

    /// <summary>
    /// Und die Umwälzpumpe behält ihre eingestellte Schonfrist.
    /// </summary>
    /// <remarks>
    /// Sonst waere die Reparatur oben eine Verschlimmbesserung: der
    /// Intervall-Betrieb ist der Grund, warum es die Einstellung gibt.
    /// </remarks>
    [Fact]
    public void DieUmwaelzpumpe_BehaeltIhreEingestellteSchonfrist()
    {
        var befunde = PumpWatchService.Beurteilen(
            Lage(luftAus: false, umwaelzungAus: true, seit: Jetzt.AddMinutes(-60)),
            Jetzt, schonfristMinuten: 240);

        Assert.DoesNotContain(befunde, b => b.Schluessel == "pump-circulation");
    }

    /// <summary>
    /// Der Waechter sieht ueberhaupt etwas — sonst prueft alles oben nichts.
    /// </summary>
    /// <remarks>
    /// Mengenwächter: liefe <c>Beurteilen</c> für diese Lage grundsätzlich leer
    /// aus (falsche Kennungen, geänderte Schwelle), wären die Fälle darüber
    /// still grün.
    /// </remarks>
    [Fact]
    public void DerWaechterSiehtDieLageUeberhaupt()
    {
        var befunde = PumpWatchService.Beurteilen(
            Lage(luftAus: true, umwaelzungAus: true, seit: Jetzt.AddMinutes(-600)),
            Jetzt, schonfristMinuten: 15);

        Assert.True(befunde.Count == 2,
            $"Bei zwei stehenden Pumpen und 600 Minuten Stillstand kommen {befunde.Count} "
            + "Befunde heraus statt zwei. Stimmen die Kennungen noch?");
    }

    private static Dictionary<string, HomeAssistantState> Lage(bool luftAus, bool umwaelzungAus, DateTime seit)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["pump-air"] = Zustand(luftAus ? "off" : "on", seit),
            ["pump-circulation"] = Zustand(umwaelzungAus ? "off" : "on", seit),
        };

    private static HomeAssistantState Zustand(string wert, DateTime seit)
        => new() { State = wert, LastChanged = seit, LastUpdated = seit };
}
