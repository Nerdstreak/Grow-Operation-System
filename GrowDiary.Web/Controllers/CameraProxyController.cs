using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class CameraProxyController : ControllerBase
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistantService;

    public CameraProxyController(GrowRepository repository, HomeAssistantService homeAssistantService)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
    }

    [HttpGet("live/tents/{tentId:int}/camera")]
    [HttpGet("camera/tents/{tentId:int}")]
    public async Task<IActionResult> GetTentCamera(int tentId, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(tentId);
        if (tent is null)
        {
            return NotFound(new CameraProxyStatusDto(false, "tent_not_found", "Zelt wurde nicht gefunden.", null, null));
        }

        if (string.IsNullOrWhiteSpace(tent.CameraEntityId))
        {
            return NotFound(new CameraProxyStatusDto(false, "camera_missing", "Für dieses Zelt ist keine Kamera-Entity hinterlegt.", null, null));
        }

        var settings = _repository.GetHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new CameraProxyStatusDto(false, "ha_not_configured", "Home Assistant ist nicht vollständig konfiguriert.", tent.CameraEntityId, null));
        }

        var snapshot = await _homeAssistantService.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
        if (snapshot is null || snapshot.Value.Bytes.Length == 0)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new CameraProxyStatusDto(false, "ha_camera_unavailable", "Home Assistant liefert für diese Kamera kein Bild.", tent.CameraEntityId, null));
        }

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        return File(snapshot.Value.Bytes, snapshot.Value.ContentType);
    }

    [HttpGet("camera/tents/{tentId:int}/status")]
    public async Task<ActionResult<CameraProxyStatusDto>> GetTentCameraStatus(int tentId, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(tentId);
        if (tent is null)
        {
            return NotFound(new CameraProxyStatusDto(false, "tent_not_found", "Zelt wurde nicht gefunden.", null, null));
        }

        if (string.IsNullOrWhiteSpace(tent.CameraEntityId))
        {
            return Ok(new CameraProxyStatusDto(false, "camera_missing", "Für dieses Zelt ist keine Kamera-Entity hinterlegt.", null, null));
        }

        var settings = _repository.GetHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return Ok(new CameraProxyStatusDto(false, "ha_not_configured", "Home Assistant ist nicht vollständig konfiguriert.", tent.CameraEntityId, null));
        }

        var snapshot = await _homeAssistantService.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
        if (snapshot is null || snapshot.Value.Bytes.Length == 0)
        {
            return Ok(new CameraProxyStatusDto(false, "ha_camera_unavailable", "Home Assistant liefert für diese Kamera kein Bild.", tent.CameraEntityId, null));
        }

        return Ok(new CameraProxyStatusDto(true, "ok", "Kamera-Snapshot wurde erfolgreich geladen.", tent.CameraEntityId, $"/api/live/tents/{tentId}/camera?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"));
    }
}

public sealed record CameraProxyStatusDto(
    bool Ok,
    string Status,
    string Message,
    string? CameraEntityId,
    string? PreviewUrl);
