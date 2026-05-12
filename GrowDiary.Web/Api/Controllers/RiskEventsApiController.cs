using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/risk-events")]
[Produces("application/json")]
public sealed class RiskEventsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly TaskRepository _taskRepository;
    private readonly KnowledgeBaseLoader _knowledgeBase;
    private readonly RiskEventSopRecommender _sopRecommender;

    public RiskEventsApiController(
        GrowRepository repository,
        TaskRepository taskRepository,
        KnowledgeBaseLoader knowledgeBase,
        RiskEventSopRecommender sopRecommender)
    {
        _repository = repository;
        _taskRepository = taskRepository;
        _knowledgeBase = knowledgeBase;
        _sopRecommender = sopRecommender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RiskEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<RiskEventDto>> List(
        [FromQuery] RiskEventStatus? status = null,
        [FromQuery] int? tentId = null,
        [FromQuery] int? growId = null,
        [FromQuery] int? hardwareItemId = null)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            ModelState.AddModelError(nameof(status), "Status ist ungueltig.");
            return ValidationError();
        }

        var items = status.HasValue
            ? _repository.GetRiskEventsByStatus(status.Value)
            : tentId.HasValue
                ? _repository.GetRiskEventsByTent(tentId.Value)
                : growId.HasValue
                    ? _repository.GetRiskEventsByGrow(growId.Value)
                    : hardwareItemId.HasValue
                        ? _repository.GetRiskEventsByHardwareItem(hardwareItemId.Value)
                        : _repository.GetRiskEvents();

        return Ok(items.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RiskEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<RiskEventDto> Detail(int id)
    {
        var item = _repository.GetRiskEvent(id);
        return item is null
            ? NotFoundError("risk_event_not_found", $"RiskEvent mit Id {id} existiert nicht.")
            : Ok(item.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(RiskEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<RiskEventDto> Create([FromBody] CreateRiskEventRequest request)
    {
        Validate(request.EventType, request.Severity, request.Status, request.Source, request.Title, request.HardwareItemId, request.TentId, request.GrowId, request.TentSensorId, request.StartedAtUtc, request.LastSeenAtUtc, request.ResolvedAtUtc, request.AcknowledgedAtUtc);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var item = _repository.CreateRiskEvent(request.ToModel());
        return CreatedAtAction(nameof(Detail), new { id = item.Id }, item.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RiskEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<RiskEventDto> Update(int id, [FromBody] UpdateRiskEventRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var item = _repository.GetRiskEvent(id);
        if (item is null)
        {
            return NotFoundError("risk_event_not_found", $"RiskEvent mit Id {id} existiert nicht.");
        }

        Validate(request.EventType, request.Severity, request.Status, request.Source, request.Title, request.HardwareItemId, request.TentId, request.GrowId, request.TentSensorId, request.StartedAtUtc, request.LastSeenAtUtc, request.ResolvedAtUtc, request.AcknowledgedAtUtc);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        request.ApplyTo(item);
        _repository.UpdateRiskEvent(item);
        return Ok(_repository.GetRiskEvent(id)!.ToDto());
    }

    [HttpPost("{id:int}/acknowledge")]
    [ProducesResponseType(typeof(RiskEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<RiskEventDto> Acknowledge(int id, [FromBody] AcknowledgeRiskEventRequest request)
    {
        if (_repository.GetRiskEvent(id) is null)
        {
            return NotFoundError("risk_event_not_found", $"RiskEvent mit Id {id} existiert nicht.");
        }

        return Ok(_repository.AcknowledgeRiskEvent(id, request.AcknowledgedAtUtc ?? DateTime.UtcNow, request.Notes).ToDto());
    }

    [HttpPost("{id:int}/resolve")]
    [ProducesResponseType(typeof(RiskEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<RiskEventDto> Resolve(int id, [FromBody] ResolveRiskEventRequest request)
    {
        if (_repository.GetRiskEvent(id) is null)
        {
            return NotFoundError("risk_event_not_found", $"RiskEvent mit Id {id} existiert nicht.");
        }

        return Ok(_repository.ResolveRiskEvent(id, request.ResolvedAtUtc ?? DateTime.UtcNow, request.Notes).ToDto());
    }

    [HttpGet("{id:int}/sop-recommendations")]
    [ProducesResponseType(typeof(IReadOnlyList<RiskEventSopRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<RiskEventSopRecommendationDto>> SopRecommendations(int id)
    {
        var item = _repository.GetRiskEvent(id);
        if (item is null)
        {
            return NotFoundError("risk_event_not_found", $"RiskEvent mit Id {id} existiert nicht.");
        }

        return Ok(_sopRecommender.Recommend(item));
    }

    [HttpPost("{id:int}/start-sop")]
    [ProducesResponseType(typeof(SopInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public ActionResult<SopInstanceDto> StartSop(int id, [FromBody] StartRiskEventSopRequest request)
    {
        var riskEvent = _repository.GetRiskEvent(id);
        if (riskEvent is null)
        {
            return NotFoundError("risk_event_not_found", $"RiskEvent mit Id {id} existiert nicht.");
        }

        if (!riskEvent.GrowId.HasValue)
        {
            return BadRequest(new ApiError("risk_event_has_no_grow", "RiskEvent hat keinen Grow-Bezug. SOP-Start benoetigt GrowId."));
        }

        var sop = _knowledgeBase.Sops.FirstOrDefault(item => string.Equals(item.Id, request.SopId, StringComparison.OrdinalIgnoreCase));
        if (sop is null)
        {
            ModelState.AddModelError(nameof(StartRiskEventSopRequest.SopId), $"SOP mit Id '{request.SopId}' existiert nicht.");
        }
        else if (sop.Steps.Count == 0)
        {
            ModelState.AddModelError(nameof(StartRiskEventSopRequest.SopId), $"SOP '{request.SopId}' hat keine ausfuehrbaren Steps.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        try
        {
            var notes = BuildRiskEventSopNotes(riskEvent, request.Notes);
            var instance = _repository.StartSopInstance(
                riskEvent.GrowId.Value,
                sop!,
                SopStartSource.Recommendation,
                $"risk-event:{riskEvent.Id}:{sop!.Id}",
                null,
                notes);

            CreateReminderTasksForSteps(instance);

            if (!riskEvent.SopInstanceId.HasValue)
            {
                riskEvent.SopInstanceId = instance.Id;
                _repository.UpdateRiskEvent(riskEvent);
            }

            return CreatedAtAction("Detail", "SopInstances", new { id = instance.Id }, _repository.GetSopInstance(instance.Id)!.ToDto());
        }
        catch (InvalidOperationException)
        {
            return Conflict(new ApiError("active_sop_exists", "Fuer diesen Grow ist diese SOP bereits aktiv."));
        }
    }

    private void Validate(
        RiskEventType eventType,
        RiskEventSeverity severity,
        RiskEventStatus status,
        RiskEventSource source,
        string? title,
        int? hardwareItemId,
        int? tentId,
        int? growId,
        int? tentSensorId,
        DateTime? startedAtUtc,
        DateTime? lastSeenAtUtc,
        DateTime? resolvedAtUtc,
        DateTime? acknowledgedAtUtc)
    {
        if (!Enum.IsDefined(eventType)) ModelState.AddModelError(nameof(CreateRiskEventRequest.EventType), "EventType ist ungueltig.");
        if (!Enum.IsDefined(severity)) ModelState.AddModelError(nameof(CreateRiskEventRequest.Severity), "Severity ist ungueltig.");
        if (!Enum.IsDefined(status)) ModelState.AddModelError(nameof(CreateRiskEventRequest.Status), "Status ist ungueltig.");
        if (!Enum.IsDefined(source)) ModelState.AddModelError(nameof(CreateRiskEventRequest.Source), "Source ist ungueltig.");
        if (string.IsNullOrWhiteSpace(title)) ModelState.AddModelError(nameof(CreateRiskEventRequest.Title), "Title darf nicht leer sein.");

        if (hardwareItemId.HasValue && _repository.GetHardwareItem(hardwareItemId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.HardwareItemId), $"HardwareItem mit Id {hardwareItemId.Value} existiert nicht.");
        }

        if (tentId.HasValue && _repository.GetTent(tentId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.TentId), $"Tent mit Id {tentId.Value} existiert nicht.");
        }

        if (growId.HasValue && _repository.GetGrow(growId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.GrowId), $"Grow mit Id {growId.Value} existiert nicht.");
        }

        if (tentSensorId.HasValue && _repository.GetTentSensor(tentSensorId.Value) is null)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.TentSensorId), $"TentSensor mit Id {tentSensorId.Value} existiert nicht.");
        }

        var started = (startedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        if (lastSeenAtUtc.HasValue && lastSeenAtUtc.Value.ToUniversalTime() < started)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.LastSeenAtUtc), "LastSeenAtUtc darf nicht vor StartedAtUtc liegen.");
        }

        if (resolvedAtUtc.HasValue && resolvedAtUtc.Value.ToUniversalTime() < started)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.ResolvedAtUtc), "ResolvedAtUtc darf nicht vor StartedAtUtc liegen.");
        }

        if (acknowledgedAtUtc.HasValue && acknowledgedAtUtc.Value.ToUniversalTime() < started)
        {
            ModelState.AddModelError(nameof(CreateRiskEventRequest.AcknowledgedAtUtc), "AcknowledgedAtUtc darf nicht vor StartedAtUtc liegen.");
        }
    }

    private void CreateReminderTasksForSteps(SopInstance instance)
    {
        var steps = _repository.GetSopStepInstances(instance.Id);
        foreach (var step in steps.Where(s => s.DueAtUtc.HasValue))
        {
            var task = new GrowTask
            {
                GrowId = instance.GrowId,
                Title = $"SOP: {instance.SopName} - {step.Title}",
                DueAtUtc = step.DueAtUtc,
                Priority = TaskPriority.Normal,
                Status = GrowTaskStatus.Open
            };
            var taskId = _taskRepository.Create(task);
            _repository.UpdateSopStepReminderTaskId(step.Id, taskId);
        }
    }

    private static string BuildRiskEventSopNotes(RiskEvent riskEvent, string? requestNotes)
    {
        var prefix = $"Gestartet aus RiskEvent #{riskEvent.Id}: {riskEvent.Title}";
        return string.IsNullOrWhiteSpace(requestNotes) ? prefix : $"{prefix}\n{requestNotes.Trim()}";
    }
}
