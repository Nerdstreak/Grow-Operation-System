using GrowDiary.Web.Models;

namespace GrowDiary.Web.ViewModels;

public sealed class JournalEntryFormViewModel
{
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public JournalEntryType EntryType { get; set; } = JournalEntryType.Note;
    public ValueOrigin Source { get; set; } = ValueOrigin.Manual;
    public string OccurredAtLocal { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");

    public JournalEntry ToEntry(int growId)
    {
        return new JournalEntry
        {
            GrowId = growId,
            Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
            Body = string.IsNullOrWhiteSpace(Body) ? null : Body.Trim(),
            EntryType = EntryType,
            Source = Source,
            OccurredAtUtc = DateTime.SpecifyKind(DateTime.Parse(OccurredAtLocal), DateTimeKind.Local).ToUniversalTime()
        };
    }
}
