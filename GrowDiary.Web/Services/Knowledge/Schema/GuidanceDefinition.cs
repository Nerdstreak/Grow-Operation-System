namespace GrowDiary.Web.Services.Knowledge.Schema;

/// <summary>
/// A rule from the growplan, in words rather than numbers.
///
/// Setpoints say <em>what</em> the target is; a guidance entry says <em>how to behave</em>
/// around it — that pH may drift inside its band and must not be chased, that a daily EC
/// drop is wanted, that the PPFD targets assume CO2. Those sentences used to live only in
/// C# inside the deviation analyser, which meant anything reading the knowledge base saw
/// bare numbers and drew the opposite conclusion.
/// </summary>
public sealed class GuidanceDefinition : KnowledgeFileMetadata
{
    public string Title { get; set; } = string.Empty;

    /// <summary>The rule itself, written so it can be quoted to the user as-is.</summary>
    public string Rule { get; set; } = string.Empty;

    /// <summary>Why the rule exists — what goes wrong when it is ignored.</summary>
    public string? Rationale { get; set; }

    /// <summary>
    /// The advice this rule contradicts. Common growing lore is often the opposite of
    /// RDWC practice, so naming the wrong answer explicitly is part of the knowledge.
    /// </summary>
    public string? CommonMistake { get; set; }

    /// <summary>Measurement keys this rule speaks to, e.g. "ph", "ec", "ppfd".</summary>
    public List<string> Metrics { get; set; } = [];

    /// <summary>Stages the rule applies to; empty means every stage.</summary>
    public List<string> Stages { get; set; } = [];

    /// <summary>Setup types the rule applies to, e.g. "RDWC", "DWC"; empty means all.</summary>
    public List<string> ApplicableSetups { get; set; } = [];

    public List<KnowledgeSource> Sources { get; set; } = [];
}
