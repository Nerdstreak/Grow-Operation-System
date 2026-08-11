using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Die Einkaufsliste aus den Materiallisten der Abläufe.
/// </summary>
/// <remarks>
/// Kein eigener Datenbestand: die Liste entsteht bei jedem Abruf aus dem
/// Wissen. Kommt ein Ablauf dazu, steht sein Material am nächsten Tag mit
/// drauf, ohne dass jemand etwas pflegen muss.
/// </remarks>
[ApiController]
[Route("api/shopping-list")]
[Produces("application/json")]
public sealed class ShoppingListApiController : ApiControllerBase
{
    private readonly EinkaufslisteService _liste;

    public ShoppingListApiController(EinkaufslisteService liste)
    {
        _liste = liste;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(IReadOnlyList<Einkaufsgruppe>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Einkaufsgruppe>> Get() => Ok(_liste.Bauen());
}
