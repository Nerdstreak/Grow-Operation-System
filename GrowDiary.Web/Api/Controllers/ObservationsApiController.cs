using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Der Einstieg in die Diagnose von der Pflanze her.
/// </summary>
/// <remarks>
/// Die Diagnose kannte bisher nur Zahlen. Dieser Endpunkt liefert den anderen
/// Weg: was man sieht — und was die Wissensbasis dazu schon weiss.
/// </remarks>
[ApiController]
[Route("api/observations")]
[Produces("application/json")]
public sealed class ObservationsApiController : ApiControllerBase
{
    private readonly BeobachtungsWegweiser _wegweiser;

    public ObservationsApiController(BeobachtungsWegweiser wegweiser)
    {
        _wegweiser = wegweiser;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(IReadOnlyList<Beobachtungsgruppe>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Beobachtungsgruppe>> Get() => Ok(_wegweiser.Gruppen());
}
