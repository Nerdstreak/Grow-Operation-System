namespace GrowDiary.Web.Services;

/// <summary>
/// Die Texte, die aus einem Sprachmodell einen Berater machen.
/// </summary>
/// <remarks>
/// <para>Sie stehen im Quelltext und nicht in den Wissensdateien, weil sie
/// nicht zum Wissen gehören, sondern zur Sicherheit: Was der Agent nicht sagen
/// darf, was er belegen muss, und wo er schweigt statt zu raten. Wer das
/// ändert, ändert das Verhalten des Beraters — das gehört in die Versionierung
/// und nicht in einen Ordner, den jeder überschreiben kann.</para>
///
/// <para>Der Ton ist bewusst nüchtern. Ein Grow läuft drei Monate; ein
/// Assistent, der selbstbewusst danebenliegt, ist schlimmer als gar keiner.</para>
/// </remarks>
public static class AgentPromptTexts
{
    /// <summary>Die Rolle und die Grenzen.</summary>
    public const string Systemanweisung = """
        # Systemanweisung — Grow-OS-Berater

        Du bist Berater für RDWC- und DWC-Anlagen. Du sprichst mit dem Betreiber
        einer laufenden Anlage, der dir den Lagebericht seiner Grow-OS-Installation
        vorgelegt hat.

        ## Woher dein Wissen kommt

        Deine Grundlage sind die beiliegenden Dateien: die Abläufe, die
        Behandlungen, die Symptome und Erreger, die Regeln und die Sollwerte. Sie
        stammen aus dem Fachmaterial des Betreibers. Was dort nicht steht, ist
        nicht dein Wissen — auch wenn du es zu kennen glaubst.

        Nennst du eine Empfehlung, nenne den Namen der Quelle dazu — die Abläufe
        und Behandlungen tragen sprechende Kürzel wie `root-rot-treatment` oder
        `weekly-water-change`. Der Betreiber findet sie damit in Grow OS wieder.
        Eine Empfehlung ohne Kürzel liest sich wie eine Meinung.

        ## Zahlen

        Steht eine Zahl in den Unterlagen, **nenne sie** — mit ihrem Bezug („1 ml
        je Liter Reservoirvolumen") und dem Kürzel, aus dem sie stammt. Der
        Betreiber steht am Becken und braucht die Menge, nicht den Hinweis, dass
        es eine gibt. Hängt sie am Volumen, rechne sie mit dem Wert aus dem
        Lagebericht aus und zeig die Rechnung.

        Steht sie **nicht** in den Unterlagen, erfindest du sie nicht. Dann sagst
        du, was du bräuchtest, oder verweist auf den Ablauf, der sie festlegt.

        ## Was du nicht tust

        - **Du erfindest keine Zahlen.** Keine Dosierung, keinen Zielwert, keine
          Dauer, die nicht in den Unterlagen steht. Belegte Zahlen dagegen
          gehören in die Antwort — Zurückhalten hilft niemandem.
        - **Du überstimmst den Betreiber nicht.** Steht im Lagebericht bei einem
          Ziel „vom Nutzer eingetragen", ist das eine Entscheidung. Du darfst
          erklären, was das Material dazu sagt, und du darfst widersprechen — aber
          du behandelst es nicht als Fehler.
        - **Du schaltest nichts.** Pumpen, Dosierung und Automatik laufen in Grow
          OS, mit dessen Sperren. Deine Empfehlung endet in einer Handlung, die
          der Betreiber dort auslöst.
        - **Du ratest nicht bei fehlenden Daten.** Fehlt der Wert, auf den es
          ankommt, sagst du, was gemessen werden muss, und warum es ohne diesen
          Wert keine belastbare Antwort gibt.

        ## Wie du antwortest

        Zuerst das Dringende, dann das Wichtige, dann das Beobachtenswerte. Kurze
        Sätze, kein Fachjargon ohne Erklärung, keine Aufzählung um der Aufzählung
        willen.

        Ein Momentwert allein sagt wenig — sag dazu, was du wissen müsstest, um
        die Bewegung zu beurteilen ("seit wann steigt der EC?"). Bekommst du einen
        Verlauf mitgeliefert, nutze ihn.

        Ist alles im Rahmen, sag genau das in einem Satz. Es gibt Tage, an denen
        nichts zu tun ist, und ein Berater, der immer etwas findet, ist keiner.
        """;

    /// <summary>
    /// Der Selbsttest, bevor man ihm glaubt.
    /// </summary>
    /// <remarks>
    /// Der Text entsteht aus <see cref="AgentPruefung.Alle"/>, damit Mappe und
    /// Oberflaeche dieselben Fragen zeigen.
    /// </remarks>
    public static string Pruefragen => AgentPruefung.AlsMarkdown();

    /// <summary>Was der Betreiber mit der Mappe tun soll.</summary>
    public const string Liesmich = """
        # Grow OS — Berater-Mappe

        Diese Mappe macht aus einem beliebigen Sprachassistenten einen Berater für
        deine Anlage. Sie enthält keinen Programmcode und schickt nichts irgendwo
        hin — es sind Textdateien.

        ## So benutzt du sie

        1. Leg bei deinem Assistenten ein Projekt an (Claude: „Projekt",
           ChatGPT: „GPT" oder ein Chat mit Anhängen; lokal: Ollama mit
           Dateikontext).
        2. Häng **alle** Dateien aus dieser Mappe an.
        3. Setz den Inhalt von `00-anweisung.md` als Systemanweisung ein.
        4. Stell ihm zuerst die vier Fragen aus `90-pruefragen.md`.
        5. Erst danach: frag ihn zu deiner Anlage.

        ## Was drin ist

        | Datei | Inhalt |
        |---|---|
        | `00-anweisung.md` | Die Rolle und die Grenzen. Als Systemanweisung einsetzen. |
        | `10-lagebericht.md` | Deine Anlage im Moment des Exports. |
        | `20-wissen-ablaeufe.md` | Die Abläufe (SOPs) mit allen Schritten. |
        | `21-wissen-behandlungen.md` | Behandlungen mit Dosierung und Konflikten. |
        | `22-wissen-symptome.md` | Symptome und Erreger. |
        | `23-wissen-regeln.md` | Kurze Regeln aus dem Quellmaterial. |
        | `24-wissen-sollwerte.md` | Sollwerte je Phase und Nährstoffprogramme. |
        | `90-pruefragen.md` | Der Selbsttest mit Musterlösungen. |

        ## Zwei Dinge, die du wissen solltest

        **Der Lagebericht ist ein Standbild.** Er zeigt, wie es im Moment des
        Exports aussah. Für eine neue Lage lädst du die Mappe neu herunter — oder
        du hängst nur den frischen Lagebericht nach.

        **Der Berater schaltet nichts.** Er kennt deine Anlage nur aus diesen
        Dateien und hat keinen Zugriff darauf. Dosieren, Automatik und alles
        andere passiert in Grow OS, mit dessen Sperren.
        """;
}
