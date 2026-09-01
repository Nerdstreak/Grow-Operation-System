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

            if (previous == current || previous == LightState.Unknown)
            {
                // Kein Uebergang, aber der Zustand ist jetzt bekannt.
                _lastKnownStateByTent[tentId] = current;
                return null;
            }

            var kind = current == LightState.On
                ? LightTransitionKind.LightOn
                : LightTransitionKind.LightOff;

            /* Erst SCHREIBEN, dann merken.
               Bis zum 01.09.2026 stand `_lastKnownStateByTent[tentId] = current`
               VOR dem Schreiben. Wirft die Ablage — ein Datenbank-Konflikt
               genuegt —, haelt die Entprellung den neuen Zustand trotzdem fuer
               bekannt, und die Flanke ist fuer immer weg: kein Eintrag in der
               Historie, kein Lichteinbruch-Alarm, und ein verzerrter gelernter
               Zyklus. Ausgerechnet in dem Poll, in dem in der Dunkelphase das
               Licht angeht.

               Dieselbe Form wie im PumpWatchNotifier und im TrendWatchRunner. */
            var flanke = _repository.CreateLightTransitionIfNotDuplicate(new LightTransitionEvent
            {
                TentId = tentId,
                Kind = kind,
                OccurredAtUtc = occurredAtUtc,
                Source = LightSource.HomeAssistant,
                RawState = state.State
            });

            _lastKnownStateByTent[tentId] = current;
            return flanke;
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
