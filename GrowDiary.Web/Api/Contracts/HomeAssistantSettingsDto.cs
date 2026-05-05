namespace GrowDiary.Web.Api.Contracts;

public sealed record HomeAssistantSettingsDto(
    string? BaseUrl,
    string? AccessToken,
    bool Enabled
);
