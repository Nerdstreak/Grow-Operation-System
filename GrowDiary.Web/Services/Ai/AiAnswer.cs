namespace GrowDiary.Web.Services.Ai;

/// <summary>One claim from the model, together with what backs it — or doesn't.</summary>
/// <param name="Text">The claim as the user reads it.</param>
/// <param name="SourceId">The knowledge id the model named.</param>
/// <param name="Grounded">
/// True when <paramref name="SourceId"/> was really among the material we sent. False means
/// the model produced a citation out of thin air, and the claim is presented as its own
/// opinion rather than as something from the user's documents.
/// </param>
/// <param name="SourceTitle">Readable name of the cited entry, when grounded.</param>
/// <param name="SourceUrl">Link to the document, when we ship it.</param>
public sealed record AiClaim(
    string Text,
    string? SourceId,
    bool Grounded,
    string? SourceTitle = null,
    string? SourceUrl = null);

public sealed class AiAnswer
{
    public string Summary { get; init; } = string.Empty;

    public List<AiClaim> Claims { get; init; } = [];

    /// <summary>What the model said it could not answer from the material.</summary>
    public string? Unanswered { get; init; }

    /// <summary>Claims that cited something we never sent. Zero is the expected case.</summary>
    public int UngroundedCount => Claims.Count(claim => !claim.Grounded);

    /// <summary>
    /// True when the model ignored the material wholesale. Worth surfacing plainly: it
    /// usually means the configured model is too weak to follow a long context, and the
    /// honest response is to say so rather than dress up the answer.
    /// </summary>
    public bool IsUngrounded => Claims.Count > 0 && Claims.All(claim => !claim.Grounded);
}
