namespace GrowDiary.Web.Services.Ai;

/// <summary>One knowledge entry that was put in front of the model, and may be cited back.</summary>
/// <param name="Id">The citation key. A claim referencing anything else is invented.</param>
/// <param name="Kind">"Regel", "Sollwerte", "SOP", "Behandlung" …</param>
/// <param name="Title">Human-readable name, shown next to the answer.</param>
/// <param name="Body">The text handed to the model.</param>
/// <param name="SourceTitle">The document behind it, e.g. the growplan.</param>
/// <param name="SourceReference">Where in that document, e.g. "Punkt 6: pH-Management".</param>
/// <param name="SourceUrl">Link, when we actually ship the document.</param>
public sealed record AiKnowledgeItem(
    string Id,
    string Kind,
    string Title,
    string Body,
    string? SourceTitle = null,
    string? SourceReference = null,
    string? SourceUrl = null);

/// <summary>
/// Everything that would leave the house for one question, assembled and inspectable.
///
/// This type is the transparency guarantee: the preview endpoint returns exactly this, so
/// what the user is shown and what is sent cannot drift apart — they are the same object.
/// </summary>
public sealed class AiContext
{
    /// <summary>Plain sentences about the grow: name, strain, stage, day.</summary>
    public List<string> GrowFacts { get; init; } = [];

    /// <summary>Recent readings, oldest first, already formatted for reading.</summary>
    public List<string> Measurements { get; init; } = [];

    /// <summary>What Grow OS itself currently flags as off.</summary>
    public List<string> OpenDeviations { get; init; } = [];

    /// <summary>The knowledge the answer must be built from.</summary>
    public List<AiKnowledgeItem> Knowledge { get; init; } = [];

    /// <summary>Every citable id — what a returned citation is checked against.</summary>
    public IReadOnlySet<string> CitableIds =>
        Knowledge.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
