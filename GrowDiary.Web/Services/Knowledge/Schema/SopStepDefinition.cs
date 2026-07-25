using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class SopStepDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("stepType")]
    public string StepType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("waitMinutes")]
    public int? WaitMinutes { get; set; }

    [JsonPropertyName("subSopId")]
    public string? SubSopId { get; set; }

    [JsonPropertyName("expectedInputs")]
    public List<string>? ExpectedInputs { get; set; }

    [JsonPropertyName("photoRequired")]
    public bool PhotoRequired { get; set; } = false;

    [JsonPropertyName("photoRecommended")]
    public bool PhotoRecommended { get; set; } = false;

    /// <summary>
    /// When this step applies. Null means always.
    ///
    /// The source SOPs branch: SOP-S1 treats a lightly affected plant differently from a
    /// badly affected one, SOP-C1 handles rockwool differently from a Jiffy. Written as a
    /// flat list, those branches collapse into prose that the user has to sort out in their
    /// head — which is exactly what a procedure is supposed to prevent.
    /// </summary>
    [JsonPropertyName("condition")]
    public SopStepCondition? Condition { get; set; }

    /// <summary>
    /// Further conditions that must <em>all</em> hold alongside <see cref="Condition"/>.
    ///
    /// One key is not always enough: decontaminating the substrate carrier depends both on
    /// which agent was chosen and on there being a carrier at all, so a bare-root cutting
    /// must not be told to dip a plug it doesn't have.
    /// </summary>
    [JsonPropertyName("conditions")]
    public List<SopStepCondition>? Conditions { get; set; }

    /// <summary>Every condition on this step, however it was written.</summary>
    public IEnumerable<SopStepCondition> AllConditions()
    {
        if (Condition is not null)
        {
            yield return Condition;
        }

        foreach (var extra in Conditions ?? [])
        {
            yield return extra;
        }
    }

    /// <summary>
    /// What this step is repeated for, e.g. "plant". Null means it runs once.
    ///
    /// "For every plant: lift out, rinse, then disinfect the shears and the surface" is the
    /// heart of SOP-S1 and SOP-C1 — and the disinfection between plants is what stops the
    /// pathogen travelling. As a single line it reads like advice; as a repeated step it
    /// gets ticked off per plant.
    /// </summary>
    [JsonPropertyName("repeatFor")]
    public string? RepeatFor { get; set; }
}

/// <summary>
/// A named choice the user makes when starting the SOP, and the value this step needs.
/// Kept deliberately simple: one question, one answer, string comparison. The SOPs branch
/// on categories ("severity", "substrate"), not on arithmetic.
/// </summary>
public sealed class SopStepCondition
{
    /// <summary>The question key, e.g. "severity" or "substrate".</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>The values this step applies to, e.g. ["severe"].</summary>
    [JsonPropertyName("equals")]
    public List<string> EqualsAny { get; set; } = [];

    /// <summary>Shown when asking, so the choice is understandable without the document.</summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }
}
