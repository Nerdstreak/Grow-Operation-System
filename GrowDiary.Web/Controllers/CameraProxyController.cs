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
    private readonly CameraFrameCache _cameraCache;

    public CameraProxyController(GrowRepository repository, HomeAssistantService homeAssistantService, CameraFrameCache cameraCache)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
        _cameraCache = cameraCache;
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

        var entityId = tent.CameraEntityId;
        var settings = _repository.GetEffectiveHomeAssistantSettings();

        // Try a fresh frame and only accept it if it is a real, valid image.
        if (settings.IsConfigured)
        {
            var snapshot = await _homeAssistantService.GetCameraSnapshotAsync(settings, entityId, cancellationToken);
            if (snapshot is { } value && CameraFrameCache.IsLikelyImage(value.Bytes, value.ContentType))
            {
                var fresh = new CameraFrame(value.Bytes, value.ContentType, DateTimeOffset.UtcNow);
                _cameraCache.Set(entityId, fresh);
                return ServeFrame(fresh, live: true);
            }
        }

        // Fresh fetch failed or returned an invalid image → fall back to the last
        // valid frame so the user always keeps a recent grow image.
        var cached = _cameraCache.Get(entityId);
        if (cached is not null)
        {
            return ServeFrame(cached, live: false);
        }

        // No valid frame has ever been captured for this camera.
        return settings.IsConfigured
            ? StatusCode(StatusCodes.Status502BadGateway, new CameraProxyStatusDto(false, "ha_camera_unavailable", "Home Assistant liefert für diese Kamera noch kein gültiges Bild.", entityId, null))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new CameraProxyStatusDto(false, "ha_not_configured", "Home Assistant ist nicht vollständig konfiguriert.", entityId, null));
    }

    private IActionResult ServeFrame(CameraFrame frame, bool live)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Camera-Captured-At"] = frame.CapturedAt.ToString("o");
        Response.Headers["X-Camera-Live"] = live ? "true" : "false";
        return File(frame.Bytes, frame.ContentType);
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

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return Ok(new CameraProxyStatusDto(false, "ha_not_configured", "Home Assistant ist nicht vollständig konfiguriert.", tent.CameraEntityId, null));
        }

        var snapshot = await _homeAssistantService.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
        var previewUrl = $"/api/live/tents/{tentId}/camera?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        if (snapshot is { } value && CameraFrameCache.IsLikelyImage(value.Bytes, value.ContentType))
        {
            _cameraCache.Set(tent.CameraEntityId, new CameraFrame(value.Bytes, value.ContentType, DateTimeOffset.UtcNow));
            return Ok(new CameraProxyStatusDto(true, "ok", "Kamera-Snapshot wurde erfolgreich geladen.", tent.CameraEntityId, previewUrl));
        }

        if (_cameraCache.Get(tent.CameraEntityId) is not null)
        {
            return Ok(new CameraProxyStatusDto(true, "stale", "Aktuell kein frisches Bild — letztes gültiges Bild wird angezeigt.", tent.CameraEntityId, previewUrl));
        }

        return Ok(new CameraProxyStatusDto(false, "ha_camera_unavailable", "Home Assistant liefert für diese Kamera kein gültiges Bild.", tent.CameraEntityId, null));
    }
}

public sealed record CameraProxyStatusDto(
    bool Ok,
    string Status,
    string Message,
    string? CameraEntityId,
    string? PreviewUrl);
