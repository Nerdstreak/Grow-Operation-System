namespace GrowDiary.Web.Models;

/// <summary>
/// Which dialect the endpoint speaks. Anthropic is not OpenAI-compatible — different path,
/// different auth header, the system prompt is its own field and the reply is shaped
/// differently — so a Claude key needs its own request rather than a compatibility layer
/// we would have to trust.
/// </summary>
public enum AiProvider
{
    /// <summary>OpenAI itself, OpenRouter, Ollama, LM Studio, vLLM …</summary>
    OpenAiCompatible,
    /// <summary>Anthropic's own API.</summary>
    Anthropic
}

/// <summary>
/// How Grow OS reaches a language model. Deliberately just an OpenAI-compatible endpoint:
/// that one shape covers the hosted providers as well as a local Ollama or LM Studio, so
/// nobody is forced to send their grow out of the house to use the feature.
/// </summary>
public sealed class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.OpenAiCompatible;

    /// <summary>Base URL up to and including <c>/v1</c>, e.g. <c>http://localhost:11434/v1</c>.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Bearer token. Local endpoints usually need none.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The model to ask, e.g. <c>gpt-4o-mini</c> or <c>llama3.1</c>.</summary>
    public string? Model { get; set; }

    /// <summary>Off by default — the whole feature stays invisible until it is set up.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether photos may be sent. Separate from <see cref="Enabled"/> on purpose: a picture
    /// of someone's grow room is a different kind of disclosure than a pH number, and it
    /// should be an explicit decision rather than a side effect of turning the feature on.
    /// </summary>
    public bool AllowPhotos { get; set; }

    /// <summary>A local endpoint keeps everything in the house; say so rather than assume it.</summary>
    public bool IsLocalEndpoint =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Anthropic needs no address — theirs is the only one — so a model and a key are
    /// enough. Everything else has to be told where to go.
    /// </summary>
    public bool IsConfigured => Provider switch
    {
        AiProvider.Anthropic => !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(ApiKey),
        _ => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model),
    };

    /// <summary>Usable only when configured <em>and</em> switched on.</summary>
    public bool IsUsable => Enabled && IsConfigured;
}
