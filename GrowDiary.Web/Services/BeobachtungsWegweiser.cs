using GrowDiary.Web.Services.Knowledge;

namespace GrowDiary.Web.Services;

/// <summary>Ein Vorschlag, der auf eine Beobachtung folgt.</summary>
public sealed record WegweiserVorschlag(string Id, string Name, string Art);

/// <summary>Eine Beobachtung samt dem, was daraus folgt.</summary>
public sealed record Beobachtung(
    string Id,
    string Name,
    IReadOnlyList<string> MoeglicheUrsachen,
    IReadOnlyList<string> SelbstPruefen,
    IReadOnlyList<WegweiserVorschlag> Vorschlaege);

/// <summary>Wo man hinschaut: Blatt, Wurzel oder Lösung.</summary>
public sealed record Beobachtungsgruppe(string Bereich, string Frage, IReadOnlyList<Beobachtung> Beobachtungen);

/// <summary>
/// Der Weg von „mir gefällt was nicht" zum vorhandenen Wissen.
/// </summary>
/// <remarks>
/// <para><b>Warum es das braucht:</b> Die Diagnose stellt heute allein auf
/// Zahlen ab — pH-Geschwindigkeit, EC-Verhalten, Sauerstoff, ORP. Wer ein
/// gelbes Blatt sieht, findet dort nichts. Dabei liegen zwanzig Symptome und
/// dreissig Behandlungen im Wissen; man muss nur wissen, wonach man sucht, um
/// sie zu finden. Genau diese Voraussetzung nimmt der Wegweiser weg.</para>
///
/// <para><b>Keine KI.</b> Drei Fragen und eine Liste — die Zuordnung von
/// Beobachtung zu Ursache und Behandlung steht seit jeher in den
/// Symptom-Dateien. Sie wurde nur nie angeboten.</para>
///
/// <para><b>Was NICHT erscheint:</b> Einträge ohne mögliche Ursachen. Die
/// Wissensbasis führt unter denselben Kategorien auch Routinen („Präventive
/// Routine-Massnahme", „Steckling bereit fürs Hauptsystem"). Das sind keine
/// Befunde, und wer nach einem Problem sucht, soll sie nicht durchblättern.
/// Das Fehlen von Ursachen ist dafür der ehrliche Unterschied — keine Liste
/// von Ausnahmen, die beim nächsten Symptom veraltet.</para>
/// </remarks>
public sealed class BeobachtungsWegweiser
{
    private static readonly (string Kategorie, string Bereich, string Frage)[] Bereiche =
    [
        ("Leaf", "Blatt", "Was siehst du am Blatt?"),
        ("Root", "Wurzel", "Wie sehen die Wurzeln aus?"),
        ("Solution", "Lösung", "Was fällt an der Nährlösung auf?"),
    ];

    private readonly KnowledgeBaseLoader _wissen;

    public BeobachtungsWegweiser(KnowledgeBaseLoader wissen)
    {
        _wissen = wissen;
    }

    public IReadOnlyList<Beobachtungsgruppe> Gruppen()
    {
        var behandlungen = _wissen.Treatments.ToDictionary(t => t.Id, t => t.Name, StringComparer.OrdinalIgnoreCase);
        var ablaeufe = _wissen.Sops.ToDictionary(s => s.Id, s => s.Name, StringComparer.OrdinalIgnoreCase);

        return Bereiche
            .Select(bereich => new Beobachtungsgruppe(
                bereich.Bereich,
                bereich.Frage,
                _wissen.Symptoms
                    .Where(symptom => string.Equals(symptom.Category, bereich.Kategorie, StringComparison.OrdinalIgnoreCase))
                    // Ohne Ursachen ist es keine Beobachtung, sondern eine Routine.
                    .Where(symptom => symptom.PossibleCauses.Count > 0)
                    .OrderBy(symptom => symptom.Name, StringComparer.CurrentCulture)
                    .Select(symptom => new Beobachtung(
                        symptom.Id,
                        symptom.Name,
                        symptom.PossibleCauses,
                        symptom.DiagnosticChecks,
                        Vorschlaege(symptom, behandlungen, ablaeufe)))
                    .ToList()))
            .Where(gruppe => gruppe.Beobachtungen.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Behandlungen und Abläufe mit ihrem Namen statt ihrer Kennung.
    /// </summary>
    /// <remarks>
    /// Was in der Wissensbasis nicht (mehr) existiert, wird weggelassen statt
    /// als nackte Kennung angezeigt: „hocl-orp-boost-emergency" auf dem
    /// Bildschirm hilft niemandem weiter.
    /// </remarks>
    private static List<WegweiserVorschlag> Vorschlaege(
        Knowledge.Schema.SymptomDefinition symptom,
        IReadOnlyDictionary<string, string> behandlungen,
        IReadOnlyDictionary<string, string> ablaeufe)
    {
        var vorschlaege = new List<WegweiserVorschlag>();

        foreach (var id in symptom.SuggestedTreatmentIds)
        {
            if (behandlungen.TryGetValue(id, out var name))
            {
                vorschlaege.Add(new WegweiserVorschlag(id, name, "Behandlung"));
            }
        }

        foreach (var id in symptom.SuggestedSopIds)
        {
            if (ablaeufe.TryGetValue(id, out var name))
            {
                vorschlaege.Add(new WegweiserVorschlag(id, name, "Ablauf"));
            }
        }

        return vorschlaege;
    }
}
