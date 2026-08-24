namespace GrowDiary.Web.Services;

/// <summary>Was der Nutzer nach einem Stellbefehl zu sehen bekommt.</summary>
/// <param name="Ok">Alles bestätigt — der Controller meldet die neuen Werte.</param>
/// <param name="Meldungen">Bei <c>Ok=false</c>: je offenem Schritt ein deutscher Satz.</param>
public sealed record AcStellErgebnisDto(bool Ok, IReadOnlyList<string> Meldungen);

/// <summary>
/// Baut aus den Schreib-Ergebnissen die Antwort an die Oberfläche.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass: „manchmal kommt 502 — aber das Schalten funktioniert."</b>
/// So hat es der Tester am 25.08.2026 gemeldet, mit Bild. Beides stimmte: das
/// Gerät hatte geschaltet, und die Seite zeigte einen Fehler. Die
/// AC-Infinity-Integration holt ihre Werte im Takt aus der Hersteller-Wolke;
/// bis die Entität den neuen Wert <i>zurückmeldet</i>, können Minuten vergehen
/// — länger, als die Nachkontrolle wartet.</para>
///
/// <para><b>„Nicht bestätigt" ist deshalb kein Fehlschlag.</b> Ein Fehlschlag
/// ist es, wenn Home Assistant den Aufruf gar nicht annimmt
/// (<see cref="SendungAbgelehnt"/>) — dann ist nichts passiert. Alles andere
/// ist ein Schwebezustand: gesendet, Bestätigung steht aus. Der bekommt
/// <c>Ok=false</c> mit Klartext und HTTP 200, kein 502 — die Oberfläche zeigt
/// ihn gelb und liest den Stand später von selbst nach.</para>
///
/// <para><b>Und warum der Nutzer vorher englischen Rohtext sah:</b> der
/// Controller gab rohe String-Listen zurück, die App liest aber den
/// Fehlervertrag (<c>ApiError.Message</c>). Ohne <c>message</c> fiel die
/// Anzeige auf „API request failed with status 502" zurück — der deutsche
/// Satz war da und kam nie an.</para>
/// </remarks>
public static class AcStellAntwort
{
    /// <summary>Hat Home Assistant mindestens einen Aufruf gar nicht angenommen?</summary>
    /// <remarks>
    /// Nur dann ist es ein echter Fehler: es wurde nichts gesendet, also auch
    /// nichts geschaltet. Verbindung oder Entität stimmen nicht.
    /// </remarks>
    public static bool SendungAbgelehnt(IReadOnlyList<AcSchrittErgebnis> ergebnisse)
        => ergebnisse.Any(e => !e.Angenommen);

    /// <summary>Die Antwort für die Oberfläche.</summary>
    public static AcStellErgebnisDto Bauen(IReadOnlyList<AcSchrittErgebnis> ergebnisse)
    {
        var offen = ergebnisse
            .Where(e => !e.Bestaetigt)
            .Select(e => $"{e.EntityId}: {e.Fehler ?? "noch keine Bestätigung"}")
            .ToList();

        return new AcStellErgebnisDto(offen.Count == 0, offen);
    }

    /// <summary>Die Zeile fürs Anlagen-Protokoll.</summary>
    public static string ProtokollZeile(
        string zeltName, string geraetName, string was, IReadOnlyList<AcSchrittErgebnis> ergebnisse)
        => $"{zeltName} · {geraetName}: {was} — "
           + string.Join(", ", ergebnisse.Select(e =>
               $"{e.EntityId} {(e.Uebersprungen ? "stand schon" : e.Bestaetigt ? $"ok nach {e.Versuche}" : "NICHT bestaetigt")}"));
}
