namespace GrowDiary.Web.Models;

/// <summary>
/// How Grow OS reaches a language model. Deliberately just an OpenAI-compatible endpoint:
/// that one shape covers the hosted providers as well as a local Ollama or LM Studio, so
/// nobody is forced to send their grow out of the house to use the feature.
/// </summary>
public sealed class AiSettings
{
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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);

    /// <summary>Usable only when configured <em>and</em> switched on.</summary>
    public bool IsUsable => Enabled && IsConfigured;
}
