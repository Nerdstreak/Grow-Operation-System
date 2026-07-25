namespace GrowDiary.Web.Api.Contracts;

/// <summary>One thing the search found, and where to go for it.</summary>
public sealed record SearchHitDto(string Kind, string Title, string? Subtitle, string Route);
