using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[ApiController]
[Route("api/camera")]
public sealed class CameraProxyController : ControllerBase
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistant;

    public CameraProxyController(GrowRepository repository, HomeAssistantService homeAssistant)
    {
        _repository = repository;
        _homeAssistant = homeAssistant;
    }

    [HttpGet("tents/{tentId:int}")]
    [HttpGet("/api/live/tents/{tentId:int}/camera")]
    public async Task<IActionResult> GetTentCamera(int tentId, CancellationToken cancellationToken)
    {
        var tent = _repository.GetTent(tentId);
        if (tent is null)
        {
            return NotFound(new { code = "tent_not_found", message = "Zelt wurde nicht gefunden." });
        }

        if (string.IsNullOrWhiteSpace(tent.CameraEntityId))
        {
            return NotFound(new { code = "camera_not_configured", message = "Für dieses Zelt ist keine Kamera-Entity hinterlegt." });
        }

        var settings = _repository.GetHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "home_assistant_not_configured", message = "Home Assistant ist nicht vollständig konfiguriert." });
        }

        var snapshot = await _homeAssistant.GetCameraSnapshotAsync(settings, tent.CameraEntityId, cancellationToken);
        if (snapshot is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { code = "camera_snapshot_unavailable", message = "Kamera-Snapshot konnte nicht von Home Assistant geladen werden." });
        }

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return File(snapshot.Value.Bytes, snapshot.Value.ContentType);
    }
}
