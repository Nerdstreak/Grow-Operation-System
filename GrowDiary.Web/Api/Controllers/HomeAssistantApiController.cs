using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/home-assistant")]
[Produces("application/json")]
public sealed class HomeAssistantApiController : ControllerBase
{
    private readonly GrowRepository _repository;
    private readonly HomeAssistantService _homeAssistantService;

    public HomeAssistantApiController(GrowRepository repository, HomeAssistantService homeAssistantService)
    {
        _repository = repository;
        _homeAssistantService = homeAssistantService;
    }

    /// <summary>
    /// Lists Home Assistant entities for the sensor picker so the user selects from
    /// a searchable dropdown instead of typing entity IDs. Optional query filters:
    /// <c>domain</c> (e.g. "sensor") and <c>deviceClass</c> (e.g. "temperature").
    /// Uses the effective connection, so inside the add-on this needs no HA setup.
    /// </summary>
    [HttpGet("entities")]
    [ProducesResponseType(typeof(IReadOnlyList<HomeAssistantEntity>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HomeAssistantEntity>>> Entities(
        [FromQuery] string? domain,
        [FromQuery] string? deviceClass,
        CancellationToken cancellationToken)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var entities = await _homeAssistantService.GetEntitiesAsync(settings, cancellationToken);

        IEnumerable<HomeAssistantEntity> filtered = entities;
        if (!string.IsNullOrWhiteSpace(domain))
        {
            filtered = filtered.Where(entity => string.Equals(entity.Domain, domain, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(deviceClass))
        {
            filtered = filtered.Where(entity => string.Equals(entity.DeviceClass, deviceClass, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(filtered
            .OrderBy(entity => entity.FriendlyName ?? entity.EntityId, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }
}
