using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class SopInstanceRepositoryTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly KnowledgeBaseLoader _knowledgeBase;

    public SopInstanceRepositoryTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-sop-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _contentRoot);
        _paths = new AppPaths(_contentRoot);
        new DatabaseInitializer(_paths, NullLogger<DatabaseInitializer>.Instance).Initialize();
        _repository = new GrowRepository(_paths);
        _knowledgeBase = new KnowledgeBaseLoader(_paths, NullLogger<KnowledgeBaseLoader>.Instance);
        _knowledgeBase.Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Fact]
    public void DatabaseInitializer_LegtSopTabellenAdditivAn()
    {
        Assert.True(TableExists("SopInstances"));
        Assert.True(TableExists("SopStepInstances"));
    }

    [Fact]
    public void StartSopInstance_MaterialisiertKnowledgeSopUndSteps()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "weekly-water-change");

        var instance = _repository.StartSopInstance(
            growId,
            sop,
            SopStartSource.Manual,
            sourceRecommendationKey: null,
            treatmentRecommendationStableKey: null,
            notes: "Start aus Test");
        var stored = _repository.GetSopInstance(instance.Id)!;
        var steps = _repository.GetSopStepInstances(instance.Id);

        Assert.Equal(growId, stored.GrowId);
        Assert.Equal("weekly-water-change", stored.SopId);
        Assert.Equal(sop.Name, stored.SopName);
        Assert.Equal(sop.Type, stored.SopType);
        Assert.Equal(SopInstanceStatus.Active, stored.Status);
        Assert.Equal(SopStartSource.Manual, stored.Source);
        Assert.Equal("Start aus Test", stored.Notes);
        Assert.Equal(sop.Steps.Count, steps.Count);
        Assert.Equal(sop.Steps.Select(step => step.Order), steps.Select(step => step.Order));
        Assert.All(steps, step => Assert.Equal(SopStepInstanceStatus.Pending, step.Status));

        var subSopStep = steps.Single(step => step.SubSopId == "mixing-order-rdwc-ro");
        Assert.Equal("SubSop", subSopStep.StepType);
        Assert.Contains("ph", steps.First().ExpectedInputsJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSopInstancesByGrow_AndActive_ReturnInstancesForGrow()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "daily-measurement-routine");
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);

        var all = _repository.GetSopInstancesByGrow(growId);
        var active = _repository.GetActiveSopInstancesByGrow(growId);

        Assert.Contains(all, item => item.Id == instance.Id);
        Assert.Contains(active, item => item.Id == instance.Id);
    }

    [Fact]
    public void StartSopInstance_SpeichertRecommendationSourceUndKeys()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "emergency-power-recovery");

        var instance = _repository.StartSopInstance(
            growId,
            sop,
            SopStartSource.Recommendation,
            "source-rec-key",
            "treatment-rec-key",
            "Gestartet aus Diagnoseempfehlung");
        var stored = _repository.GetSopInstance(instance.Id)!;

        Assert.Equal(SopStartSource.Recommendation, stored.Source);
        Assert.Equal("source-rec-key", stored.SourceRecommendationKey);
        Assert.Equal("treatment-rec-key", stored.TreatmentRecommendationStableKey);
        Assert.Equal("Gestartet aus Diagnoseempfehlung", stored.Notes);
    }

    [Fact]
    public void StartSopInstance_VerhindertDoppelteAktiveSop()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "daily-measurement-routine");

        _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);

        Assert.Throws<InvalidOperationException>(() =>
            _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null));
    }

    [Fact]
    public void UpdateSopStepInstance_SetztInProgressUndNotes()
    {
        var instance = StartRoutine();
        var step = _repository.GetSopStepInstances(instance.Id).First();

        var updated = _repository.UpdateSopStepInstance(
            step.Id,
            SopStepInstanceStatus.InProgress,
            "In Arbeit",
            measurementId: null,
            journalEntryId: null,
            photoAssetId: null);

        Assert.Equal(SopStepInstanceStatus.InProgress, updated.Status);
        Assert.NotNull(updated.StartedAtUtc);
        Assert.Null(updated.CompletedAtUtc);
        Assert.Null(updated.SkippedAtUtc);
        Assert.Equal("In Arbeit", updated.Notes);
        Assert.Equal(SopInstanceStatus.Active, _repository.GetSopInstance(instance.Id)!.Status);
    }

    [Fact]
    public void UpdateSopStepInstance_SetztDoneUndCompletedAt()
    {
        var instance = StartRoutine();
        var step = _repository.GetSopStepInstances(instance.Id).First();

        var updated = _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Done, "Erledigt", null, null, null);

        Assert.Equal(SopStepInstanceStatus.Done, updated.Status);
        Assert.NotNull(updated.StartedAtUtc);
        Assert.NotNull(updated.CompletedAtUtc);
        Assert.Null(updated.SkippedAtUtc);
        Assert.Equal("Erledigt", updated.Notes);
    }

    [Fact]
    public void UpdateSopStepInstance_SetztSkippedUndSkippedAt()
    {
        var instance = StartRoutine();
        var step = _repository.GetSopStepInstances(instance.Id).First();

        var updated = _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Skipped, "Nicht noetig", null, null, null);

        Assert.Equal(SopStepInstanceStatus.Skipped, updated.Status);
        Assert.Null(updated.StartedAtUtc);
        Assert.Null(updated.CompletedAtUtc);
        Assert.NotNull(updated.SkippedAtUtc);
        Assert.Equal("Nicht noetig", updated.Notes);
    }

    [Fact]
    public void UpdateSopStepInstance_PendingResetLeertZeitstempel()
    {
        var instance = StartRoutine();
        var step = _repository.GetSopStepInstances(instance.Id).First();
        _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Done, "Erledigt", null, null, null);

        var reset = _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Pending, null, null, null, null);

        Assert.Equal(SopStepInstanceStatus.Pending, reset.Status);
        Assert.Null(reset.StartedAtUtc);
        Assert.Null(reset.CompletedAtUtc);
        Assert.Null(reset.SkippedAtUtc);
        Assert.Null(reset.Notes);
    }

    [Fact]
    public void UpdateSopStepInstance_SchliesstSopWennAlleStepsDoneOderSkippedSind()
    {
        var instance = StartRoutine();
        var steps = _repository.GetSopStepInstances(instance.Id);

        for (var i = 0; i < steps.Count; i++)
        {
            var status = i % 2 == 0 ? SopStepInstanceStatus.Done : SopStepInstanceStatus.Skipped;
            _repository.UpdateSopStepInstance(steps[i].Id, status, $"Step {i}", null, null, null);
        }

        var stored = _repository.GetSopInstance(instance.Id)!;
        Assert.Equal(SopInstanceStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAtUtc);
    }

    [Fact]
    public void UpdateSopStepInstance_VerweigertUpdateBeiCompletedSop()
    {
        var instance = StartRoutine();
        foreach (var step in _repository.GetSopStepInstances(instance.Id))
        {
            _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Done, null, null, null, null);
        }

        var completedStep = _repository.GetSopStepInstances(instance.Id).First();

        Assert.Throws<InvalidOperationException>(() =>
            _repository.UpdateSopStepInstance(completedStep.Id, SopStepInstanceStatus.Pending, null, null, null, null));
    }

    [Fact]
    public void GetSopStepInstance_GibtNullBeiUnbekanntemStep()
    {
        Assert.Null(_repository.GetSopStepInstance(999999));
    }

    // E4 Scheduling Tests

    [Fact]
    public void StartSopInstance_LinearSop_ErsterStepBekommtDueAtUtcGleichStart()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "flip-to-flower");
        Assert.Equal("Linear", sop.Type);
        Assert.True(sop.Steps.Count > 1);

        var before = DateTime.UtcNow.AddSeconds(-2);
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var after = DateTime.UtcNow.AddSeconds(2);
        var steps = _repository.GetSopStepInstances(instance.Id).OrderBy(s => s.Order).ToList();

        var firstStep = steps[0];
        Assert.NotNull(firstStep.DueAtUtc);
        Assert.True(firstStep.DueAtUtc >= before && firstStep.DueAtUtc <= after,
            $"firstStep.DueAtUtc={firstStep.DueAtUtc} nicht in [{before}, {after}]");
        Assert.Null(firstStep.AvailableAtUtc);

        foreach (var laterStep in steps.Skip(1).Where(s => !s.WaitMinutes.HasValue))
        {
            Assert.Null(laterStep.DueAtUtc);
            Assert.Null(laterStep.AvailableAtUtc);
        }
    }

    [Fact]
    public void StartSopInstance_WaitStep_BekommtAvailableAtUtcUndDueAtUtc()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "emergency-power-recovery");
        var waitStepDef = sop.Steps.Single(s => s.WaitMinutes.HasValue);
        Assert.Equal(30, waitStepDef.WaitMinutes);

        var before = DateTime.UtcNow.AddSeconds(-2);
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var after = DateTime.UtcNow.AddSeconds(2);
        var steps = _repository.GetSopStepInstances(instance.Id);

        var waitStep = steps.Single(s => s.WaitMinutes.HasValue);
        Assert.NotNull(waitStep.DueAtUtc);
        Assert.NotNull(waitStep.AvailableAtUtc);

        var expectedMinDue = before.AddMinutes(30);
        var expectedMaxDue = after.AddMinutes(30);
        Assert.True(waitStep.DueAtUtc >= expectedMinDue && waitStep.DueAtUtc <= expectedMaxDue,
            $"WaitStep.DueAtUtc={waitStep.DueAtUtc} nicht in [{expectedMinDue}, {expectedMaxDue}]");
        Assert.Equal(waitStep.DueAtUtc, waitStep.AvailableAtUtc);
    }

    [Fact]
    public void StartSopInstance_MultiDaySop_SetztInstanceDueAtUtcAnhandDurationDays()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "cuttings-quarantine");
        Assert.Equal("MultiDay", sop.Type);
        Assert.Equal(7, sop.DurationDays);

        var before = DateTime.UtcNow.AddSeconds(-2);
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var after = DateTime.UtcNow.AddSeconds(2);
        var stored = _repository.GetSopInstance(instance.Id)!;

        Assert.NotNull(stored.DueAtUtc);
        var expectedMinDue = before.AddDays(7);
        var expectedMaxDue = after.AddDays(7);
        Assert.True(stored.DueAtUtc >= expectedMinDue && stored.DueAtUtc <= expectedMaxDue,
            $"DueAtUtc={stored.DueAtUtc} nicht in [{expectedMinDue}, {expectedMaxDue}]");
    }

    [Fact]
    public void StartSopInstance_RecurringSop_SetztIsRecurring()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "daily-measurement-routine");
        Assert.Equal("Recurring", sop.Type);

        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var stored = _repository.GetSopInstance(instance.Id)!;

        Assert.True(stored.IsRecurring);
        Assert.Equal(sop.IntervalDays, stored.RecurrenceIntervalDays);
    }

    [Fact]
    public void StartSopInstance_SetzNextStepDueAtUtc()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "flip-to-flower");

        var before = DateTime.UtcNow.AddSeconds(-2);
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var after = DateTime.UtcNow.AddSeconds(2);
        var stored = _repository.GetSopInstance(instance.Id)!;

        Assert.NotNull(stored.NextStepDueAtUtc);
        Assert.True(stored.NextStepDueAtUtc >= before && stored.NextStepDueAtUtc <= after,
            $"NextStepDueAtUtc={stored.NextStepDueAtUtc} nicht in [{before}, {after}]");
    }

    [Fact]
    public void UpdateSopStepInstance_AktualisiertNextStepDueAtUtcNachStepDone()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "flip-to-flower");
        Assert.True(sop.Steps.Count > 1);
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var steps = _repository.GetSopStepInstances(instance.Id).OrderBy(s => s.Order).ToList();

        _repository.UpdateSopStepInstance(steps[0].Id, SopStepInstanceStatus.Done, null, null, null, null);

        var stored = _repository.GetSopInstance(instance.Id)!;
        Assert.Equal(SopInstanceStatus.Active, stored.Status);
    }

    [Fact]
    public void UpdateSopStepInstance_NachCompletion_NextStepDueAtUtcIstNull()
    {
        var instance = StartRoutine();
        var steps = _repository.GetSopStepInstances(instance.Id);

        foreach (var step in steps)
        {
            _repository.UpdateSopStepInstance(step.Id, SopStepInstanceStatus.Done, null, null, null, null);
        }

        var stored = _repository.GetSopInstance(instance.Id)!;
        Assert.Equal(SopInstanceStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAtUtc);
        Assert.Null(stored.NextStepDueAtUtc);
    }

    [Fact]
    public void StartSopInstance_DueAtUtc_RoundtripBehältUtcKindOhneZeitverschiebung()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "flip-to-flower");

        var before = DateTime.UtcNow.AddSeconds(-2);
        var instance = _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
        var after = DateTime.UtcNow.AddSeconds(2);

        var steps = _repository.GetSopStepInstances(instance.Id).OrderBy(s => s.Order).ToList();
        var stored = _repository.GetSopInstance(instance.Id)!;

        // Step DueAtUtc: Kind muss Utc sein, kein Local (keine Zeitzonenverschiebung)
        Assert.NotNull(steps[0].DueAtUtc);
        Assert.Equal(DateTimeKind.Utc, steps[0].DueAtUtc!.Value.Kind);
        Assert.True(steps[0].DueAtUtc >= before && steps[0].DueAtUtc <= after);

        // Instance NextStepDueAtUtc: ebenfalls Utc Kind
        Assert.NotNull(stored.NextStepDueAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.NextStepDueAtUtc!.Value.Kind);
        Assert.True(stored.NextStepDueAtUtc >= before && stored.NextStepDueAtUtc <= after);
    }

    private int CreateGrow()
        => _repository.CreateGrow(new GrowRun
        {
            Name = "SOP Grow",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running
        });

    private SopInstance StartRoutine()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "daily-measurement-routine");
        return _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);
    }

    private bool TableExists(string tableName)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Project root not found");
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var dest = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
