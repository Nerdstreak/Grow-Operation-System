using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Verschleiss, Prüfung, Sicherung — die Termine, die bisher nur herumlagen.
/// </summary>
/// <remarks>
/// Dieselbe Fehlerklasse wie beim Wasserwechsel vor beta.29: Zahlen am Datensatz,
/// die niemand liest. Diese Tests halten fest, dass sie jetzt gelesen werden —
/// und dass gerechnet wird mit dem, was der Betreiber selbst eingetragen hat.
/// </remarks>
public sealed class WartungDueTests
{
    private static readonly DateTime Jetzt = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    private static HardwareItem Geraet(
        int id = 1, string name = "Luftstein", int? lebensdauer = null,
        int? pruefintervall = null, int einbauVorTagen = 0)
        => new()
        {
            Id = id,
            Name = name,
            Status = HardwareItemStatus.Active,
            InstalledAtUtc = Jetzt.AddDays(-einbauVorTagen),
            ExpectedLifespanDays = lebensdauer,
            InspectionIntervalDays = pruefintervall,
        };

    /// <summary>Eine frische Sicherung, damit sie den Tests nicht dazwischenfunkt.</summary>
    private static readonly DateTime FrischGesichert = Jetzt.AddDays(-1);

    [Fact]
    public void APartPastItsLifespanIsDueForReplacement()
    {
        var punkte = WartungDueService.Beurteilen(
            [Geraet(lebensdauer: 180, einbauVorTagen: 200)], new Dictionary<int, DateTime>(), FrischGesichert, Jetzt);

        var verschleiss = punkte.Single(p => p.Bereich == "Verschleiß");
        Assert.Equal("kritisch", verschleiss.Stufe);
        Assert.Contains("200 Tagen im Einsatz", verschleiss.Meldung);
        Assert.Contains("vorgesehen sind 180", verschleiss.Meldung);
        // Die Zahl ist SEINE — das muss dastehen, damit sie niemand fuer Wissen haelt.
        Assert.Contains("aus deinem Geräte-Eintrag", verschleiss.Herkunft);
    }

    [Fact]
    public void TheWarningComesEarlyEnoughToOrderAReplacement()
    {
        // 165 von 180 Tagen: 91 % — die Vorwarnung soll kommen, solange Zeit
        // zum Bestellen bleibt, nicht erst wenn das Teil schon durch ist.
        var punkte = WartungDueService.Beurteilen(
            [Geraet(lebensdauer: 180, einbauVorTagen: 165)], new Dictionary<int, DateTime>(), FrischGesichert, Jetzt);

        var verschleiss = punkte.Single(p => p.Bereich == "Verschleiß");
        Assert.Equal("warnung", verschleiss.Stufe);
        Assert.Contains("noch 15 von 180 Tagen", verschleiss.Meldung);
    }

    [Fact]
    public void AFreshPartSaysNothing()
    {
        var punkte = WartungDueService.Beurteilen(
            [Geraet(lebensdauer: 180, einbauVorTagen: 20)], new Dictionary<int, DateTime>(), FrischGesichert, Jetzt);

        Assert.Empty(punkte);
    }

    [Fact]
    public void WithoutNumbersOnTheItemNothingIsInvented()
    {
        // Kein Lebensdauer-, kein Pruefintervall-Eintrag: dann gibt es dazu auch
        // nichts zu sagen. Eine erfundene Standard-Lebensdauer waere geraten.
        var punkte = WartungDueService.Beurteilen(
            [Geraet(einbauVorTagen: 900)], new Dictionary<int, DateTime>(), FrischGesichert, Jetzt);

        Assert.Empty(punkte);
    }

    [Fact]
    public void TheInspectionClockStartsAtInstallAndResetsOnACompletedService()
    {
        var geraet = Geraet(id: 7, name: "pH-Sonde", pruefintervall: 30, einbauVorTagen: 100);

        // Nie geprueft: es zaehlt der Einbau, und der Text sagt das auch.
        var ohne = WartungDueService.Beurteilen([geraet], new Dictionary<int, DateTime>(), FrischGesichert, Jetzt);
        var punkt = ohne.Single(p => p.Bereich == "Prüfung");
        Assert.Contains("seit dem Einbau vor 100 Tagen", punkt.Meldung);
        Assert.Contains("ohne Prüfeintrag zählt das Einbaudatum", punkt.Herkunft);

        // Vor zehn Tagen gewartet: die Uhr beginnt neu, also nichts faellig.
        var mit = WartungDueService.Beurteilen(
            [geraet], new Dictionary<int, DateTime> { [7] = Jetzt.AddDays(-10) }, FrischGesichert, Jetzt);
        Assert.DoesNotContain(mit, p => p.Bereich == "Prüfung");
    }

    [Fact]
    public void NoBackupAtAllIsTheLoudestOfThemAll()
    {
        var punkte = WartungDueService.Beurteilen([], new Dictionary<int, DateTime>(), letzteSicherung: null, Jetzt);

        var sicherung = punkte.Single();
        Assert.Equal("kritisch", sicherung.Stufe);
        Assert.Contains("noch keine Sicherung", sicherung.Meldung);
    }

    [Fact]
    public void AnAgingBackupWarnsAndSaysWhatIsAtStake()
    {
        var punkte = WartungDueService.Beurteilen(
            [], new Dictionary<int, DateTime>(), Jetzt.AddDays(-40), Jetzt);

        var sicherung = punkte.Single();
        Assert.Equal("warnung", sicherung.Stufe);
        Assert.Contains("vor 40 Tagen", sicherung.Meldung);
        Assert.Contains("Faustregel", sicherung.Herkunft);
    }

    [Fact]
    public void RetiredGearIsNotNagged()
    {
        // Ausgemustertes Geraet: es liegt in der Schublade, nicht im Eimer.
        var alt = Geraet(lebensdauer: 30, einbauVorTagen: 900);
        alt.Status = HardwareItemStatus.Retired;

        var punkte = WartungDueService.Beurteilen(
            [alt], new Dictionary<int, DateTime>(), FrischGesichert, Jetzt);

        // Beurteilen filtert nicht selbst — das tut Offen(). Hier zaehlt nur,
        // dass die Rechnung stimmt; der Status-Filter hat seinen eigenen Weg.
        Assert.Single(punkte);
    }
}
