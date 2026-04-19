namespace GrowDiary.Web.Models;

public sealed class RecommendationCard
{
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
