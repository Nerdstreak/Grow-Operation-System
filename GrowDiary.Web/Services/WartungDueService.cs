using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>Etwas, das gewartet, getauscht oder gesichert gehört.</summary>
public sealed record WartungsPunkt(
    string Bereich,
    string Titel,
    string Stufe,
    string Meldung,
    string Herkunft);

/// <summary>
/// Liest die Termine, die bisher nur herumlagen: Lebensdauer, Prüfintervall, Sicherung.
/// </summary>
/// <remarks>
/// <para><b>Dasselbe Muster wie beim Wasserwechsel.</b> An jedem Gerät stehen
/// <c>ExpectedLifespanDays</c> und <c>InspectionIntervalDays</c> — sie werden aus
/// der Verschleiss-Vorlage vorbefüllt, gespeichert, angezeigt und von keinem
/// Dienst je ausgewertet. Wer keinen Wartungstermin von Hand anlegt, wird nie
/// erinnert; der Luftstein sitzt dann drei Jahre im Eimer.</para>
///
/// <para><b>Die Zahlen sind seine eigenen.</b> Hier wird nichts über
/// Lebensdauern behauptet — gerechnet wird mit dem, was am Gerät steht (aus der
/// Vorlage oder von Hand). Nur die Sicherungs-Frist ist eine Setzung, und die
/// sagt der Text auch.</para>
/// </remarks>
public sealed class WartungDueService
{
    /// <summary>Ab wann eine Sicherung als alt gilt — Faustregel, keine Wissenschaft.</summary>
    public const int SicherungAlterTage = 30;

    /// <summary>Ab welchem Anteil der Lebensdauer vorgewarnt wird.</summary>
    /// <remarks>Bei 90 % bleibt Zeit zum Bestellen, bevor das Teil wirklich fällig ist.</remarks>
    private const double VorwarnAnteil = 0.9;

    private readonly HardwareRepository _hardware;
    private readonly AppPaths _paths;
    private readonly SopDueService _stufe;

    public WartungDueService(HardwareRepository hardware, AppPaths paths, SopDueService stufe)
    {
        _hardware = hardware;
        _paths = paths;
        _stufe = stufe;
    }

    public IReadOnlyList<WartungsPunkt> Offen(DateTime nowUtc)
    {
        // Wartung ist eine Erinnerung, keine Gefahrenmeldung — anders als der
        // Pumpen-Waechter richtet sie sich deshalb nach der Begleitungsstufe.
        var stufe = _stufe.Stufe;
        if (stufe == "expert") return [];

        var geraete = _hardware.GetHardwareItems()
            .Where(g => g.Status == HardwareItemStatus.Active)
            .ToList();

        var zuletztGeprueft = new Dictionary<int, DateTime>();
        foreach (var ereignis in _hardware.GetMaintenanceEvents())
        {
            if (ereignis.Status != MaintenanceEventStatus.Completed || ereignis.PerformedAtUtc is not { } wann) continue;
            if (!zuletztGeprueft.TryGetValue(ereignis.HardwareItemId, out var bisher) || wann > bisher)
            {
                zuletztGeprueft[ereignis.HardwareItemId] = wann;
            }
        }

        var punkte = Beurteilen(geraete, zuletztGeprueft, LetzteSicherung(), nowUtc);
        return stufe == "important"
            ? punkte.Where(p => p.Stufe == "kritisch").ToList()
            : punkte;
    }

    /// <summary>Wann zuletzt gesichert wurde — die Datei selbst ist der Beleg.</summary>
    /// <remarks>
    /// Kein eigener Zeitstempel in den Einstellungen: der wäre eine zweite
    /// Wahrheit, die auseinanderlaufen kann. Was zählt, ist, ob eine Sicherung
    /// wirklich daliegt.
    /// </remarks>
    public DateTime? LetzteSicherung()
    {
        var ordner = _paths.BackupsPath;
        if (!Directory.Exists(ordner)) return null;

        DateTime? neueste = null;
        foreach (var datei in Directory.EnumerateFiles(ordner, "*.zip"))
        {
            var wann = File.GetLastWriteTimeUtc(datei);
            if (neueste is null || wann > neueste) neueste = wann;
        }
        return neueste;
    }

