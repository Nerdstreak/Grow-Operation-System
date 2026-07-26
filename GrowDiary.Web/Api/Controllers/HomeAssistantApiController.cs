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
    /// Whether Home Assistant is set up and currently answering.
    ///
    /// Deliberately no extra request: the service already stops calling after
    /// repeated failures, and that state is the honest answer. The UI shows one
    /// banner from this instead of leaving empty fields all over the page.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HomeAssistantHealthDto), StatusCodes.Status200OK)]
    public ActionResult<HomeAssistantHealthDto> Health()
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var unreachableUntil = _homeAssistantService.UnreachableUntilUtc;

        return Ok(new HomeAssistantHealthDto(
            Configured: settings.IsConfigured,
            Reachable: settings.IsConfigured && unreachableUntil is null,
            RetryAtUtc: unreachableUntil));
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

/// <param name="Configured">Base URL and token are set and the integration is on.</param>
/// <param name="Reachable">Calls are currently going through.</param>
/// <param name="RetryAtUtc">When the next attempt happens; null while everything works.</param>
public sealed record HomeAssistantHealthDto(bool Configured, bool Reachable, DateTime? RetryAtUtc);
