using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Produces("application/json")]
public sealed class AlertsApiController : ApiControllerBase
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

        /* Ein vertauschtes Paar wird abgelehnt.
         *
         * `AlertEvaluationService.Decide` rechnet `wert < min ? unten : wert >
         * max ? oben : im Rahmen`. Bei min 22 / max 18 greift bei 20 °C die
         * erste Bedingung — die Regel meldet dauerhaft „zu kalt", obwohl 20
         * zwischen den beiden Zahlen liegt. Wer sich beim Eintippen vertut,
         * bekommt eine Warnung, die nie mehr aufhoert, und stellt am Ende die
         * Benachrichtigungen ab.
         *
         * Gefunden bei der Gesamtdurchsicht am 01.09.2026: der Endpunkt nahm
         * das Paar an und antwortete HTTP 200. */
        foreach (var regel in rules.Where(r => r.MinValue is { } min && r.MaxValue is { } max && min > max))
        {
            ModelState.AddModelError(nameof(AlertRuleDto.MinValue),
                $"Bei der Messgroesse {regel.MetricKey} liegt die Untergrenze "
                + $"({regel.MinValue}) ueber der Obergrenze ({regel.MaxValue}). "
                + "So gemeldet wuerde die Regel dauerhaft warnen.");
        }

        if (!ModelState.IsValid)
        {
            // ValidationError() und nicht ValidationProblem(): letzteres liefert
            // das ASP.NET-Standardformat ohne code/message/fieldErrors, und die
            // Oberflaeche liest nur payload.message — sie zeigte dafuer
            // "API request failed with status 400".
            return ValidationError("Die Grenzwerte lassen sich so nicht speichern.");
        }

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
