using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class MeasurementsApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly AuditRepository _auditRepository;
    private readonly MeasurementSanityService _measurementSanityService;
    private readonly PhotoStorageService _photoStorageService;

    public MeasurementsApiController(
        GrowRepository repository,
        AuditRepository auditRepository,
        MeasurementSanityService measurementSanityService,
        PhotoStorageService photoStorageService)
    {
        _repository = repository;
        _auditRepository = auditRepository;
        _measurementSanityService = measurementSanityService;
        _photoStorageService = photoStorageService;
    }

    [HttpGet("grows/{growId:int}/measurements")]
    [ProducesResponseType(typeof(IReadOnlyList<MeasurementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<MeasurementDto>> List(int growId)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        return Ok(_repository.GetMeasurementsForGrow(growId).Select(measurement => measurement.ToDto()).ToList());
    }

    [HttpGet("measurements/{measurementId:int}")]
    [ProducesResponseType(typeof(MeasurementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<MeasurementDto> Detail(int measurementId)
    {
        var measurement = _repository.GetMeasurement(measurementId);
        if (measurement is null)
        {
            return NotFoundError("measurement_not_found", $"Messung mit Id {measurementId} existiert nicht.");
        }

        return Ok(measurement.ToDto());
    }

    [HttpGet("measurements/{measurementId:int}/photos")]
    [ProducesResponseType(typeof(IReadOnlyList<PhotoAssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<PhotoAssetDto>> Photos(int measurementId)
    {
        var measurement = _repository.GetMeasurement(measurementId);
        if (measurement is null)
        {
            return NotFoundError("measurement_not_found", $"Messung mit Id {measurementId} existiert nicht.");
        }

        return Ok(_repository.GetPhotosForMeasurement(measurementId).Select(photo => photo.ToDto()).ToList());
    }

    [HttpPost("grows/{growId:int}/measurements")]
    [ProducesResponseType(typeof(MeasurementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<MeasurementDto> Create(int growId, [FromBody] MeasurementUpsertRequest request)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        Measurement measurement;
        try
        {
            measurement = request.ToFormModel(grow).ToMeasurement();
        }
        catch
        {
            ModelState.AddModelError(nameof(request.TakenAtLocal), "Datum oder Uhrzeit konnten nicht gelesen werden.");
            return ValidationError();
        }

        measurement.GrowId = growId;
        _measurementSanityService.ApplyBlockingValidation(ModelState, grow, measurement);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        measurement.Id = _repository.CreateMeasurement(measurement);
        _auditRepository.LogMeasurementCreated(growId, measurement.Id, measurement.Stage, measurement.TakenAt, measurement.Source);

        if (grow.Status == GrowStatus.Planning)
        {
            grow.Status = GrowStatus.Running;
            _repository.UpdateGrow(grow);
        }

        return CreatedAtAction(nameof(Detail), new { measurementId = measurement.Id }, measurement.ToDto());
    }

    [HttpPost("measurements/{measurementId:int}/photos")]
    [ProducesResponseType(typeof(IReadOnlyList<PhotoAssetDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<PhotoAssetDto>>> UploadPhotos(
        int measurementId,
        [FromForm] MeasurementPhotoUploadRequest request)
    {
        var measurement = _repository.GetMeasurement(measurementId);
        if (measurement is null)
        {
            return NotFoundError("measurement_not_found", $"Messung mit Id {measurementId} existiert nicht.");
        }

        var grow = _repository.GetGrow(measurement.GrowId);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {measurement.GrowId} existiert nicht.");
        }

        if (request.Photos.Count == 0)
        {
            ModelState.AddModelError(nameof(request.Photos), "Bitte waehle mindestens ein Foto aus.");
            return ValidationError();
        }

        _photoStorageService.ValidatePhotos(request.Photos, ModelState, nameof(request.Photos));
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        var created = await _photoStorageService.SaveMeasurementPhotosAsync(
            grow,
            measurementId,
            request.Photos,
            request.PhotoTag,
            request.PhotoCaption,
            request.UseAsReferenceShot,
            request.Source);

        _auditRepository.LogPhotosUploaded(grow.Id, measurementId, created.Count);

        return CreatedAtAction(nameof(Photos), new { measurementId }, created.Select(photo => photo.ToDto()).ToList());
    }

    [HttpPut("measurements/{measurementId:int}")]
    [ProducesResponseType(typeof(MeasurementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<MeasurementDto> Update(int measurementId, [FromBody] MeasurementUpsertRequest request)
    {
        var existingMeasurement = _repository.GetMeasurement(measurementId);
        if (existingMeasurement is null)
        {
            return NotFoundError("measurement_not_found", $"Messung mit Id {measurementId} existiert nicht.");
        }

        var grow = _repository.GetGrow(existingMeasurement.GrowId);
        if (grow is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {existingMeasurement.GrowId} existiert nicht.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        Measurement measurement;
        try
        {
            measurement = request.ToFormModel(grow).ToMeasurement();
        }
        catch
        {
            ModelState.AddModelError(nameof(request.TakenAtLocal), "Datum oder Uhrzeit konnten nicht gelesen werden.");
            return ValidationError();
        }

        measurement.Id = measurementId;
        measurement.GrowId = grow.Id;
        measurement.CreatedAtUtc = existingMeasurement.CreatedAtUtc;

        _measurementSanityService.ApplyBlockingValidation(ModelState, grow, measurement);
        if (!ModelState.IsValid)
        {
            return ValidationError();
        }

        _repository.UpdateMeasurement(measurement);
        _auditRepository.LogMeasurementUpdated(grow.Id, measurementId, measurement.TakenAt);

        return Ok(measurement.ToDto());
    }

    [HttpDelete("measurements/{measurementId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public IActionResult Delete(int measurementId)
    {
        var measurement = _repository.GetMeasurement(measurementId);
        if (measurement is null)
        {
            return NotFoundError("measurement_not_found", $"Messung mit Id {measurementId} existiert nicht.");
        }

        _repository.DeleteMeasurement(measurementId);
        _auditRepository.LogMeasurementDeleted(measurement.GrowId, measurementId, measurement.TakenAt);

        return NoContent();
    }
}
