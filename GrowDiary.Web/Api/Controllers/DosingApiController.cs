using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Dosierpumpen: einrichten, kalibrieren, von Hand dosieren, Protokoll lesen.
/// </summary>
/// <remarks>
/// Stufe 1 — nichts läuft von allein. Jede Dosis wird hier ausgelöst, weil
/// jemand gedrückt hat. Die Automatik kommt erst, wenn Rechnung und Anschläge
/// sich an echten Zelten bewährt haben.
/// </remarks>
[ApiController]
[Route("api/dosing")]
[Produces("application/json")]
public sealed class DosingApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly DosingRepository _dosing;
    private readonly AlertRuleRepository _alertRules;
    private readonly DosingService _service;

    public DosingApiController(
        GrowRepository repository,
        DosingRepository dosing,
        AlertRuleRepository alertRules,
        DosingService service)
    {
        _repository = repository;
        _dosing = dosing;
        _alertRules = alertRules;
        _service = service;
    }

    // ---------- Pumpen ----------

    [HttpGet("pumps")]
    [ProducesResponseType(typeof(IReadOnlyList<DosingPumpDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DosingPumpDto>> GetPumps([FromQuery] int? tentId)
        => Ok(_dosing.GetPumps(tentId).Select(ToDto).ToList());

    [HttpGet("pumps/{id:int}")]
    [ProducesResponseType(typeof(DosingPumpDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<DosingPumpDto> GetPump(int id)
    {
        var pump = _dosing.GetPump(id);
        return pump is null ? NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.") : Ok(ToDto(pump));
    }

    [HttpPost("pumps")]
    [ProducesResponseType(typeof(DosingPumpDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<DosingPumpDto> Create([FromBody] DosingPumpUpsertRequest? request)
    {
        if (Validate(request) is { } error) return error;

        var pump = Apply(new DosingPump(), request!);
        pump.TubeChangedAtUtc = DateTime.UtcNow;   // frisch eingerichtet = frischer Schlauch
        var id = _dosing.InsertPump(pump);
        return CreatedAtAction(nameof(GetPump), new { id }, ToDto(_dosing.GetPump(id)!));
    }

    [HttpPut("pumps/{id:int}")]
    [ProducesResponseType(typeof(DosingPumpDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<DosingPumpDto> Update(int id, [FromBody] DosingPumpUpsertRequest? request)
    {
        var existing = _dosing.GetPump(id);
        if (existing is null) return NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.");
        if (Validate(request) is { } error) return error;

        var pump = Apply(existing, request!);
        if (request!.TubeChangedNow) pump.TubeChangedAtUtc = DateTime.UtcNow;
        _dosing.UpdatePump(pump);
        return Ok(ToDto(_dosing.GetPump(id)!));
    }

    [HttpDelete("pumps/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete(int id)
    {
        _dosing.DeletePump(id);
        return NoContent();
    }

    // ---------- Kalibrieren ----------

    /// <summary>
    /// Lässt die Pumpe für die angegebene Zeit laufen — Schlauchende im
    /// Messbecher. Was herauskommt, trägt der Nutzer danach ein.
    /// </summary>
    [HttpPost("pumps/{id:int}/calibration/run")]
    [ProducesResponseType(typeof(DoseResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoseResultDto>> CalibrationRun(int id, [FromBody] CalibrationRunRequest request, CancellationToken cancellationToken)
    {
        var pump = _dosing.GetPump(id);
        if (pump is null) return NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.");

        var seconds = Math.Clamp(request.Seconds, 5, DosingGuard.AbsoluteMaxSeconds);
        var ok = await _service.RunForSecondsAsync(pump, seconds, cancellationToken);

        _dosing.InsertEvent(new DoseEvent
        {
            PumpId = pump.Id,
            TentId = pump.TentId,
            OccurredAtUtc = DateTime.UtcNow,
            Trigger = DoseTrigger.Calibration,
            Outcome = ok ? DoseOutcome.Done : DoseOutcome.Failed,
            RequestedMl = 0,
            // Vor der Kalibrierung ist die Fördermenge unbekannt — was hier
            // geflossen ist, weiss erst der Messbecher.
            DosedMl = 0,
            SecondsRun = ok ? seconds : 0,
            Reason = ok ? $"Kalibrierlauf {seconds:0.#} s" : "Kalibrierlauf: Home Assistant hat nicht geschaltet.",
            Simulated = pump.SimulationMode,
        });

        return Ok(new DoseResultDto(ok, 0, ok ? seconds : 0,
            ok ? $"{seconds:0.#} s gelaufen — jetzt den Messbecher ablesen."
               : "Home Assistant hat die Pumpe nicht geschaltet."));
    }

    /// <summary>Trägt ein, was im Becher stand, und rechnet die Fördermenge daraus.</summary>
    [HttpPost("pumps/{id:int}/calibration")]
    [ProducesResponseType(typeof(DosingPumpDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<DosingPumpDto> SaveCalibration(int id, [FromBody] CalibrationResultRequest request)
    {
        var pump = _dosing.GetPump(id);
        if (pump is null) return NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.");

        var mlPerMinute = DosingCalculator.MlPerMinuteFrom(request.MeasuredMl, request.Seconds);
        if (mlPerMinute is null)
        {
            return BadRequestError("invalid_calibration", "Menge und Laufzeit müssen beide größer als null sein.");
        }

        _dosing.SaveCalibration(id, mlPerMinute.Value, DateTime.UtcNow);
        return Ok(ToDto(_dosing.GetPump(id)!));
    }

    // ---------- Von Hand dosieren ----------

    [HttpPost("pumps/{id:int}/dose")]
    [ProducesResponseType(typeof(DoseResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoseResultDto>> Dose(int id, [FromBody] ManualDoseRequest request, CancellationToken cancellationToken)
    {
        var pump = _dosing.GetPump(id);
        if (pump is null) return NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.");

        var nowUtc = DateTime.UtcNow;
        var context = BuildContext(pump, nowUtc);
        var decision = DosingGuard.Evaluate(pump, request.Ml, context, nowUtc);

        if (!decision.Allowed)
        {
            // Auch das Nicht-Dosieren wird protokolliert — sonst raetselt man
            // spaeter, warum nichts passiert ist.
            _dosing.InsertEvent(new DoseEvent
            {
                PumpId = pump.Id,
                TentId = pump.TentId,
                OccurredAtUtc = nowUtc,
                Trigger = DoseTrigger.Manual,
                Outcome = DoseOutcome.Rejected,
                RequestedMl = request.Ml,
                ValueBefore = context.Reading,
                Reason = decision.Reason,
                Simulated = pump.SimulationMode,
            });
            return Ok(new DoseResultDto(false, 0, 0, decision.Reason));
        }

        var ok = await _service.RunForSecondsAsync(pump, decision.Seconds, cancellationToken);
        _dosing.InsertEvent(new DoseEvent
        {
            PumpId = pump.Id,
            TentId = pump.TentId,
            OccurredAtUtc = nowUtc,
            Trigger = DoseTrigger.Manual,
            Outcome = ok ? DoseOutcome.Done : DoseOutcome.Failed,
            RequestedMl = request.Ml,
            DosedMl = ok ? decision.Ml : 0,
            SecondsRun = ok ? decision.Seconds : 0,
            ValueBefore = context.Reading,
            Simulated = pump.SimulationMode,
            Reason = ok ? (pump.SimulationMode ? "Testbetrieb — es ist nichts geflossen." : "Von Hand ausgelöst.") : "Home Assistant hat die Pumpe nicht geschaltet.",
        });

        return Ok(new DoseResultDto(ok, ok ? decision.Ml : 0, ok ? decision.Seconds : 0,
            ok ? $"{decision.Ml:0.##} ml gegeben. Erst mischen, dann neu messen."
               : "Home Assistant hat die Pumpe nicht geschaltet."));
    }

    /// <summary>
    /// Sofort aus. Der wichtigste Knopf auf der ganzen Seite.
    /// </summary>
    /// <remarks>
    /// Fragt nichts und prüft nichts — Ausschalten darf nie an einer Bedingung
    /// scheitern. Läuft auch dann, wenn die Pumpe aus Sicht von Grow OS längst
    /// steht: dann kostet es einen wirkungslosen Aufruf, und das ist der
    /// richtige Preis.
    /// </remarks>
    [HttpPost("pumps/{id:int}/stop")]
    [ProducesResponseType(typeof(DoseResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoseResultDto>> Stop(int id, CancellationToken cancellationToken)
    {
        var pump = _dosing.GetPump(id);
        if (pump is null) return NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.");

        var ok = await _service.TurnOffAsync(pump, cancellationToken);
        return Ok(new DoseResultDto(false, 0, 0,
            ok ? $"{pump.Name} ausgeschaltet." : $"{pump.Name} liess sich nicht schalten — in Home Assistant nachsehen."));
    }

    /// <summary>Nur rechnen, nicht dosieren — was würde jetzt herauskommen.</summary>
    [HttpGet("pumps/{id:int}/suggestion")]
    [ProducesResponseType(typeof(DoseResultDto), StatusCodes.Status200OK)]
    public ActionResult<DoseResultDto> Suggestion(int id)
    {
        var pump = _dosing.GetPump(id);
        if (pump is null) return NotFoundError("pump_not_found", $"Pumpe {id} existiert nicht.");

        var nowUtc = DateTime.UtcNow;
        var context = BuildContext(pump, nowUtc);
        var history = _dosing.GetEvents(pumpId: pump.Id, limit: 50);
        var gelernt = DosingCalculator.LearnedChangePerMl(history);

        var target = TargetFor(pump);
        if (context.Reading is not { } ist || target is not { } ziel)
        {
            return Ok(new DoseResultDto(false, 0, 0, "Kein Messwert oder kein Zielwert — nichts zu rechnen."));
        }

        var ml = DosingCalculator.MlToReach(ist, ziel, gelernt);
        if (ml is null)
        {
            return Ok(new DoseResultDto(false, 0, 0, gelernt is null
                ? "Noch keine Erfahrung — die ersten Dosen gibst du von Hand, danach rechnet Grow OS."
                : "Nichts zu tun: der Wert liegt schon richtig, oder diese Pumpe wirkt andersherum."));
        }

        var decision = DosingGuard.Evaluate(pump, ml.Value, context, nowUtc);
        return Ok(new DoseResultDto(decision.Allowed, decision.Ml, decision.Seconds, decision.Reason));
    }

    // ---------- Protokoll ----------

    [HttpGet("log")]
    [ProducesResponseType(typeof(IReadOnlyList<DoseEventDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DoseEventDto>> Log([FromQuery] int? pumpId, [FromQuery] int? tentId, [FromQuery] int limit = 50)
    {
        var namen = _dosing.GetPumps().ToDictionary(pump => pump.Id, pump => pump.Name);
        var events = _dosing.GetEvents(pumpId, tentId, Math.Clamp(limit, 1, 500));
        return Ok(events.Select(dose => new DoseEventDto(
            dose.Id, dose.PumpId, namen.GetValueOrDefault(dose.PumpId, "—"),
            dose.OccurredAtUtc, dose.Trigger.ToString(), dose.Outcome.ToString(),
            dose.RequestedMl, dose.DosedMl, dose.SecondsRun,
            dose.ValueBefore, dose.ValueAfter, dose.TargetValue, dose.Reason, dose.Simulated)).ToList());
    }

    // ---------- Innenleben ----------

    private DosingContext BuildContext(DosingPump pump, DateTime nowUtc)
    {
        var mitternacht = nowUtc.Date;
        var heute = _dosing.GetDosesSince(pump.Id, mitternacht);

        // Der Messwert kommt aus der letzten Messung des Zelts. Live-Werte aus
        // Home Assistant kommen in Stufe 3 dazu, wenn die Automatik gegen sie
        // entscheidet — von Hand genuegt, was zuletzt erfasst wurde.
        double? reading = null;
        TimeSpan? age = null;
        var grow = _repository.GetTent(pump.TentId)?.ActiveGrows.FirstOrDefault();
        if (grow is not null && pump.MetricKey is { } key)
        {
            var letzte = _repository.GetMeasurementsForGrow(grow.Id).FirstOrDefault();
            if (letzte is not null)
            {
                reading = key switch
                {
                    "reservoir-ph" => letzte.ReservoirPh,
                    "reservoir-ec" => letzte.ReservoirEc,
                    _ => null,
                };
                if (reading is not null) age = nowUtc - letzte.TakenAt.ToUniversalTime();
            }
        }

        return new DosingContext(reading, age, null, false, heute, null);
    }

    private double? TargetFor(DosingPump pump)
    {
        // Bewusst schlicht in Stufe 1: der Zielwert kommt aus den Grenzwerten
        // des Zelts, wenn dort welche stehen. Die Phasenziele zieht erst die
        // Automatik heran.
        var tent = _repository.GetTent(pump.TentId);
        if (tent is null || pump.MetricKey is not { } key) return null;

        // Dieselbe Stelle wie Live und Diagnose: der Wert des Nutzers gewinnt.
        if (UserTargets.For(key, _alertRules.GetForTent(tent.Id)) is { Min: { } min, Max: { } max })
        {
            return (min + max) / 2;
        }

        return null;
    }

    private ActionResult? Validate(DosingPumpUpsertRequest? request)
    {
        // Ein unlesbarer Rumpf kommt hier als null an. Ohne diesen Riegel wird
        // daraus ein 500 — ein Serverfehler fuer einen Fehler des Aufrufers.
        if (request is null)
            return BadRequestError("invalid_body", "Der Anfrage-Rumpf ist leer oder unlesbar.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestError("name_required", "Die Pumpe braucht einen Namen.");
        if (!request.SimulationMode && string.IsNullOrWhiteSpace(request.HaEntityId))
            return BadRequestError("entity_required", "Ohne Home-Assistant-Entität lässt sich nichts schalten — oder schalte den Testbetrieb ein.");
        if (_repository.GetTent(request.TentId) is null)
            return BadRequestError("tent_not_found", $"Zelt {request.TentId} existiert nicht.");
        if (request.MaxSingleDoseMl is <= 0)
            return BadRequestError("invalid_limit", "Die größte Einzeldosis muss über null liegen.");
        return null;
    }

    private static DosingPump Apply(DosingPump pump, DosingPumpUpsertRequest request)
    {
        pump.TentId = request.TentId;
        pump.Name = request.Name.Trim();
        pump.Purpose = Enum.TryParse<DosingPurpose>(request.Purpose, ignoreCase: true, out var purpose) ? purpose : DosingPurpose.Custom;
        pump.Agent = string.IsNullOrWhiteSpace(request.Agent) ? null : request.Agent.Trim();
        pump.ConcentrationPercent = request.ConcentrationPercent;
        pump.HaEntityId = request.HaEntityId.Trim();
        pump.CalibrationIntervalDays = request.CalibrationIntervalDays;
        pump.TubeIntervalDays = request.TubeIntervalDays;
        if (request.MaxSingleDoseMl is { } single) pump.MaxSingleDoseMl = single;
        if (request.MinIntervalMinutes is { } interval) pump.MinIntervalMinutes = interval;
        if (request.MaxDosesPerDay is { } doses) pump.MaxDosesPerDay = doses;
        if (request.MaxMlPerDay is { } perDay) pump.MaxMlPerDay = perDay;
        if (request.MaxReadingAgeMinutes is { } age) pump.MaxReadingAgeMinutes = age;
        pump.AutomationEnabled = request.AutomationEnabled;
        pump.HasHomeAssistantAutoOff = request.HasHomeAssistantAutoOff;
        pump.SimulationMode = request.SimulationMode;
        return pump;
    }

    private DosingPumpDto ToDto(DosingPump pump)
    {
        var history = _dosing.GetEvents(pumpId: pump.Id, limit: 50);
        var auswertbar = history.Count(dose =>
            dose.Outcome == DoseOutcome.Done && dose.DosedMl > 0 && dose.ValueBefore is not null && dose.ValueAfter is not null);

        var nowUtc = DateTime.UtcNow;
        var decision = DosingGuard.Evaluate(pump, 0.1, BuildContext(pump, nowUtc), nowUtc);

        return new DosingPumpDto(
            pump.Id, pump.TentId, pump.Name, pump.Purpose.ToString(), pump.Agent, pump.ConcentrationPercent,
            pump.HaEntityId, pump.MlPerMinute, pump.CalibratedAtUtc, pump.TubeChangedAtUtc,
            pump.CalibrationIntervalDays, pump.TubeIntervalDays,
            pump.MaxSingleDoseMl, pump.MinIntervalMinutes, pump.MaxDosesPerDay, pump.MaxMlPerDay,
            pump.MaxReadingAgeMinutes, pump.AutomationEnabled, pump.HasHomeAssistantAutoOff, pump.SimulationMode,
            pump.MetricKey,
            DosingCalculator.LearnedChangePerMl(history),
            auswertbar,
            decision.Allowed ? null : decision.Reason);
    }
}
