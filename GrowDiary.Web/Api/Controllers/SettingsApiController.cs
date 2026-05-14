using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Produces("application/json")]
public sealed class SettingsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;

    public SettingsApiController(GrowRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(SettingsOverviewDto), StatusCodes.Status200OK)]
    public ActionResult<SettingsOverviewDto> Overview()
        => Ok(new SettingsOverviewDto(
            HomeAssistant: _repository.GetHomeAssistantSettings().ToDto(),
            Tents: _repository.GetTents().Select(tent => tent.ToDto()).ToList()));

    [HttpGet("home-assistant")]
    [ProducesResponseType(typeof(HomeAssistantSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<HomeAssistantSettingsDto> HomeAssistant()
        => Ok(_repository.GetHomeAssistantSettings().ToDto());

    [HttpPut("home-assistant")]
    [ProducesResponseType(typeof(HomeAssistantSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<HomeAssistantSettingsDto> SaveHomeAssistant([FromBody] SaveHomeAssistantSettingsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var settings = request.ToModel();
        _repository.SaveHomeAssistantSettings(settings);
        return Ok(_repository.GetHomeAssistantSettings().ToDto());
    }

    [HttpGet("tents")]
    [ProducesResponseType(typeof(IReadOnlyList<TentDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TentDto>> Tents([FromQuery] bool includeArchived = false)
        => Ok(_repository.GetTents(includeArchived).Select(tent => tent.ToDto()).ToList());

    [HttpGet("tents/{id:int}")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<TentDto> Tent(int id)
    {
        var tent = _repository.GetTent(id);
        if (tent is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        return Ok(tent.ToDto());
    }

    [HttpPost("tents")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<TentDto> CreateTent([FromBody] CreateTentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        ValidateTentRequest(request);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var created = _repository.CreateTent(request.ToModel());
        if (request.Sensors is not null)
        {
            _repository.ReplaceTentSensors(created.Id, request.ToSensors(created.Id));
            created = _repository.GetTent(created.Id)!;
        }

        return CreatedAtAction(nameof(Tent), new { id = created.Id }, created.ToDto());
    }

    [HttpPut("tents/{id:int}")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<TentDto> SaveTent(int id, [FromBody] UpdateTentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var existing = _repository.GetTent(id);
        if (existing is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        ValidateTentRequest(request);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var tentToSave = request.ToModel(id);
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            tentToSave.Status = existing.Status;
        }

        _repository.UpdateTent(tentToSave);
        if (request.Sensors is not null)
        {
            _repository.ReplaceTentSensors(id, request.ToSensors(id));
        }

        return Ok(_repository.GetTent(id)!.ToDto());
    }


    [HttpPost("tents/{id:int}/archive")]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<TentDto> ArchiveTent(int id)
    {
        var existing = _repository.GetTent(id);
        if (existing is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        _repository.ArchiveTent(id);
        return Ok(_repository.GetTent(id)!.ToDto());
    }

    [HttpDelete("tents/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(TentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public IActionResult DeleteTent(int id)
    {
        var existing = _repository.GetTent(id);
        if (existing is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {id} existiert nicht.");
        }

        if (_repository.HasTentDependencies(id))
        {
            _repository.ArchiveTent(id);
            return Ok(_repository.GetTent(id)!.ToDto());
        }

        _repository.DeleteTent(id);
        return NoContent();
    }

    private void ValidateTentRequest(CreateTentRequest request)
    {
        ValidateTentBase(
            request.Name,
            request.TentType,
            request.Status,
            request.LightController,
            request.HvacController,
            request.WidthCm,
            request.DepthCm,
            request.TentHeightCm,
            request.LightWatt,
            request.ExhaustFanCount,
            request.ExhaustM3h,
            request.CirculationFanCount,
            request.Sensors);
    }

    private void ValidateTentRequest(UpdateTentRequest request)
    {
        ValidateTentBase(
            request.Name,
            request.TentType,
            request.Status,
            request.LightController,
            request.HvacController,
            request.WidthCm,
            request.DepthCm,
            request.TentHeightCm,
            request.LightWatt,
            request.ExhaustFanCount,
            request.ExhaustM3h,
            request.CirculationFanCount,
            request.Sensors);
    }

    private void ValidateTentBase(
        string name,
        string? tentType,
        string? status,
        string? lightController,
        string? hvacController,
        int? widthCm,
        int? depthCm,
        int? tentHeightCm,
        int? lightWatt,
        int? exhaustFanCount,
        int? exhaustM3h,
        int? circulationFanCount,
        IReadOnlyList<UpdateTentSensorRequest>? sensors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.Name), "Bitte gib dem Zelt einen Namen.");
        }

        if (!string.IsNullOrWhiteSpace(tentType) && !Enum.TryParse<TentType>(tentType, out _))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.TentType), $"Tent-Typ {tentType} ist nicht erlaubt.");
        }

        if (!string.IsNullOrWhiteSpace(status) && !Enum.TryParse<TentStatus>(status, out _))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.Status), $"Tent-Status {status} ist nicht erlaubt.");
        }

        if (!string.IsNullOrWhiteSpace(lightController) && !Enum.TryParse<LightControllerType>(lightController, out _))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.LightController), $"Light-Controller {lightController} ist nicht erlaubt.");
        }

        if (!string.IsNullOrWhiteSpace(hvacController) && !Enum.TryParse<HvacControllerType>(hvacController, out _))
        {
            ModelState.AddModelError(nameof(UpdateTentRequest.HvacController), $"HVAC-Controller {hvacController} ist nicht erlaubt.");
        }

        AddPositiveError(widthCm, nameof(UpdateTentRequest.WidthCm), "Breite muss größer als 0 sein.");
        AddPositiveError(depthCm, nameof(UpdateTentRequest.DepthCm), "Tiefe muss größer als 0 sein.");
        AddPositiveError(tentHeightCm, nameof(UpdateTentRequest.TentHeightCm), "Höhe muss größer als 0 sein.");
        AddPositiveError(lightWatt, nameof(UpdateTentRequest.LightWatt), "Lichtleistung muss größer als 0 sein.");
        AddNonNegativeError(exhaustFanCount, nameof(UpdateTentRequest.ExhaustFanCount), "Abluft-Anzahl darf nicht negativ sein.");
        AddNonNegativeError(exhaustM3h, nameof(UpdateTentRequest.ExhaustM3h), "Abluftleistung darf nicht negativ sein.");
        AddNonNegativeError(circulationFanCount, nameof(UpdateTentRequest.CirculationFanCount), "Umluft-Anzahl darf nicht negativ sein.");

        if (sensors is null) return;
        for (var index = 0; index < sensors.Count; index++)
        {
            var sensor = sensors[index];
            if (!Enum.TryParse<SensorMetricType>(sensor.MetricType, out _))
            {
                ModelState.AddModelError($"Sensors[{index}].{nameof(UpdateTentSensorRequest.MetricType)}", $"Sensor-Metrik {sensor.MetricType} ist nicht erlaubt.");
            }

            if (sensor.IsActive && string.IsNullOrWhiteSpace(sensor.HaEntityId))
            {
                ModelState.AddModelError($"Sensors[{index}].{nameof(UpdateTentSensorRequest.HaEntityId)}", "Aktive Sensoren benötigen eine Home-Assistant Entity ID.");
            }
        }
    }

    private void AddPositiveError(int? value, string fieldName, string message)
    {
        if (value.HasValue && value.Value <= 0)
        {
            ModelState.AddModelError(fieldName, message);
        }
    }

    private void AddNonNegativeError(int? value, string fieldName, string message)
    {
        if (value.HasValue && value.Value < 0)
        {
            ModelState.AddModelError(fieldName, message);
        }
    }
}
