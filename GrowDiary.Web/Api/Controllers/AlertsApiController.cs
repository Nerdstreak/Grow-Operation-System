using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Produces("application/json")]
public sealed class AlertsApiController : ControllerBase
{
    private readonly GrowRepository _repository;
    private readonly AlertRuleRepository _alertRules;
    private readonly HomeAssistantService _homeAssistant;

    public AlertsApiController(
        GrowRepository repository,
        AlertRuleRepository alertRules,
        HomeAssistantService homeAssistant)
    {
        _repository = repository;
        _alertRules = alertRules;
        _homeAssistant = homeAssistant;
    }

    /// <summary>Lists the Home Assistant notify services the user can push alerts to.</summary>
    [HttpGet("notify-services")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> NotifyServices(CancellationToken cancellationToken)
    {
        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var services = await _homeAssistant.GetNotifyServicesAsync(settings, cancellationToken);
        return Ok(services);
    }

    /// <summary>Returns the alert rules configured for a tent.</summary>
    [HttpGet("tents/{tentId:int}")]
    [ProducesResponseType(typeof(TentAlertRulesDto), StatusCodes.Status200OK)]
    public ActionResult<TentAlertRulesDto> GetForTent(int tentId)
    {
        if (!TentExists(tentId))
        {
            return NotFound();
        }

        var rules = _alertRules.GetForTent(tentId)
            .Select(rule => new AlertRuleDto(rule.MetricKey, rule.MinValue, rule.MaxValue, rule.NotifyService, rule.Enabled, rule.CooldownMinutes))
            .ToList();

        return Ok(new TentAlertRulesDto(tentId, rules));
    }

    /// <summary>Replaces a tent's alert rules with the submitted set.</summary>
    [HttpPut("tents/{tentId:int}")]
    [ProducesResponseType(typeof(TentAlertRulesDto), StatusCodes.Status200OK)]
    public ActionResult<TentAlertRulesDto> SaveForTent(int tentId, [FromBody] SaveTentAlertRulesRequest request)
    {
        if (!TentExists(tentId))
        {
            return NotFound();
        }

        // The notify target is configured centrally (Notification Center), so a rule only needs
        // a metric and at least one bound. NotifyService is kept for schema compatibility.
        var rules = (request.Rules ?? Array.Empty<AlertRuleDto>())
            .Where(dto => !string.IsNullOrWhiteSpace(dto.MetricKey)
                          && (dto.MinValue.HasValue || dto.MaxValue.HasValue))
            .Select(dto => new TentAlertRule
            {
                TentId = tentId,
                MetricKey = dto.MetricKey.Trim(),
                MinValue = dto.MinValue,
                MaxValue = dto.MaxValue,
                NotifyService = dto.NotifyService?.Trim() ?? string.Empty,
                Enabled = dto.Enabled,
                CooldownMinutes = dto.CooldownMinutes <= 0 ? 30 : dto.CooldownMinutes,
            })
            .ToList();

        _alertRules.ReplaceForTent(tentId, rules);

        var saved = rules
            .Select(rule => new AlertRuleDto(rule.MetricKey, rule.MinValue, rule.MaxValue, rule.NotifyService, rule.Enabled, rule.CooldownMinutes))
            .ToList();

        return Ok(new TentAlertRulesDto(tentId, saved));
    }

    /// <summary>Sends a test push so the user can confirm the notify service works.</summary>
    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Test([FromBody] AlertTestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NotifyService))
        {
            return BadRequest(new { ok = false, message = "Kein Notify-Dienst angegeben." });
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        var sent = await _homeAssistant.SendNotificationAsync(
            settings,
            request.NotifyService,
            "🌱 Grow OS",
            "Test-Benachrichtigung — die Grenzwert-Alarme sind richtig eingerichtet.",
            cancellationToken);

        return Ok(new { ok = sent });
    }

    private bool TentExists(int tentId)
        => _repository.GetTents(includeArchived: true).Any(tent => tent.Id == tentId);
}
