using System.Text;

namespace GrowDiary.Web.Services.Ai;

/// <summary>
/// Turns a context into the two messages that go to the model.
///
/// Kept separate from the HTTP call so the exact text can be asserted in tests and shown
/// to the user — the "was wird gesendet" preview renders this, not a paraphrase of it.
/// </summary>
public static class AiPrompt
{
    /// <summary>
    /// The behaviour rules. Two of them carry the whole design: the material outranks the
    /// model's own knowledge, and every claim names its source so the citation can be
    /// checked afterwards.
    /// </summary>
    public static string SystemMessage =>
        """
        Du bist der Grow-Assistent von Grow OS und berätst zu RDWC- und DWC-Anbau.

        Regeln:
        1. Antworte ausschließlich auf Basis der Unterlagen im Abschnitt UNTERLAGEN.
        2. Wo die Unterlagen deinem Allgemeinwissen widersprechen, gelten die Unterlagen.
           Das gilt besonders für pH, EC und Licht — verbreitete Ratschläge sind hier oft
           das Gegenteil des Richtigen.
        3. Jede Aussage nennt die id der Unterlage, auf die sie sich stützt.
        4. Findest du in den Unterlagen nichts zur Frage, sag das offen. Rate nicht.
        5. Du schlägst vor, du handelst nicht. Keine Aussage im Befehlston.

        Antworte als JSON, ohne Text davor oder danach:
        {
          "antwort": "<zwei bis vier Sätze Zusammenfassung>",
          "aussagen": [
            { "text": "<eine konkrete Aussage oder Empfehlung>", "quelle": "<id aus UNTERLAGEN>" }
          ],
          "offen": "<was du mangels Unterlagen nicht beantworten kannst, sonst leer>"
        }
        """;

    public static string UserMessage(AiContext context, string question)
    {
        var builder = new StringBuilder();

        builder.AppendLine("## GROW");
        foreach (var fact in context.GrowFacts)
        {
            builder.AppendLine($"- {fact}");
        }

        if (context.Measurements.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## MESSUNGEN (älteste zuerst)");
            foreach (var measurement in context.Measurements)
            {
                builder.AppendLine($"- {measurement}");
            }
        }

        if (context.OpenDeviations.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## OFFENE ABWEICHUNGEN (von Grow OS erkannt)");
            foreach (var deviation in context.OpenDeviations)
            {
                builder.AppendLine($"- {deviation}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## UNTERLAGEN");
        foreach (var item in context.Knowledge)
        {
            builder.AppendLine($"[{item.Id}] ({item.Kind}) {item.Title}");
            builder.AppendLine($"  {item.Body}");
            if (!string.IsNullOrWhiteSpace(item.SourceTitle))
            {
                var reference = string.IsNullOrWhiteSpace(item.SourceReference) ? string.Empty : $", {item.SourceReference}";
                builder.AppendLine($"  Quelle: {item.SourceTitle}{reference}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## FRAGE");
        builder.AppendLine(question.Trim());

        return builder.ToString();
    }
}
