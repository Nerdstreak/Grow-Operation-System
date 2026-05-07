using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class AutoMeasurementStatusService
{
    private readonly GrowRepository _repository;

    public AutoMeasurementStatusService(GrowRepository repository)
    {
        _repository = repository;
    }

    public AutoMeasurementGrowStatusDto? GetGrowStatus(int growId)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return null;
        }

        var configs = _repository.GetAutoMeasurementConfigsByGrow(growId)
            .Select(config => BuildConfigStatus(config, grow))
            .ToList();

        return new AutoMeasurementGrowStatusDto(growId, configs);
    }

    private AutoMeasurementConfigStatusDto BuildConfigStatus(AutoMeasurementConfig config, GrowRun grow)
    {
        var mappings = _repository.GetAutoMeasurementFieldMappings(config.Id);
        var runs = _repository.GetAutoMeasurementRunsByConfig(config.Id);
        var lastRun = runs.FirstOrDefault();
        var latestTransition = GetLatestRelevantTransition(config, grow);

        return new AutoMeasurementConfigStatusDto(
            ConfigId: config.Id,
            GrowId: config.GrowId,
            Name: config.Name,
            Status: config.Status,
            TriggerKind: config.TriggerKind,
            DelayMinutes: config.DelayMinutes,
            WindowMinutes: config.WindowMinutes,
            MappingCount: mappings.Count,
            RequiredMappingCount: mappings.Count(mapping => mapping.IsRequired),
            LastRunStatus: lastRun?.Status,
            LastRunScheduledForUtc: lastRun?.ScheduledForUtc.ToUniversalTime(),
            LastRunMeasurementId: lastRun?.MeasurementId,
            LastRunErrorMessage: lastRun?.ErrorMessage,
            CreatedRunCount: runs.Count(run => run.Status == AutoMeasurementRunStatus.Created),
            SkippedRunCount: runs.Count(run => run.Status == AutoMeasurementRunStatus.Skipped),
            FailedRunCount: runs.Count(run => run.Status == AutoMeasurementRunStatus.Failed),
            LatestRelevantLightTransitionAtUtc: latestTransition?.OccurredAtUtc.ToUniversalTime(),
            LatestRelevantLightTransitionKind: latestTransition?.Kind
        );
    }

    private LightTransitionEvent? GetLatestRelevantTransition(AutoMeasurementConfig config, GrowRun grow)
    {
        var kind = ToTransitionKind(config.TriggerKind);
        var tentId = config.TentId ?? grow.TentId;
        return kind.HasValue && tentId.HasValue
            ? _repository.GetLatestLightTransitionForTentAndKind(tentId.Value, kind.Value)
            : null;
    }

    private static LightTransitionKind? ToTransitionKind(AutoMeasurementTriggerKind triggerKind)
        => triggerKind switch
        {
            AutoMeasurementTriggerKind.LightOnDelay => LightTransitionKind.LightOn,
            AutoMeasurementTriggerKind.LightOffDelay => LightTransitionKind.LightOff,
            _ => null
        };
}
