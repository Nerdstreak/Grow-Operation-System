using GrowDiary.Web.Models;
using GrowDiary.Web.ViewModels;

namespace GrowDiary.Web.Services;

public sealed class TimelineComposer
{
    public List<TimelineItemViewModel> Build(
        GrowRun grow,
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<PhotoAsset> photos,
        IReadOnlyList<JournalEntry> journalEntries,
        IReadOnlyList<GrowTask> tasks,
        IReadOnlyList<AuditEntry> audits)
    {
        var items = new List<TimelineItemViewModel>();
        var photosByMeasurement = photos.Where(x => x.MeasurementId.HasValue).GroupBy(x => x.MeasurementId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.TakenAtUtc).ToList());

        foreach (var measurement in measurements)
        {
            items.Add(new TimelineItemViewModel
            {
                Timestamp = measurement.TakenAt,
                Kind = "measurement",
                KindLabel = "Messung",
                Title = BuildMeasurementTitle(grow, measurement),
                Body = measurement.Notes,
                SourceLabel = SourceLabel(measurement.Source),
                Badges = BuildMeasurementBadges(grow, measurement),
                PhotoPath = photosByMeasurement.TryGetValue(measurement.Id, out var measurementPhotos) ? measurementPhotos.FirstOrDefault()?.RelativePath : null,
                ActionUrl = $"/grows/measurements/{measurement.Id}/edit",
                ActionLabel = "Bearbeiten"
            });
        }

        foreach (var entry in journalEntries)
        {
            items.Add(new TimelineItemViewModel
            {
                Timestamp = entry.OccurredAtUtc.ToLocalTime(),
                Kind = "note",
                KindLabel = EntryLabel(entry.EntryType),
                Title = entry.Title ?? EntryLabel(entry.EntryType),
                Body = entry.Body,
                SourceLabel = SourceLabel(entry.Source),
                Badges = new List<string> { EntryLabel(entry.EntryType) }
            });
        }

        foreach (var photo in photos.Where(x => !x.MeasurementId.HasValue))
        {
            items.Add(new TimelineItemViewModel
            {
                Timestamp = photo.TakenAtUtc.ToLocalTime(),
                Kind = "photo",
                KindLabel = "Foto",
                Title = photo.Caption ?? "Foto hinzugefügt",
                Body = photo.Tag.ToString(),
                SourceLabel = SourceLabel(photo.Source),
                Badges = new List<string> { photo.Tag.ToString(), photo.IsReferenceShot ? "Referenz" : string.Empty }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                PhotoPath = photo.RelativePath
            });
        }

        foreach (var task in tasks.Where(x => x.Status != GrowTaskStatus.Open))
        {
            items.Add(new TimelineItemViewModel
            {
                Timestamp = (task.CompletedAtUtc ?? task.CreatedAtUtc).ToLocalTime(),
                Kind = "task",
                KindLabel = task.Status == GrowTaskStatus.Done ? "Aufgabe erledigt" : "Aufgabe übersprungen",
                Title = task.Title,
                Body = task.Notes,
                Badges = new List<string> { task.Priority.ToString() }
            });
        }

        foreach (var audit in audits)
        {
            items.Add(new TimelineItemViewModel
            {
                Timestamp = audit.CreatedAtUtc.ToLocalTime(),
                Kind = "audit",
                KindLabel = "Änderung",
                Title = audit.Action,
                Body = audit.Summary
            });
        }

        return items
            .OrderByDescending(x => x.Timestamp)
            .Take(120)
            .ToList();
    }

    public PhotoComparisonViewModel BuildComparison(IReadOnlyList<PhotoAsset> photos)
    {
        var ordered = photos.OrderByDescending(x => x.TakenAtUtc).ToList();
        var latest = ordered.FirstOrDefault();
        var previous = ordered.Skip(1).FirstOrDefault();
        var weekBack = latest is null ? null : ordered.FirstOrDefault(x => x.TakenAtUtc <= latest.TakenAtUtc.AddDays(-6));
        var reference = ordered.FirstOrDefault(x => x.IsReferenceShot);

        return new PhotoComparisonViewModel
        {
            Latest = latest,
            Previous = previous,
            WeekBack = weekBack,
            ReferenceShot = reference
        };
    }

    private static string BuildMeasurementTitle(GrowRun grow, Measurement measurement)
    {
        if (grow.Profile.IsHydro)
        {
            return $"pH {Display(measurement.ReservoirPh, "0.00")} • EC {Display(measurement.ReservoirEc, "0.00")} • Wasser {Display(measurement.ReservoirLevelLiters, "0.0")} L";
        }

        return $"pH {Display(measurement.IrrigationPh, "0.00")} • Wasser {Display(measurement.WaterAmountMl, "0")} ml • Drain {Display(measurement.DrainPh, "0.00")}";
    }

    private static List<string> BuildMeasurementBadges(GrowRun grow, Measurement measurement)
    {
        var badges = new List<string> { measurement.Stage.ToString() };
        if (grow.Profile.IsHydro)
        {
            if (measurement.ReservoirWaterTempC.HasValue) badges.Add($"{measurement.ReservoirWaterTempC.Value:0.0} °C");
            if (measurement.DissolvedOxygenMgL.HasValue) badges.Add($"DO {measurement.DissolvedOxygenMgL.Value:0.0}");
        }
        else
        {
            if (measurement.RunoffAmountMl.HasValue) badges.Add($"Runoff {measurement.RunoffAmountMl.Value:0} ml");
            if (measurement.DrainEc.HasValue) badges.Add($"Drain EC {measurement.DrainEc.Value:0.00}");
        }
        return badges;
    }

    private static string Display(double? value, string format) => value.HasValue ? value.Value.ToString(format) : "–";

    private static string SourceLabel(ValueOrigin source) => source switch
    {
        ValueOrigin.HomeAssistant => "Home Assistant",
        ValueOrigin.Imported => "Importiert",
        ValueOrigin.Derived => "Abgeleitet",
        _ => "Manuell"
    };

    private static string EntryLabel(JournalEntryType type) => type switch
    {
        JournalEntryType.Observation => "Beobachtung",
        JournalEntryType.Action => "Aktion",
        JournalEntryType.Problem => "Problem",
        JournalEntryType.Solution => "Lösung",
        JournalEntryType.Training => "Training",
        JournalEntryType.Transplant => "Umtopfen",
        JournalEntryType.Feeding => "Fütterung",
        JournalEntryType.ReservoirChange => "Reservoirwechsel",
        _ => "Notiz"
    };
}