    /// <summary>
    /// Die reine Rechnung — ohne Datenbank, ohne Dateisystem.
    /// </summary>
    public static IReadOnlyList<WartungsPunkt> Beurteilen(
        IReadOnlyList<HardwareItem> geraete,
        IReadOnlyDictionary<int, DateTime> zuletztGeprueft,
        DateTime? letzteSicherung,
        DateTime nowUtc)
    {
        var punkte = new List<WartungsPunkt>();

        foreach (var geraet in geraete)
        {
            if (geraet.InstalledAtUtc is not { } eingebaut) continue;
            var tageSeitEinbau = (int)(nowUtc.Date - eingebaut.Date).TotalDays;

            if (geraet.ExpectedLifespanDays is > 0 and { } lebensdauer)
            {
                if (tageSeitEinbau >= lebensdauer)
                {
                    punkte.Add(new WartungsPunkt(
                        "Verschleiß", geraet.Name, "kritisch",
                        $"{geraet.Name}: seit {tageSeitEinbau} Tagen im Einsatz, vorgesehen sind {lebensdauer}. Tausch fällig.",
                        "Lebensdauer aus deinem Geräte-Eintrag, gerechnet ab Einbaudatum."));
                }
                else if (tageSeitEinbau >= lebensdauer * VorwarnAnteil)
                {
                    var rest = lebensdauer - tageSeitEinbau;
                    punkte.Add(new WartungsPunkt(
                        "Verschleiß", geraet.Name, "warnung",
                        $"{geraet.Name}: noch {rest} von {lebensdauer} Tagen. Ersatz jetzt bestellen, dann liegt er da, wenn er gebraucht wird.",
                        "Lebensdauer aus deinem Geräte-Eintrag; Vorwarnung bei 90 %."));
                }
            }

            if (geraet.InspectionIntervalDays is > 0 and { } intervall)
            {
                var zuletzt = zuletztGeprueft.TryGetValue(geraet.Id, out var geprueft) ? geprueft : eingebaut;
                var seit = (int)(nowUtc.Date - zuletzt.Date).TotalDays;
                if (seit >= intervall)
                {
                    var nieGeprueft = !zuletztGeprueft.ContainsKey(geraet.Id);
                    punkte.Add(new WartungsPunkt(
                        "Prüfung", geraet.Name, seit >= intervall * 2 ? "kritisch" : "warnung",
                        $"{geraet.Name}: {(nieGeprueft ? "seit dem Einbau" : "zuletzt")} vor {seit} Tagen geprüft (Plan: alle {intervall}).",
                        nieGeprueft
                            ? "Prüfintervall aus deinem Geräte-Eintrag; ohne Prüfeintrag zählt das Einbaudatum."
                            : "Prüfintervall aus deinem Geräte-Eintrag, gerechnet ab der letzten abgeschlossenen Wartung."));
                }
            }
        }

        // Die Sicherung zum Schluss: sie betrifft kein Geraet, sondern alles.
        if (letzteSicherung is { } sicherung)
        {
            var alter = (int)(nowUtc.Date - sicherung.Date).TotalDays;
            if (alter >= SicherungAlterTage)
            {
                punkte.Add(new WartungsPunkt(
                    "Sicherung", "Datensicherung", alter >= SicherungAlterTage * 3 ? "kritisch" : "warnung",
                    $"Letzte Sicherung vor {alter} Tagen. Alles seither — Messungen, Journal, Ernten — hängt an einer SD-Karte.",
                    $"Faustregel: nach {SicherungAlterTage} Tagen erinnern. Unter Einstellungen sicherst du in einem Klick."));
            }
        }
        else
        {
            punkte.Add(new WartungsPunkt(
                "Sicherung", "Datensicherung", "kritisch",
                "Es liegt noch keine Sicherung vor. Geht die Karte kaputt, ist alles weg — jede Messung, jedes Journal, jede Ernte.",
                "Keine Datei unter App_Data/backups gefunden."));
        }

        return punkte
            .OrderByDescending(p => p.Stufe == "kritisch")
            .ThenBy(p => p.Bereich)
            .ToList();
    }
}
