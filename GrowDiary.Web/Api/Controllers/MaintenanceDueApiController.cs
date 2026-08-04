using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Was gewartet, getauscht oder gesichert gehört — ohne dass man es von Hand eingetragen hat.
/// </summary>
/// <remarks>
/// Getrennt von <c>maintenance-events</c>: dort stehen Termine, die jemand
/// angelegt hat. Hier steht, was sich aus den Angaben am Gerät selbst ergibt —
/// genau die Termine, die bisher niemand las.
/// </remarks>
[ApiController]
[Route("api/maintenance-due")]
[Produces("application/json")]
public sealed class MaintenanceDueApiController : ApiControllerBase
{
    private readonly WartungDueService _wartung;

    public MaintenanceDueApiController(WartungDueService wartung)
    {
        _wartung = wartung;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(IReadOnlyList<WartungsPunkt>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WartungsPunkt>> Get()
        => Ok(_wartung.Offen(DateTime.UtcNow));
}
