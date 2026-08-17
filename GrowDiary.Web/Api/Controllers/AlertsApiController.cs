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
    private readonly AlertEvaluationService _alertEval;

    public AlertsApiController(
        GrowRepository repository,
        AlertRuleRepository alertRules,
        HomeAssistantService homeAssistant,
        AlertEvaluationService alertEval)
    {
        _repository = repository;
        _alertRules = alertRules;
        _homeAssistant = homeAssistant;
        _alertEval = alertEval;
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
    public async Task<ActionResult<TentAlertRulesDto>> SaveForTent(int tentId, [FromBody] SaveTentAlertRulesRequest request, CancellationToken cancellationToken)
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

        // Immediate feedback: evaluate the freshly saved rules against current values right
        // away, so if something is already out of range the user gets a push now instead of
        // waiting for the next check. Fresh rules have no notify state, so this fires at once.
        var tent = _repository.GetTents(includeArchived: true).FirstOrDefault(item => item.Id == tentId);
        var haSettings = _repository.GetEffectiveHomeAssistantSettings();
        if (tent is not null && haSettings.IsConfigured)
        {
            try
            {
                var states = await _homeAssistant.GetStatesAsync(haSettings, tent, cancellationToken);
                await _alertEval.EvaluateAsync(tent, states, cancellationToken);
            }
            catch
            {
                // Saving must never fail just because HA is momentarily unreachable — the
                // per-minute AlertWatchWorker will evaluate the new rules shortly anyway.
            }
        }

        var saved = rules
            .Select(rule => new AlertRuleDto(rule.MetricKey, rule.MinValue, rule.MaxValue, rule.NotifyService, rule.Enabled, rule.CooldownMinutes))
            .ToList();

        return Ok(new TentAlertRulesDto(tentId, saved));
    }

    private bool TentExists(int tentId)
        => _repository.GetTents(includeArchived: true).Any(tent => tent.Id == tentId);
}
