using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Der Versuchsaufbau „Zelt (AC-Test)" — Geräte eintragen und ihre Stufe stellen.
/// </summary>
/// <remarks>
/// Siehe <see cref="AcTest"/> für das Warum. Kurz: es geht um die Frage, ob
/// Grow OS die Zentrale sein kann, von der aus der ganze Grow läuft — und die
/// beantwortet ein Nutzer, kein Entwurf.
/// </remarks>
[ApiController]
[Route("api/ac-test")]
public sealed class AcTestApiController : ControllerBase
{
    private readonly GrowRepository _grows;
    private readonly AppSettingsRepository _einstellungen;
    private readonly HomeAssistantService _homeAssistant;
    private readonly SystemAuditRepository _protokoll;
    private readonly ILogger<AcTestApiController> _logger;

    public AcTestApiController(
        GrowRepository grows,
        AppSettingsRepository einstellungen,
        HomeAssistantService homeAssistant,
        SystemAuditRepository protokoll,
        ILogger<AcTestApiController> logger)
    {
        _grows = grows;
        _einstellungen = einstellungen;
        _homeAssistant = homeAssistant;
        _protokoll = protokoll;
        _logger = logger;
    }

    /// <summary>Die eingetragenen Geräte samt aktueller Stufe.</summary>
    [HttpGet("{zeltId:int}")]
    public async Task<ActionResult<AcTestStand>> Stand(int zeltId, CancellationToken ct)
    {
        if (_grows.GetTent(zeltId) is null) return NotFound();

        var einstellungen = _grows.GetEffectiveHomeAssistantSettings();
        var geraete = AcTest.Lesen(_einstellungen, zeltId);
        var stand = new List<AcGeraetStand>();

        foreach (var geraet in geraete)
        {
            // Jede Entität EINZELN holen: das Wörterbuch aus GetStatesAsync
            // kennt nur Metrik-Kennungen, nie Entitäts-Kennungen. Genau daran
            // ist der Kühler-Regler schon einmal gescheitert.
            var leistung = await _homeAssistant.GetEntityStateAsync(einstellungen, geraet.LeistungEntityId, ct);
            var modus = string.IsNullOrWhiteSpace(geraet.ModusEntityId)
                ? null
                : await _homeAssistant.GetEntityStateAsync(einstellungen, geraet.ModusEntityId, ct);

            stand.Add(new AcGeraetStand(
                geraet,
                leistung?.NumericValue,
                modus?.State,
                leistung is null
                    ? $"{geraet.LeistungEntityId} antwortet nicht — gibt es die Entität?"
                    : null));
        }

        return Ok(new AcTestStand(zeltId, stand, einstellungen.IsConfigured, DemoData.IsEnabled));
    }

    /// <summary>Die Geräte eintragen oder ändern.</summary>
    [HttpPut("{zeltId:int}")]
    public ActionResult<IReadOnlyList<string>> Speichern(
        int zeltId, [FromBody] List<AcGeraet> geraete)
    {
        if (_grows.GetTent(zeltId) is null) return NotFound();

        var maengel = AcTest.Speichern(_einstellungen, zeltId, geraete);
        if (maengel.Count > 0) return BadRequest(maengel);

        return Ok(Array.Empty<string>());
    }

    /// <summary>Eine Stufe stellen — der eigentliche Versuch.</summary>
    /// <remarks>
    /// <b>Nur auf Klick.</b> Hier läuft kein Hintergrunddienst und nichts regelt
    /// sich von selbst. Jeder Aufruf steht im Anlagen-Protokoll, auch der
    /// erfolgreiche: bei einem Versuch will man hinterher wissen, wer wann was
    /// gestellt hat.
    /// </remarks>
    [HttpPost("{zeltId:int}/stufe")]
    public async Task<IActionResult> Stufe(
        int zeltId, [FromBody] StufeRequest request, CancellationToken ct)
    {
        var zelt = _grows.GetTent(zeltId);
        if (zelt is null) return NotFound();

        var geraet = AcTest.Lesen(_einstellungen, zeltId)
            .FirstOrDefault(g => g.LeistungEntityId == request.EntityId);
        if (geraet is null)
        {
            // Kein blindes Schreiben auf eine beliebige Entität: gestellt wird
            // nur, was der Nutzer vorher hier eingetragen hat.
            return BadRequest(new[] { $"{request.EntityId} ist für dieses Zelt nicht eingetragen." });
        }

        if (!AcTest.StufeErlaubt(request.Stufe))
        {
            return BadRequest(new[]
            {
                $"Stufe {request.Stufe} liegt ausserhalb von {AcTest.StufeMin}–{AcTest.StufeMax}.",
            });
        }

        var einstellungen = _grows.GetEffectiveHomeAssistantSettings();
        var domain = geraet.LeistungEntityId.Split('.', 2)[0];

        var ok = await _homeAssistant.CallEntityServiceAsync(
            einstellungen, domain, "set_value", geraet.LeistungEntityId, ct,
            new Dictionary<string, object> { ["value"] = request.Stufe });

        _protokoll.Add(new SystemAuditEvent
        {
            EventType = AcTest.ProtokollTyp,
            Action = ok ? "stufe-gesetzt" : "stufe-fehlgeschlagen",
            Summary = $"{zelt.Name} · {geraet.Name}: Stufe {request.Stufe:0.#} "
                + $"an {geraet.LeistungEntityId}.",
            Severity = ok ? "info" : "warning",
            Success = ok,
        });

        if (!ok)
        {
            _logger.LogWarning(
                "AC-Test: {Entity} liess sich nicht auf {Stufe} stellen.",
                geraet.LeistungEntityId, request.Stufe);
            return StatusCode(502, new[]
            {
                $"Home Assistant hat {geraet.LeistungEntityId} nicht gestellt. "
                + "Steht die Verbindung, und gibt es die Entität?",
            });
        }

        return NoContent();
    }

    public sealed class StufeRequest
    {
        public string EntityId { get; set; } = string.Empty;
        public double Stufe { get; set; }
    }
}
