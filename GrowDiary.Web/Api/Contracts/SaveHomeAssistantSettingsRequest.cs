namespace GrowDiary.Web.Api.Contracts;

public sealed class SaveHomeAssistantSettingsRequest
{
    public string? BaseUrl { get; set; }
    public string? AccessToken { get; set; }
    public bool Enabled { get; set; }
}
