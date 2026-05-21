using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

public sealed class DeviationRiskEventSyncServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly DeviationRiskEventSyncService _service;

    public DeviationRiskEventSyncServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "DeviationRiskSyncTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _tempRoot);

        _paths = new AppPaths(_tempRoot);
        TestDatabase.InitializeWithDefaultTent(_paths, tentType: TentType.Production);
        _repository = new GrowRepository(_paths);

        var loader = new KnowledgeBaseLoader(_paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Initialize();
        var analyzer = new DeviationAnalyzerService(new TargetValueService(loader));
        _service = new DeviationRiskEventSyncService(_repository, analyzer, new TreatmentRecommender(loader));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void SyncActiveGrowDeviations_CreatesGenericDeviationRisksWithoutDuplicates()
    {
        var growId = CreateHydroGrow("Risk Sync Hydro");
        AddMeasurement(growId, Utc(2026, 5, 20), ec: 3.2, orp: 700, waterTemp: 25);

        _service.SyncActiveGrowDeviations();
        _service.SyncActiveGrowDeviations();

        var risks = _repository.GetRiskEventsByGrow(growId)
            .Where(risk => risk.Source == RiskEventSource.Deviation)
            .ToList();

        Assert.Equal(3, risks.Count);
        Assert.Contains(risks, risk => risk.DedupeKey == $"deviation:grow:{growId}:hydro.ec");
        Assert.Contains(risks, risk => risk.DedupeKey == $"deviation:grow:{growId}:hydro.orp");
        Assert.Contains(risks, risk => risk.DedupeKey == $"deviation:grow:{growId}:hydro.water-temp");
        Assert.All(risks, risk => Assert.Equal(RiskEventStatus.Open, risk.Status));
        Assert.All(risks, risk => Assert.Contains("Handlung:", risk.Description));
    }

    [Fact]
    public void SyncActiveGrowDeviations_ResolvesDeviationRisksWhenCurrentValuesRecover()
    {
        var growId = CreateHydroGrow("Recovered Hydro");
        AddMeasurement(growId, Utc(2026, 5, 20), ec: 3.2, orp: 700, waterTemp: 25);
        _service.SyncActiveGrowDeviations();

        AddMeasurement(growId, Utc(2026, 5, 21), ec: 0.7, orp: 410, waterTemp: 20);
        AddMeasurement(growId, Utc(2026, 5, 22), ec: 0.7, orp: 410, waterTemp: 20);
        _service.SyncActiveGrowDeviations();

        var risks = _repository.GetRiskEventsByGrow(growId)
            .Where(risk => risk.Source == RiskEventSource.Deviation)
            .ToList();

        Assert.Equal(3, risks.Count);
        Assert.All(risks, risk => Assert.Equal(RiskEventStatus.Resolved, risk.Status));
        Assert.All(risks, risk => Assert.NotNull(risk.ResolvedAtUtc));
    }

    private int CreateHydroGrow(string name)
    {
        var tent = _repository.GetTents().Single();
        return _repository.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = name,
            Status = GrowStatus.Running,
            MediumType = MediumType.Hydro,
            HydroStyle = HydroStyle.RDWC,
            IrrigationType = IrrigationType.ActiveHydro,
            StartDate = Utc(2026, 5, 1)
        });
    }

    private void AddMeasurement(int growId, DateTime takenAt, double ec, double orp, double waterTemp)
    {
        _repository.CreateMeasurement(new Measurement
        {
            GrowId = growId,
            TakenAt = takenAt,
            Stage = GrowStage.Veg,
            Source = ValueOrigin.Manual,
            ReservoirPh = 6.0,
            ReservoirEc = ec,
            OrpMv = orp,
            ReservoirWaterTempC = waterTemp,
            DissolvedOxygenMgL = 8.0
        });
    }

    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web")))
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
