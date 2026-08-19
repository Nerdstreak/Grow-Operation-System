using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Der Wächter für Kühler und USV.
///
/// <para>Beide Größen waren jahrelang mappbar und wurden von keinem Dienst
/// gelesen. Im RDWC ist der Kühler die Kette, die eine Ernte kostet: Kühler aus,
/// Wassertemperatur steigt, gelöster Sauerstoff fällt, Wurzelfäule.</para>
/// </summary>
public sealed class AnlagenWatchServiceTests
{
    private static readonly DateTime Jetzt = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private static Dictionary<string, HomeAssistantState> Zustaende(params (string Key, string State, double? Zahl, int? SeitMinuten)[] werte)
        => werte.ToDictionary(
            w => w.Key,
            w => new HomeAssistantState
            {
                EntityId = "sensor." + w.Key,
                State = w.State,
                NumericValue = w.Zahl,
                LastChanged = w.SeitMinuten is { } m ? Jetzt.AddMinutes(-m) : null,
            });

    [Fact]
    public void Ohne_Kuehler_und_ohne_USV_sagt_der_Waechter_nichts()
    {
        // „Unbekannt heisst Gefahr" waere hier das Gegenteil von hilfreich: die
        // meisten haben weder Kuehler noch USV gemappt.
        Assert.Empty(AnlagenWatchService.Beurteilen(Zustaende(), Jetzt));
    }

    [Fact]
    public void Ein_laufender_Kuehler_ist_in_Ordnung()
    {
        var b = AnlagenWatchService.Beurteilen(Zustaende(("chiller", "on", null, 300)), Jetzt);
        Assert.Equal("ok", Assert.Single(b).Stufe);
    }

    [Fact]
    public void Ein_kurz_ausgeschalteter_Kuehler_ist_noch_keine_Meldung()
    {
        // Ein Kuehler taktet. Jedes Abschalten sofort zu melden waere Rauschen —
        // dieselbe Schonfrist wie bei den Pumpen.
        var b = AnlagenWatchService.Beurteilen(Zustaende(("chiller", "off", null, 5)), Jetzt, schonfristMinuten: 15);
        Assert.Empty(b);
    }

    [Fact]
    public void Ein_laenger_stehender_Kuehler_ist_kritisch_und_nennt_die_Folge()
    {
        var b = Assert.Single(AnlagenWatchService.Beurteilen(Zustaende(("chiller", "off", null, 90)), Jetzt, schonfristMinuten: 15));

        Assert.Equal("kritisch", b.Stufe);
        Assert.Contains("90 Minuten", b.Meldung);
        // Die Folge gehoert in die Meldung: „Kuehler aus" allein sagt niemandem,
        // warum das in zwei Tagen den Lauf kostet.
        Assert.Contains("Sauerstoff", b.Meldung);
        Assert.Contains("Schonfrist", b.Herkunft);
    }

    [Fact]
    public void Eine_USV_am_Netz_mit_voller_Batterie_ist_in_Ordnung()
    {
        var b = Assert.Single(AnlagenWatchService.Beurteilen(
            Zustaende(("ups-status", "online", null, null), ("ups-battery", "100", 100, null)), Jetzt));

        Assert.Equal("ok", b.Stufe);
        Assert.Contains("100 %", b.Meldung);
    }

    [Theory]
    [InlineData("On Battery")]
    [InlineData("onbatt")]
    [InlineData("discharging")]
    [InlineData("OB")]
    public void Die_Schreibweisen_der_Integrationen_werden_alle_erkannt(string zustand)
    {
        // NUT meldet „On Battery", andere „onbatt" oder „discharging". Wer nur
        // eine Schreibweise kennt, verpasst den Stromausfall bei allen anderen.
        var b = Assert.Single(AnlagenWatchService.Beurteilen(
            Zustaende(("ups-status", zustand, null, null), ("ups-battery", "85", 85, null)), Jetzt));

        Assert.Contains("auf Batterie", b.Meldung);
    }

    [Fact]
    public void Auf_Batterie_mit_wenig_Ladung_ist_kritisch()
    {
        var b = Assert.Single(AnlagenWatchService.Beurteilen(
            Zustaende(("ups-status", "on battery", null, null), ("ups-battery", "25", 25, null)), Jetzt));

        Assert.Equal("kritisch", b.Stufe);
        Assert.Contains("25 %", b.Meldung);
    }

    [Fact]
    public void Auf_Batterie_ohne_Ladestand_gilt_als_kritisch_und_sagt_warum()
    {
        // Ohne Ladestand ist unbekannt, wie viel Zeit bleibt. Im Zweifel die
        // dringendere Einstufung — und der Grund steht dabei, damit niemand die
        // Einstufung fuer eine Messung haelt.
        var b = Assert.Single(AnlagenWatchService.Beurteilen(
            Zustaende(("ups-status", "on battery", null, null)), Jetzt));

        Assert.Equal("kritisch", b.Stufe);
        Assert.Contains("kein Ladestand", b.Herkunft);
    }

    [Fact]
    public void Eine_halbleere_Batterie_am_Netz_ist_ein_Hinweis_keine_Stoerung()
    {
        var b = Assert.Single(AnlagenWatchService.Beurteilen(
            Zustaende(("ups-status", "online", null, null), ("ups-battery", "55", 55, null)), Jetzt));

        Assert.Equal("warnung", b.Stufe);
        Assert.Contains("kürzer durch", b.Meldung);
    }

    [Fact]
    public void Jede_Meldung_traegt_ihre_Herkunft()
    {
        // Regel des Projekts: Faustregeln nur mit Etikett. Wer eine Schwelle
        // liest, muss sehen, woher sie kommt.
        var alle = AnlagenWatchService.Beurteilen(
            Zustaende(("chiller", "off", null, 90), ("ups-status", "on battery", null, null), ("ups-battery", "20", 20, null)),
            Jetzt, schonfristMinuten: 15);

        Assert.NotEmpty(alle);
        Assert.All(alle, b => Assert.False(string.IsNullOrWhiteSpace(b.Herkunft)));
    }
}
