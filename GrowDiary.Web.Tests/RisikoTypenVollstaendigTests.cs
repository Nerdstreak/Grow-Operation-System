using System.Text.RegularExpressions;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Kein Empfehler verzweigt auf einen Ereignistyp, den niemand erzeugt.
///
/// <para><b>Der Anlass.</b> <see cref="RiskEventType"/> hat neun Werte. Erzeugt
/// wurden drei. Der Notfall-Empfehler verzweigte auf fünf — vier davon setzte
/// kein Erzeuger je. Gemessen an der laufenden App: 21 Risiko-Ereignisse, und
/// <b>0 davon</b> bekamen eine SOP-Empfehlung. Der Ablauf
/// „emergency-power-recovery" lag vollständig in der Wissensbasis und war auf
/// diesem Weg unerreichbar.</para>
///
/// <para>Dazu kam: die Wächter für Pumpe und Home-Assistant-Verbindung
/// schickten nur eine Push-Nachricht und legten gar kein Ereignis an. Wer sie
/// in der Ruhezeit verpasste, fand in der App nichts.</para>
///
/// <para><b>Warum über den Quelltext.</b> „Wird dieser Enum-Wert je gesetzt?"
/// lässt sich zur Laufzeit nicht beantworten — man müsste jede Störung
/// auslösen. Im Quelltext ist die Frage einfach: kommt der Wert außerhalb einer
/// switch-Verzweigung überhaupt vor?</para>
/// </summary>
public sealed class RisikoTypenVollstaendigTests
{
    /// <summary>Typen ohne automatischen Erzeuger — jeweils mit Grund.</summary>
    private static readonly Dictionary<RiskEventType, string> GewollteAusnahmen = new()
    {
        [RiskEventType.Other] = "Sammelfall der Abweichungs-Analyse für alles ohne eigenen Typ",
        [RiskEventType.SensorUnavailable] = "Kommt bewusst nur von Hand über die Risiko-API",
        [RiskEventType.PowerOutage] = "Braucht ein Signal, das Home Assistant heute nicht liefert — die USV meldet den Ausfall, nicht der Strom selbst",
    };

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }

    private static string[] Quelldateien()
        => Directory.GetFiles(Path.Combine(ProjektWurzel(), "GrowDiary.Web"), "*.cs", SearchOption.AllDirectories);

    /// <summary>
    /// Wird dieser Typ irgendwo GESETZT — nicht bloß abgefragt?
    /// </summary>
    /// <remarks>
    /// Ein <c>case</c> und ein <c>=&gt;</c> in einem switch sind Abfragen. Setzen
    /// heißt: als Wert zugewiesen oder als Argument übergeben.
    /// </remarks>
    private static bool WirdGesetzt(RiskEventType typ, IEnumerable<string> dateien)
    {
        var name = $"RiskEventType.{typ}";
        var verzweigung = new Regex(@"(case\s+RiskEventType\.\w+|RiskEventType\.\w+\s*(when\b|=>))");

        foreach (var datei in dateien)
        {
            foreach (var zeile in File.ReadAllLines(datei))
            {
                if (!zeile.Contains(name, StringComparison.Ordinal)) continue;

                // Kommentare zählen nicht.
                //
                // Beim Schreiben dieses Tests selbst passiert: eine XML-Doku im
                // neuen Dienst erwähnte `RiskEventType.UpsOnBattery`, und der
                // Test hielt die Erwähnung für einen Erzeuger. Er lief grün,
                // während der Typ weiterhin von niemandem gesetzt wurde — genau
                // die Fehlerklasse, gegen die er gebaut ist.
                var blank = zeile.TrimStart();
                if (blank.StartsWith("//", StringComparison.Ordinal)
                    || blank.StartsWith("*", StringComparison.Ordinal)
                    || zeile.Contains("<see cref=", StringComparison.Ordinal))
                {
                    continue;
                }

                // Verzweigungen zählen nicht als Erzeugung.
                if (verzweigung.IsMatch(zeile)) continue;
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<object[]> Typen()
        => Enum.GetValues<RiskEventType>().Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Typen))]
    public void Jeder_Risiko_Typ_hat_einen_Erzeuger_oder_einen_Grund(RiskEventType typ)
    {
        if (GewollteAusnahmen.ContainsKey(typ)) return;

        Assert.True(WirdGesetzt(typ, Quelldateien()),
            $"RiskEventType.{typ} wird nirgends gesetzt — es kann also nie ein solches Ereignis geben. "
            + "Entweder einen Erzeuger bauen oder den Typ mit Grund in GewollteAusnahmen eintragen.");
    }

    [Fact]
    public void Der_Notfall_Empfehler_verzweigt_nur_auf_erzeugbare_Typen()
    {
        // Das ist der eigentliche Fund: eine Verzweigung auf einen Typ, den
        // niemand setzt, ist toter Code, der wie eine gebaute Funktion aussieht.
        var dateien = Quelldateien();
        var empfehler = dateien.Single(d => Path.GetFileName(d) == "RiskEventSopRecommender.cs");
        var quelltext = File.ReadAllText(empfehler);

        var verzweigt = Enum.GetValues<RiskEventType>()
            .Where(t => quelltext.Contains($"RiskEventType.{t}", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(verzweigt);

        var tot = verzweigt
            .Where(t => !GewollteAusnahmen.ContainsKey(t))
            .Where(t => !WirdGesetzt(t, dateien))
            .ToList();

        Assert.True(tot.Count == 0,
            "Der Notfall-Empfehler verzweigt auf Typen, die kein Erzeuger je setzt — diese Zweige "
            + "können nie erreicht werden:\n  " + string.Join("\n  ", tot));
    }

    [Fact]
    public void Der_Test_sieht_seine_Grundmengen()
    {
        // Sonst prüft er nichts und ist trotzdem grün.
        Assert.True(Enum.GetValues<RiskEventType>().Length >= 9);
        Assert.True(Quelldateien().Length >= 100);
    }
}
