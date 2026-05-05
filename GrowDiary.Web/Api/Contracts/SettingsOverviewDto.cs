namespace GrowDiary.Web.Api.Contracts;

public sealed record SettingsOverviewDto(
    HomeAssistantSettingsDto HomeAssistant,
    IReadOnlyList<TentDto> Tents
);
