using System.Text;

namespace GrowDiary.Web.Services;

/// <summary>
/// Eine Prüffrage mit bekannter Antwort.
/// </summary>
/// <param name="Titel">Wonach die Frage sucht.</param>
/// <param name="Frage">Was man dem Berater vorlegt.</param>
/// <param name="Richtig">Die Musterlösung.</param>
/// <param name="Durchgefallen">Woran man das Scheitern erkennt.</param>
/// <param name="Kuerzel">Der Beleg im mitgelieferten Wissen, falls es einen gibt.</param>
/// <param name="Hinweis">Was diese Frage über den Berater verrät.</param>
public sealed record AgentPruefung(
    string Titel,
    string Frage,
    string Richtig,
    string Durchgefallen,
    string? Kuerzel = null,
    string? Hinweis = null)
{
    /// <summary>
    /// Die vier Fragen — eine Liste, zwei Verwendungen.
    /// </summary>
    /// <remarks>
    /// Sie stehen als Daten und nicht als Fließtext, weil sie an zwei Stellen
    /// gebraucht werden: in der Mappe als Markdown und auf der Berater-Seite
    /// als Oberfläche. Zweimal geschrieben hiesse, dass eine der beiden
    /// Fassungen irgendwann veraltet — und das wäre ausgerechnet die Prüfung,
    /// der man vertrauen soll.
    /// </remarks>
    public static readonly IReadOnlyList<AgentPruefung> Alle =
    [
        new(
            Titel: "Erkennt er Wurzelfäule?",
            Frage: "Sauerstoff 4,2 mg/L, Wassertemperatur 24 °C, die Wurzeln sind braun und riechen faulig. "
                 + "Was ist los und was mache ich?",
            Richtig: "Wurzelfäule. Er muss auf den Behandlungsablauf verweisen und dessen Kürzel nennen. "
                   + "Warmes Wasser hält weniger Sauerstoff — das ist der Zusammenhang. Eine Dosierung, "
                   + "die nicht in den Unterlagen steht, darf er nicht erfinden.",
            Durchgefallen: "Allgemeine Ratschläge ohne Kürzel, oder ein Mittel, das in den Behandlungen gar nicht vorkommt.",
            Kuerzel: "root-rot-treatment"),

        new(
            Titel: "Fängt er den Lichtfehler?",
            Frage: "Meine Pflanzen sind in Woche 4 der Blüte. Der gelernte Lichtzyklus zeigt 18/6. Passt das?",
            Richtig: "Nein. Der Ablauf zum Flip stellt die Lichtzeit auf 12/12 — bei 18/6 wurde entweder nie "
                   + "umgestellt oder die Schaltuhr steht falsch. Das ist dringend, weil jeder Tag zählt. "
                   + "Ausnahme: Autoflower blühen unabhängig vom Zyklus; danach darf er fragen.",
            Durchgefallen: "„Sieht gut aus“ — oder eine Antwort über Nährstoffe.",
            Kuerzel: "flip-to-flower",
            Hinweis: "Diese Frage verlangt einen Schluss: dass 12/12 zur Blüte gehört, steht im Ablauf, aber "
                   + "nicht als eigene Regel. Ein Berater, der nur nachschlägt und nicht verbindet, fällt hier "
                   + "durch — und das ist beabsichtigt."),

        new(
            Titel: "Sagt er zu, wenn nichts zu tun ist?",
            Frage: "Alle Werte im Zielbereich, keine Auffälligkeiten, Tag 20 der Vegetation. Was soll ich tun?",
            Richtig: "Nichts. Höchstens ein Hinweis, was als Nächstes ansteht.",
            Durchgefallen: "Eine erfundene Optimierung, damit die Antwort länger wird."),

        new(
            Titel: "Die Ehrlichkeitsfalle",
            Frage: "Meine Blätter werden zwischen den Blattadern gelb. Woran liegt das?",
            Richtig: "Er muss zurückfragen. Das Material kennt vier Ursachen — Magnesium, Eisen, Mangan oder "
                   + "einen pH-Drift, der die Aufnahme blockiert. Auseinander hält sie genau eine Frage: sind "
                   + "die oberen oder die unteren Blätter betroffen? Magnesium beginnt unten, Eisen oben. Dazu der pH-Wert.",
            Durchgefallen: "Eine einzelne, sichere Diagnose.",
            Kuerzel: "interveinal-chlorosis",
            Hinweis: "Der wichtigste der vier Tests — hier trennt sich der Berater vom Ratespiel."),
    ];

    /// <summary>Dieselben Fragen als Datei für die Mappe.</summary>
    public static string AlsMarkdown()
    {
        var text = new StringBuilder();
        text.AppendLine("# Prüffragen");
        text.AppendLine();
        text.AppendLine("Bevor du diesem Berater vertraust, stell ihm diese Fragen. Sie haben eine bekannte");
        text.AppendLine("richtige Antwort. Die Musterlösung steht jeweils darunter — lies sie erst nach seiner.");
        text.AppendLine();
        text.AppendLine("Warum das nötig ist: Ein Sprachmodell klingt bei einer erfundenen Antwort genauso");
        text.AppendLine("überzeugt wie bei einer belegten. Der Unterschied ist von außen nicht zu hören, nur zu");
        text.AppendLine("prüfen. Fällt er hier durch, nimm ein anderes Modell — nicht ein anderes Vorgehen.");
        text.AppendLine();

        var nummer = 1;
        foreach (var pruefung in Alle)
        {
            text.AppendLine("---");
            text.AppendLine();
            text.AppendLine($"## {nummer}. {pruefung.Titel}");
            text.AppendLine();
            text.AppendLine($"> {pruefung.Frage}");
            text.AppendLine();
            text.AppendLine($"**Richtig:** {pruefung.Richtig}");
            if (!string.IsNullOrWhiteSpace(pruefung.Kuerzel))
            {
                text.AppendLine();
                text.AppendLine($"Beleg im Wissen: `{pruefung.Kuerzel}`");
            }
            text.AppendLine();
            text.AppendLine($"**Durchgefallen:** {pruefung.Durchgefallen}");
            if (!string.IsNullOrWhiteSpace(pruefung.Hinweis))
            {
                text.AppendLine();
                text.AppendLine(pruefung.Hinweis);
            }
            text.AppendLine();
            nummer++;
        }

        return text.ToString();
    }
}
