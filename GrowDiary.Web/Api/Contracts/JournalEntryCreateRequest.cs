using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class JournalEntryCreateRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public JournalEntryType EntryType { get; set; } = JournalEntryType.Note;
    public ValueOrigin Source { get; set; } = ValueOrigin.Manual;
    public string OccurredAtLocal { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");
}
