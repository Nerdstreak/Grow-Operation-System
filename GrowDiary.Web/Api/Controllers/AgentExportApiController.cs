using System.Text;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// Der Lagebericht zum Mitnehmen — für einen eigenen KI-Agenten.
/// </summary>
/// <remarks>
/// In der App selbst steckt keine KI, und das bleibt so. Wer trotzdem einen
/// Assistenten fragen will, bekommt hier eine Datei und entscheidet selbst, wem
/// er sie vorlegt. Das ist der ehrlichere Weg: Grow OS braucht keinen Schlüssel,
/// verschickt nichts, und was den Rechner verlässt, verlässt ihn, weil jemand
/// auf „Herunterladen" gedrückt hat.
/// </remarks>
[ApiController]
[Route("api/agent-export")]
public sealed class AgentExportApiController : ApiControllerBase
{
    private readonly AgentContextBuilder _builder;

    public AgentExportApiController(AgentContextBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>Der Lagebericht als Text — zum Ansehen in der App.</summary>
    [HttpGet("grows/{growId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public IActionResult Preview(int growId)
    {
        if (_builder.Build(growId, DateTime.UtcNow) is not { } context)
        {
            return NotFoundError("grow_not_found", $"Grow {growId} existiert nicht.");
        }

        return Ok(new { growName = context.GrowName, markdown = AgentContextBuilder.ToMarkdown(context) });
    }

    /// <summary>Dieselbe Datei zum Herunterladen.</summary>
    [HttpGet("grows/{growId:int}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public IActionResult Download(int growId)
    {
        if (_builder.Build(growId, DateTime.UtcNow) is not { } context)
        {
            return NotFoundError("grow_not_found", $"Grow {growId} existiert nicht.");
        }

        var markdown = AgentContextBuilder.ToMarkdown(context);
        var name = $"grow-os-lagebericht-{Dateiname(context.GrowName)}-{DateTime.Now:yyyy-MM-dd}.md";
        return File(Encoding.UTF8.GetBytes(markdown), "text/markdown; charset=utf-8", name);
    }

    /// <summary>
    /// Ein Grow-Name darf alles Mögliche enthalten — ein Dateiname nicht.
    /// </summary>
    private static string Dateiname(string name)
    {
        var sauber = new string(name
            .Select(zeichen => char.IsLetterOrDigit(zeichen) ? char.ToLowerInvariant(zeichen) : '-')
            .ToArray())
            .Trim('-');

        while (sauber.Contains("--")) sauber = sauber.Replace("--", "-");
        return string.IsNullOrEmpty(sauber) ? "grow" : sauber;
    }
}
