namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// The AI connection as the UI sees it. The key is never sent back — only whether one is
/// stored — so it cannot leak through the API it was saved with.
/// </summary>
public sealed record AiSettingsDto(
    string Provider,
    string? BaseUrl,
    string? Model,
    bool Enabled,
    bool AllowPhotos,
    bool HasApiKey,
    bool IsLocalEndpoint,
    bool IsConfigured);

/// <param name="ApiKey">Null leaves the stored key untouched; an empty string clears it.</param>
public sealed record AiSettingsRequest(
    string? Provider,
    string? BaseUrl,
    string? Model,
    bool Enabled,
    bool AllowPhotos,
    string? ApiKey);

public sealed record AiKnowledgeItemDto(
    string Id,
    string Kind,
    string Title,
    string Body,
    string? SourceTitle,
    string? SourceReference,
    string? SourceUrl);

/// <summary>
/// Exactly what would leave the house for this grow — the same object the request is built
/// from, so the preview cannot drift away from reality.
/// </summary>
public sealed record AiSendPreviewDto(
    int GrowId,
    bool WouldLeaveTheHouse,
    string? Endpoint,
    IReadOnlyList<string> GrowFacts,
    IReadOnlyList<string> Measurements,
    IReadOnlyList<string> OpenDeviations,
    IReadOnlyList<AiKnowledgeItemDto> Knowledge,
    string SystemMessage,
    string UserMessage);

public sealed record AiClaimDto(
    string Text,
    string? SourceId,
    bool Grounded,
    string? SourceTitle,
    string? SourceUrl);

public sealed record AiAnswerDto(
    string Summary,
    IReadOnlyList<AiClaimDto> Claims,
    string? Unanswered,
    int UngroundedCount,
    bool IsUngrounded);

public sealed record AiAskRequest(int GrowId, string Question);

public sealed record AiConnectionTestDto(bool Ok, string? ErrorCode, string? Message, string? Reply);