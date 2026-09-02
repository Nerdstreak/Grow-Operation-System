namespace GrowDiary.Web.Models;

public sealed class RecommendationCard
{
    /// <summary>Einer von vier Werten — siehe <see cref="Kartenschwere"/>.</summary>
    public string Severity { get; set; } = Kartenschwere.Hinweis;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
