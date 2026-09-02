using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Die Chronik eines Grows: was wann geändert wurde.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>AuditEntries</c> war
/// <b>schreib-only</b>. Vier Controller schrieben hinein — Grows, Messungen,
/// Journal, Abläufe —, es gab einen Index für eine Abfrage, die niemand
/// stellte, und keinen Weg, an die Zeilen zu kommen.</para>
///
/// <para>Die App sammelte damit seit Monaten die Geschichte jedes Grows, ohne
/// dass jemand herankam. Genau diese Zeilen beantworten „wann habe ich
/// eigentlich geflippt" und „wann wurde dieser Wert geändert", wenn jemand
/// hinterher sucht.</para>
///
/// <para><b>Warum es dafür keinen Knopf gibt.</b> Man liest eine Chronik nicht
/// täglich, sondern wenn etwas passiert ist — wie
/// <c>GET /api/system/audit-events</c>. Ob und wie sie auf einer Seite
/// erscheinen soll, ist eine Gestaltungsfrage; dass die gesammelten Daten
/// erreichbar sind, ist keine.</para>
/// </remarks>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class GrowChronikApiController : ApiControllerBase
{
    private readonly GrowRepository _grows;
    private readonly AuditRepository _chronik;

    public GrowChronikApiController(GrowRepository grows, AuditRepository chronik)
    {
        _grows = grows;
        _chronik = chronik;
    }

    /// <summary>Die letzten Änderungen an diesem Grow, das Neueste zuerst.</summary>
    [HttpGet("grows/{growId:int}/chronik")]
    [ProducesResponseType(typeof(IReadOnlyList<ChronikEintragDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<ChronikEintragDto>> Chronik(int growId, int limit = 200)
    {
        if (_grows.GetGrow(growId) is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        return Ok(_chronik.GetForGrow(growId, limit)
            .Select(eintrag => new ChronikEintragDto(
                eintrag.Id,
                eintrag.CreatedAtUtc,
                eintrag.EntityType,
                eintrag.EntityId,
                eintrag.Action,
                eintrag.Summary))
            .ToList());
    }
}

/// <summary>Eine Zeile der Grow-Chronik.</summary>
/// <param name="Id">Die Kennung des Eintrags.</param>
/// <param name="GeschehenUtc">Wann es passiert ist.</param>
/// <param name="Gegenstand">Woran — „Grow", „Measurement", „JournalEntry".</param>
/// <param name="GegenstandId">Welches genau, sofern es eine Kennung gibt.</param>
/// <param name="Handlung">Was getan wurde, kurz.</param>
/// <param name="Beschreibung">Derselbe Vorgang in einem Satz.</param>
public sealed record ChronikEintragDto(
    int Id,
    DateTime GeschehenUtc,
    string Gegenstand,
    int? GegenstandId,
    string Handlung,
    string Beschreibung);
