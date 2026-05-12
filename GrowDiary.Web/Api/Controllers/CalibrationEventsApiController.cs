using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/calibration-events")]
[Produces("application/json")]
public sealed class CalibrationEventsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;

    public CalibrationEventsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CalibrationEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<CalibrationEventDto>> List([FromQuery] int? hardwareItemId = null, [FromQuery] DateTime? dueBeforeUtc = null)
    {
        if (hardwareItemId.HasValue && _repository.GetHardwareItem(hardwareItemId.Value) is null)
        {
            ModelState.AddModelError(nameof(hardwareItemId), $"HardwareItem mit Id {hardwareItemId.Value} existiert nicht.");
            return ValidationError();
        }

        var items = hardwareItemId.HasValue
            ? _repository.GetCalibrationEventsByHardwareItem(hardwareItemId.Value)
            : dueBeforeUtc.HasValue
                ? _repository.GetDueCalibrationEvents(dueBeforeUtc.Value)
                : _repository.GetCalibrationEvents();

        return Ok(items.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CalibrationEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<CalibrationEventDto> Detail(int id)
    {
        var item = _repository.GetCalibrationEvent(id);
        return item is null
            ? NotFoundError("calibration_event_not_found", $"CalibrationEvent mit Id {id} existiert nicht.")
            : Ok(item.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(typeof(CalibrationEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<CalibrationEventDto> Create([FromBody] CreateCalibrationEventRequest request)
    {
        Validate(request.HardwareItemId, request.CalibrationType, request.Status, request.Result, request.Title, request.TemperatureC, request.PerformedAtUtc, request.NextDueAtUtc);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var item = _repository.CreateCalibrationEvent(request.ToModel());
        return CreatedAtAction(nameof(Detail), new { id = item.Id }, item.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CalibrationEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<CalibrationEventDto> Update(int id, [FromBody] UpdateCalibrationEventRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var item = _repository.GetCalibrationEvent(id);
        if (item is null)
        {
            return NotFoundError("calibration_event_not_found", $"CalibrationEvent mit Id {id} existiert nicht.");
        }

        Validate(request.HardwareItemId, request.CalibrationType, request.Status, request.Result, request.Title, request.TemperatureC, request.PerformedAtUtc, request.NextDueAtUtc);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        request.ApplyTo(item);
        _repository.UpdateCalibrationEvent(item);
        return Ok(_repository.GetCalibrationEvent(id)!.ToDto());
    }

    private void Validate(
        int hardwareItemId,
        CalibrationEventType calibrationType,
        CalibrationEventStatus status,
        CalibrationResult result,
        string? title,
        decimal? temperatureC,
        DateTime? performedAtUtc,
        DateTime? nextDueAtUtc)
    {
        if (_repository.GetHardwareItem(hardwareItemId) is null)
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.HardwareItemId), $"HardwareItem mit Id {hardwareItemId} existiert nicht.");
        }

        if (!Enum.IsDefined(calibrationType))
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.CalibrationType), "CalibrationType ist ungueltig.");
        }

        if (!Enum.IsDefined(status))
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.Status), "Status ist ungueltig.");
        }

        if (!Enum.IsDefined(result))
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.Result), "Result ist ungueltig.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.Title), "Title darf nicht leer sein.");
        }

        if (temperatureC is < -10m or > 60m)
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.TemperatureC), "TemperatureC muss zwischen -10 und 60 liegen.");
        }

        if (performedAtUtc.HasValue &&
            nextDueAtUtc.HasValue &&
            nextDueAtUtc.Value.ToUniversalTime() < performedAtUtc.Value.ToUniversalTime())
        {
            ModelState.AddModelError(nameof(CreateCalibrationEventRequest.NextDueAtUtc), "NextDueAtUtc darf nicht vor PerformedAtUtc liegen.");
        }
    }
}
