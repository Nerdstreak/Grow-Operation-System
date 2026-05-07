using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class LightStatusTransitionService
{
    private readonly GrowRepository _repository;
    private readonly Dictionary<int, LightState> _lastKnownStateByTent = new();
    private readonly object _gate = new();

    public LightStatusTransitionService(GrowRepository repository)
    {
        _repository = repository;
    }

    public LightTransitionEvent? Process(int tentId, HomeAssistantState state, DateTime occurredAtUtc)
    {
        var current = LightStateNormalizer.Normalize(state.State);
        if (current == LightState.Unknown)
        {
            return null;
        }

        lock (_gate)
        {
            var previous = GetPreviousState(tentId);
            _lastKnownStateByTent[tentId] = current;

            if (previous == current || previous == LightState.Unknown)
            {
                return null;
            }

            var kind = current == LightState.On
                ? LightTransitionKind.LightOn
                : LightTransitionKind.LightOff;

            return _repository.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
            {
                TentId = tentId,
                Kind = kind,
                OccurredAtUtc = occurredAtUtc,
                Source = LightSource.HomeAssistant,
                RawState = state.State
            });
        }
    }

    private LightState GetPreviousState(int tentId)
    {
        if (_lastKnownStateByTent.TryGetValue(tentId, out var known))
        {
            return known;
        }

        var latest = _repository.GetLatestLightTransitionForTent(tentId);
        return latest?.Kind switch
        {
            LightTransitionKind.LightOn => LightState.On,
            LightTransitionKind.LightOff => LightState.Off,
            _ => LightState.Unknown
        };
    }
}
