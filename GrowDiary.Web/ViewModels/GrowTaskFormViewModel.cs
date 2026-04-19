using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class GrowTaskFormViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? DueAtLocal { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    public GrowTask ToTask(int growId)
    {
        return new GrowTask
        {
            GrowId = growId,
            Title = string.IsNullOrWhiteSpace(Title) ? string.Empty : Title.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            DueAtUtc = string.IsNullOrWhiteSpace(DueAtLocal) ? null : DateTime.SpecifyKind(DateTime.Parse(DueAtLocal), DateTimeKind.Local).ToUniversalTime(),
            Priority = Priority
        };
    }
}
