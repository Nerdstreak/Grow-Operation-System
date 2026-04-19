using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.ViewModels;

public sealed class GrowDetailsViewModel
{
    public GrowRun Grow { get; set; } = new();
    public Tent? Tent { get; set; }
    public MeasurementFormViewModel MeasurementForm { get; set; } = new();
    public JournalEntryFormViewModel JournalForm { get; set; } = new();
    public GrowTaskFormViewModel TaskForm { get; set; } = new();
    public List<Measurement> Measurements { get; set; } = new();
    public List<PhotoAsset> Photos { get; set; } = new();
    public IReadOnlyList<RecommendationCard> Recommendations { get; set; } = Array.Empty<RecommendationCard>();
    public NutrientProgram? NutrientProgram { get; set; }
    public MediumPlaybook? MediumPlaybook { get; set; }
    public DateTime? LastSolutionChangeAt { get; set; }
    public ChartSeries? MainChart { get; set; }
    public ChartSeries? SecondaryChart { get; set; }
    public ChartSeries? WateringChart { get; set; }
    public List<GrowTask> OpenTasks { get; set; } = new();
    public List<JournalEntry> JournalEntries { get; set; } = new();
    public List<AuditEntry> AuditEntries { get; set; } = new();
    public List<TimelineItemViewModel> Timeline { get; set; } = new();
    public PhotoComparisonViewModel PhotoComparison { get; set; } = new();
    public List<PhotoTag> AvailablePhotoTags { get; set; } = Enum.GetValues<PhotoTag>().ToList();
    public GrowWeekInfo? WeekInfo { get; set; }
    public HarvestEntry? Harvest { get; set; }
}
