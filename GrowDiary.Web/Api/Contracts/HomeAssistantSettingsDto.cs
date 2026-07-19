namespace GrowDiary.Web.Api.Contracts;

public sealed record HomeAssistantSettingsDto(
    string? BaseUrl,
    string? AccessToken,
    bool Enabled,
    // True when Grow OS runs as a Home Assistant add-on: the connection is managed
    // automatically, so the UI can hide the manual URL/token fields.
    bool IsManagedByAddon = false
);
