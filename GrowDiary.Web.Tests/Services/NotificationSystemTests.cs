using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class NotificationSettingsTests
{
    [Theory]
    [InlineData(22, 7, 23, true)]   // inside a window that wraps past midnight
    [InlineData(22, 7, 3, true)]
    [InlineData(22, 7, 7, false)]   // end is exclusive
    [InlineData(22, 7, 8, false)]
    [InlineData(22, 7, 22, true)]   // start is inclusive
    [InlineData(1, 6, 3, true)]     // normal (non-wrapping) window
    [InlineData(1, 6, 0, false)]
    [InlineData(1, 6, 6, false)]
    public void IsQuietHour_RespectsWindow(int start, int end, int hour, bool expected)
    {
        var settings = new NotificationSettings { QuietHoursStartHour = start, QuietHoursEndHour = end };

        Assert.Equal(expected, settings.IsQuietHour(hour));
    }

    [Fact]
    public void IsQuietHour_OffWhenUnset()
    {
        Assert.False(new NotificationSettings().IsQuietHour(3));
        Assert.False(new NotificationSettings { QuietHoursStartHour = 5, QuietHoursEndHour = 5 }.IsQuietHour(5));
    }

    [Fact]
    public void IsCategoryEnabled_ReflectsToggles()
    {
        var settings = new NotificationSettings { Calibration = false, Thresholds = true };

        Assert.False(settings.IsCategoryEnabled(NotificationCategory.Calibration));
        Assert.True(settings.IsCategoryEnabled(NotificationCategory.Threshold));
    }
}

public sealed class SensorOfflineTrackerTests
{
    [Fact]
    public void SingleOfflinePoll_DoesNotAlert()
    {
        var tracker = new SensorOfflineTracker();

        Assert.Equal(SensorOfflineTracker.Transition.None, tracker.Observe("1:reservoir-ph", offline: true));
    }

    [Fact]
    public void TwoConsecutiveOfflinePolls_Alert_ThenStayQuiet()
    {
        var tracker = new SensorOfflineTracker();

        Assert.Equal(SensorOfflineTracker.Transition.None, tracker.Observe("k", true));
        Assert.Equal(SensorOfflineTracker.Transition.WentOffline, tracker.Observe("k", true));
        Assert.Equal(SensorOfflineTracker.Transition.None, tracker.Observe("k", true));
    }

    [Fact]
    public void Recovery_AfterAlert_ReportsCameOnline()
    {
        var tracker = new SensorOfflineTracker();
        tracker.Observe("k", true);
        tracker.Observe("k", true); // WentOffline

        Assert.Equal(SensorOfflineTracker.Transition.CameOnline, tracker.Observe("k", false));
        Assert.Equal(SensorOfflineTracker.Transition.None, tracker.Observe("k", false));
    }

    [Fact]
    public void TransientBlip_DoesNotFalseAlarm()
    {
        var tracker = new SensorOfflineTracker();

        Assert.Equal(SensorOfflineTracker.Transition.None, tracker.Observe("k", true));  // one bad poll
        Assert.Equal(SensorOfflineTracker.Transition.None, tracker.Observe("k", false)); // recovered before alert
    }
}

public sealed class CalibrationReminderMessageTests
{
    private static CalibrationEvent Planned(string title) => new() { Status = CalibrationEventStatus.Planned, Title = title };

    [Fact]
    public void NoDueEvents_ReturnsNull()
        => Assert.Null(CalibrationReminderService.BuildDueMessage(new List<CalibrationEvent>()));

    [Fact]
    public void CompletedEvents_AreIgnored()
    {
        var events = new List<CalibrationEvent> { new() { Status = CalibrationEventStatus.Completed, Title = "pH" } };

        Assert.Null(CalibrationReminderService.BuildDueMessage(events));
    }

    [Fact]
    public void SingleDue_NamesTheSensor()
    {
        var message = CalibrationReminderService.BuildDueMessage(new List<CalibrationEvent> { Planned("pH-Sonde") });

        Assert.Equal("Kalibrierung fällig: pH-Sonde.", message);
    }

    [Fact]
    public void MultipleDue_Summarises()
    {
        var message = CalibrationReminderService.BuildDueMessage(new List<CalibrationEvent> { Planned("pH"), Planned("EC"), Planned("ORP") });

        Assert.StartsWith("3 Kalibrierungen fällig:", message);
    }
}
