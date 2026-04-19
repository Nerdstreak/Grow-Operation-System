namespace GrowDiary.Web.Models;

public sealed class HomeAssistantSettings
{
    public string? BaseUrl { get; set; }
    public string? AccessToken { get; set; }
    public bool Enabled { get; set; }

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(AccessToken);
}
