using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// One box that finds anything by name.
///
/// The app has grown past the point where a menu can stay browsable: twenty-odd
/// destinations plus every grow, tent, system, strain, SOP and knowledge entry. Regrouping
/// the sidebar helps until the next feature arrives; typing what you want does not decay.
/// </summary>
[ApiController]
[Route("api/search")]
[Produces("application/json")]
public sealed class SearchApiController : ApiControllerBase
{
    private const int PerKind = 5;

    private readonly GrowRepository _repository;
    private readonly KnowledgeBaseLoader _knowledge;

    public SearchApiController(GrowRepository repository, KnowledgeBaseLoader knowledge)
    {
        _repository = repository;
        _knowledge = knowledge;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SearchHitDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SearchHitDto>> Search([FromQuery] string? q)
    {
        var term = q?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            return Ok(Array.Empty<SearchHitDto>());
        }

        var hits = new List<SearchHitDto>();

        foreach (var grow in _repository.GetAllGrows().Where(grow => Matches(term, grow.Name, grow.Strain, grow.Breeder)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Grow", grow.Name, Join(grow.Strain, grow.TentName), $"/grows/{grow.Id}"));
        }

        foreach (var tent in _repository.GetTents().Where(tent => Matches(term, tent.Name)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Zelt", tent.Name, null, $"/zelte/{tent.Id}"));
        }

        foreach (var system in _repository.GetHydroSetups().Where(system => Matches(term, system.Name)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Hydro", system.Name, null, $"/hydro/{system.Id}"));
        }

        foreach (var strain in _repository.GetStrains().Where(strain => Matches(term, strain.Name, strain.Breeder)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Sorte", strain.Name, strain.Breeder, "/sorten"));
        }

        foreach (var sop in _knowledge.Sops.Where(sop => Matches(term, sop.Name)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("SOP", sop.Name, null, "/sops"));
        }

        // Knowledge is searched over the text as well as the title: people look for the
        // problem ("Wurzelfäule"), not for the filename.
        foreach (var rule in _knowledge.Guidance.Where(rule => Matches(term, rule.Title, rule.Rule)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Regel", rule.Title, rule.Sources.FirstOrDefault()?.Title, "/wissen"));
        }

        foreach (var treatment in _knowledge.Treatments.Where(item => Matches(term, item.Name)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Behandlung", treatment.Name, null, "/wissen"));
        }

        foreach (var pathogen in _knowledge.Pathogens.Where(item => Matches(term, item.Name)).Take(PerKind))
        {
            hits.Add(new SearchHitDto("Erreger", pathogen.Name, null, "/wissen"));
        }

        return Ok(hits);
    }

    private static bool Matches(string term, params string?[] fields) =>
        fields.Any(field => !string.IsNullOrWhiteSpace(field)
            && field.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string? Join(params string?[] parts)
    {
        var kept = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return kept.Count == 0 ? null : string.Join(" · ", kept);
    }
}
