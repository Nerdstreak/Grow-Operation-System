using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Die Nachtabsenkung eines Grows — Plan ansehen, ein- und ausschalten.
/// </summary>
/// <remarks>
/// Der Plan ist abrufbar, BEVOR etwas geschrieben wird. Eine Automatik, deren
/// Wirkung man erst am Chiller merkt, hat in einer Anlage nichts verloren.
/// </remarks>
[ApiController]
[Route("api/grows/{growId:int}/night-ramp")]
[Produces("application/json")]
public sealed class NightRampApiController : ApiControllerBase
{
    private readonly GrowRepository _grows;
    private readonly NachtabsenkungWriter _absenkung;

    public NightRampApiController(GrowRepository grows, NachtabsenkungWriter absenkung)
    {
        _grows = grows;
        _absenkung = absenkung;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(NightRampDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<NightRampDto> Get(int growId)
    {
        var grow = _grows.GetGrow(growId);
        if (grow is null) return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");

        return Ok(Bauen(grow, _absenkung.PlanFuer(grow, DateTime.Now, vorschau: true)));
    }

    [HttpPut("")]
    [ProducesResponseType(typeof(NightRampDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<NightRampDto> Put(int growId, [FromBody] NightRampRequest request)
    {
        var grow = _grows.GetGrow(growId);
        if (grow is null) return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");

        if (request.FloorC is { } boden && boden < NachtabsenkungService.AbsoluteUntergrenzeC)
        {
            return BadRequestError(
                "floor_too_low",
                $"Die Untergrenze darf nicht unter {NachtabsenkungService.AbsoluteUntergrenzeC:0.#} °C liegen — "
                    + "darunter schadet die Kühlung mehr, als der Stress bringt.");
        }

        // Das Zielgeraet haengt am Zelt, wird aber hier gepflegt: der Nutzer
        // trifft beide Entscheidungen an derselben Stelle, statt sie sich aus
        // zwei Formularen zusammenzusuchen.
        if (request.TargetEntityId is not null && grow.TentId is { } tentId)
        {
            var zelt = _grows.GetTents(includeArchived: true).FirstOrDefault(t => t.Id == tentId);
            if (zelt is not null)
            {
                zelt.WaterTargetEntityId = string.IsNullOrWhiteSpace(request.TargetEntityId)
                    ? null
                    : request.TargetEntityId.Trim();
                _grows.UpdateTent(zelt);
            }
        }

        grow.NightRampEnabled = request.Enabled;
        grow.NightRampFloorC = request.FloorC;
        _grows.UpdateGrow(grow);

        return Ok(Bauen(grow, _absenkung.PlanFuer(grow, DateTime.Now)));
    }

    private NightRampDto Bauen(GrowRun grow, Absenkplan plan)
    {
        var zelt = grow.TentId is { } tentId ? _grows.GetTents(includeArchived: true).FirstOrDefault(t => t.Id == tentId) : null;
        return new NightRampDto(
            grow.NightRampEnabled,
            grow.NightRampFloorC,
            NachtabsenkungService.AbsoluteUntergrenzeC,
            zelt?.WaterTargetEntityId,
            plan);
    }

    public sealed class NightRampRequest
    {
        public bool Enabled { get; set; }
        public double? FloorC { get; set; }

        /// <summary>Die HA-Entität, die den Sollwert annimmt; leer schaltet das Schreiben ab.</summary>
        public string? TargetEntityId { get; set; }
    }
}

/// <param name="TargetEntityId">Wohin geschrieben wird; null heisst: nur Plan, kein Eingriff.</param>
public sealed record NightRampDto(
    bool Enabled,
    double? FloorC,
    double HardFloorC,
    string? TargetEntityId,
    Absenkplan Plan);
