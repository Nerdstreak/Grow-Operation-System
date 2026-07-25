using GrowDiary.Web.Services.Knowledge.Schema;

namespace GrowDiary.Web.Services;

/// <summary>One step as it will actually be worked through, once the choices are known.</summary>
/// <param name="Step">The definition it came from.</param>
/// <param name="Occurrence">1-based counter when the step repeats, otherwise 1.</param>
/// <param name="OccurrenceCount">How many times it repeats in total.</param>
/// <param name="Subject">What this occurrence is for, e.g. "Pflanze 2 von 6".</param>
public sealed record PlannedSopStep(
    SopStepDefinition Step,
    int Occurrence,
    int OccurrenceCount,
    string? Subject)
{
    public bool IsRepeated => OccurrenceCount > 1;
}

/// <summary>A question the SOP has to ask before it can be planned.</summary>
public sealed record SopChoice(string Key, string? Prompt, IReadOnlyList<string> Options);

/// <summary>
/// Turns an SOP definition into the concrete list of steps for one run.
///
/// The source procedures are not flat. SOP-S1 sends a badly affected plant down a different
/// path than a healthy one; SOP-C1 handles rockwool differently from a Jiffy; both repeat a
/// block once per plant, with disinfection in between — which is the part that actually
/// stops the pathogen spreading. Flattening that into one list turns a procedure back into
/// prose, and prose is what people skip.
/// </summary>
public static class SopStepPlanner
{
    /// <summary>
    /// Every choice the SOP branches on, collected from its steps so the UI can ask before
    /// starting rather than mid-procedure.
    /// </summary>
    public static IReadOnlyList<SopChoice> RequiredChoices(SopDefinition sop)
    {
        return sop.Steps
            .Where(step => step.Condition is not null)
            .GroupBy(step => step.Condition!.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SopChoice(
                group.Key,
                group.Select(step => step.Condition!.Prompt).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                group.SelectMany(step => step.Condition!.EqualsAny)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                     .ToList()))
            .ToList();
    }

    /// <summary>
    /// The steps for one run.
    /// </summary>
    /// <param name="answers">Answers to <see cref="RequiredChoices"/>, by key.</param>
    /// <param name="repeatCounts">
    /// How often a repeated step runs, by subject key — e.g. <c>{"plant": 6}</c>. A missing
    /// or non-positive count means the block runs once, so an unanswered question never
    /// silently drops a step.
    /// </param>
    public static IReadOnlyList<PlannedSopStep> Plan(
        SopDefinition sop,
        IReadOnlyDictionary<string, string>? answers = null,
        IReadOnlyDictionary<string, int>? repeatCounts = null)
    {
        var planned = new List<PlannedSopStep>();

        foreach (var step in sop.Steps.OrderBy(step => step.Order))
        {
            if (!Applies(step, answers))
            {
                continue;
            }

            var count = RepeatCount(step, repeatCounts);
            for (var occurrence = 1; occurrence <= count; occurrence++)
            {
                planned.Add(new PlannedSopStep(
                    step,
                    occurrence,
                    count,
                    count > 1 ? $"{SubjectLabel(step.RepeatFor!)} {occurrence} von {count}" : null));
            }
        }

        return planned;
    }

    private static bool Applies(SopStepDefinition step, IReadOnlyDictionary<string, string>? answers)
    {
        if (step.Condition is not { } condition || condition.EqualsAny.Count == 0)
        {
            return true;
        }

        // Unanswered means "keep it". Dropping a step because a question was skipped would
        // silently shorten a treatment procedure, which is the worst way to be wrong here.
        if (answers is null || !answers.TryGetValue(condition.Key, out var answer))
        {
            return true;
        }

        return condition.EqualsAny.Contains(answer, StringComparer.OrdinalIgnoreCase);
    }

    private static int RepeatCount(SopStepDefinition step, IReadOnlyDictionary<string, int>? repeatCounts)
    {
        if (string.IsNullOrWhiteSpace(step.RepeatFor)
            || repeatCounts is null
            || !repeatCounts.TryGetValue(step.RepeatFor, out var count)
            || count <= 0)
        {
            return 1;
        }

        return count;
    }

    private static string SubjectLabel(string repeatFor) => repeatFor.ToLowerInvariant() switch
    {
        "plant" => "Pflanze",
        "bucket" => "Eimer",
        "module" => "Modul",
        "cutting" => "Steckling",
        _ => repeatFor,
    };
}
