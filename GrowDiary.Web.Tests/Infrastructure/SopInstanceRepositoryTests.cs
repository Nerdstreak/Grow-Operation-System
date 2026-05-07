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
    public void StartSopInstance_VerhindertDoppelteAktiveSop()
    {
        var growId = CreateGrow();
        var sop = _knowledgeBase.Sops.Single(item => item.Id == "daily-measurement-routine");

        _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null);

        Assert.Throws<InvalidOperationException>(() =>
            _repository.StartSopInstance(growId, sop, SopStartSource.Manual, null, null, null));
    }

    private int CreateGrow()
        => _repository.CreateGrow(new GrowRun
        {
            Name = "SOP Grow",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running
        });

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
