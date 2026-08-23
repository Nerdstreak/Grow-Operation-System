using System.Reflection;

namespace GrowDiary.Web.Services;

/// <summary>Aus welchem Build die laufende Programmdatei stammt.</summary>
/// <remarks>
/// <para><b>Warum es das gibt.</b> Eine Prüfung gegen einen alten Stand ist
/// schlimmer als gar keine: sie meldet Erfolg. Am 24.08.2026 lief auf Port 5076
/// noch eine App aus einer früheren Sitzung; der neue Start meldete
/// <c>Now listening on: http://0.0.0.0:5076</c> und antwortete trotzdem nie —
/// die alte Instanz hielt <c>127.0.0.1</c>. Eine halbe Stunde Messung galt
/// einem Stand von vorgestern, und aufgefallen ist es nur, weil ein neuer
/// Endpunkt 404 gab.</para>
///
/// <para><b>Woher die Kennung kommt.</b> Aus dem Feld <c>TimeDateStamp</c> im
/// PE-Kopf der Assembly — nicht aus der Dateizeit, die ein Kopiervorgang
/// verändert, und nicht aus einer Konstante, die jemand von Hand pflegen
/// müsste. Bei einem <i>deterministischen</i> Build steht dort ein Hash über
/// die Eingaben und kein Datum. Deshalb heisst es Kennung und wird auf
/// <b>Gleichheit</b> verglichen, nie auf früher oder später.</para>
/// </remarks>
public static class Bauzeit
{
    /// <summary>Die Kennung des Builds, aus dem die laufende Assembly stammt.</summary>
    /// <remarks>
    /// Acht Hexziffern aus dem PE-Feld <c>TimeDateStamp</c>. Bewusst als Text
    /// und nicht als Datum: bei deterministischem Build steht dort ein Hash.
    /// </remarks>
    public static readonly string Kennung = Lesen().ToString("x8");

    private static uint Lesen()
    {
        var pfad = Assembly.GetExecutingAssembly().Location;

        // Einzeldatei-Veröffentlichung hat keinen Pfad zur Assembly. Dann ist
        // die Prozess-Datei die nächstbeste Wahrheit — und wenn auch die fehlt,
        // lieber der Startzeitpunkt als eine Ausnahme im Gesundheits-Endpunkt.
        if (string.IsNullOrEmpty(pfad) || !File.Exists(pfad))
        {
            pfad = Environment.ProcessPath ?? string.Empty;
        }

        // Ohne lesbare Datei gibt es keine ehrliche Kennung. Dann lieber eine,
        // die bei jedem Start anders ist, als eine erfundene Konstante: so
        // faellt eine Pruefung auf, statt still durchzugehen.
        if (string.IsNullOrEmpty(pfad) || !File.Exists(pfad)) return 0;

        try
        {
            return PeZeitstempel(pfad) ?? 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Das Feld <c>TimeDateStamp</c> aus dem PE-Kopf holen.</summary>
    /// <remarks>
    /// Der Aufbau steht fest: bei <c>0x3C</c> liegt der Zeiger auf die
    /// PE-Signatur, vier Byte weiter beginnt der COFF-Kopf, und dessen Feld
    /// <c>TimeDateStamp</c> steht ab Byte 4 — Sekunden seit 1970.
    /// </remarks>
    private static uint? PeZeitstempel(string pfad)
    {
        using var strom = File.OpenRead(pfad);
        using var leser = new BinaryReader(strom);

        if (strom.Length < 0x40) return null;
        strom.Position = 0x3C;
        var peBeginn = leser.ReadInt32();

        if (peBeginn <= 0 || peBeginn + 8 > strom.Length) return null;
        strom.Position = peBeginn;
        if (leser.ReadUInt32() != 0x0000_4550) return null;   // "PE\0\0"

        strom.Position = peBeginn + 8;
        var stempel = leser.ReadUInt32();
        return stempel == 0 ? null : stempel;
    }
}
