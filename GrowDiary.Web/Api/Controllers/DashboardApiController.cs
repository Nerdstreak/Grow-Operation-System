using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// The tent's own dashboard arrangement, plus the live values for tiles that point at
/// arbitrary Home Assistant entities — the ones Grow OS has no built-in metric for.
/// </summary>
[ApiController]
[Route("api/tents")]
[Produces("application/json")]
public sealed class DashboardApiController : ApiControllerBase
{
    private readonly GrowRepository _repository;
    private readonly DashboardLayoutRepository _layouts;
    private readonly HomeAssistantService _homeAssistant;

    public DashboardApiController(
        GrowRepository repository,
        DashboardLayoutRepository layouts,
        HomeAssistantService homeAssistant)
    {
        _repository = repository;
        _layouts = layouts;
        _homeAssistant = homeAssistant;
    }

    [HttpGet("{tentId:int}/dashboard")]
    [ProducesResponseType(typeof(DashboardLayoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<DashboardLayoutDto> Get(int tentId)
    {
        if (_repository.GetTent(tentId) is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {tentId} existiert nicht.");
        }

        return Ok(ToDto(_layouts.Get(tentId)));
    }

    [HttpPut("{tentId:int}/dashboard")]
    [ProducesResponseType(typeof(DashboardLayoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<DashboardLayoutDto> Save(int tentId, [FromBody] DashboardLayoutDto request)
    {
        if (_repository.GetTent(tentId) is null)
        {
            return NotFoundError("tent_not_found", $"Zelt mit Id {tentId} existiert nicht.");
        }

        var layout = new DashboardLayout
        {
            TentId = tentId,
            Sections = (request.Sections ?? [])
                .Select(section => new DashboardSection
                {
                    Id = string.IsNullOrWhiteSpace(section.Id) ? Guid.NewGuid().ToString("N")[..8] : section.Id,
                    Title = string.IsNullOrWhiteSpace(section.Title) ? "Bereich" : section.Title.Trim(),
                    Tiles = (section.Tiles ?? [])
                        .Where(tile => !string.IsNullOrWhiteSpace(tile.MetricKey) || !string.IsNullOrWhiteSpace(tile.EntityId))
                        .Select(tile => new DashboardTile
                        {
                            Id = string.IsNullOrWhiteSpace(tile.Id) ? Guid.NewGuid().ToString("N")[..8] : tile.Id,
                            Kind = Enum.TryParse<DashboardTileKind>(tile.Kind, ignoreCase: true, out var kind)
                                ? kind
                                : DashboardTileKind.Metric,
                            MetricKey = string.IsNullOrWhiteSpace(tile.MetricKey) ? null : tile.MetricKey.Trim(),
                            EntityId = string.IsNullOrWhiteSpace(tile.EntityId) ? null : tile.EntityId.Trim(),
                            Label = string.IsNullOrWhiteSpace(tile.Label) ? null : tile.Label.Trim(),
                            Unit = string.IsNullOrWhiteSpace(tile.Unit) ? null : tile.Unit.Trim(),
                            Span = Math.Clamp(tile.Span ?? 1, 1, 3),
                        })
                        .ToList(),
                })
                .ToList(),
        };

        _layouts.Save(layout);
        return Ok(ToDto(_layouts.Get(tentId)));
    }

    /// <summary>Restores the built-in arrangement.</summary>
    [HttpDelete("{tentId:int}/dashboard")]
    [ProducesResponseType(typeof(DashboardLayoutDto), StatusCodes.Status200OK)]
    public ActionResult<DashboardLayoutDto> Reset(int tentId)
    {
        _layouts.Reset(tentId);
        return Ok(ToDto(DashboardLayout.Default(tentId)));
    }

    /// <summary>
    /// Current values for the layout's entity tiles. Grow OS's own metrics already come
    /// with the live payload; this fills in the sensors it doesn't know about.
    /// </summary>
    [HttpGet("{tentId:int}/dashboard/values")]
    [ProducesResponseType(typeof(IReadOnlyList<DashboardEntityValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DashboardEntityValueDto>>> Values(int tentId, CancellationToken cancellationToken)
    {
        var wanted = _layouts.Get(tentId).Sections
            .SelectMany(section => section.Tiles)
            .Where(tile => tile.Kind == DashboardTileKind.Entity && !string.IsNullOrWhiteSpace(tile.EntityId))
            .Select(tile => tile.EntityId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (wanted.Count == 0)
        {
            return Ok(Array.Empty<DashboardEntityValueDto>());
        }

        var settings = _repository.GetEffectiveHomeAssistantSettings();
        if (!settings.IsConfigured)
        {
            return Ok(Array.Empty<DashboardEntityValueDto>());
        }

        var entities = await _homeAssistant.GetEntitiesAsync(settings, cancellationToken);
        var values = entities
            .Where(entity => wanted.Contains(entity.EntityId))
            .Select(entity => new DashboardEntityValueDto(entity.EntityId, entity.FriendlyName, entity.State, entity.UnitOfMeasurement))
            .ToList();

        return Ok(values);
    }

    private static DashboardLayoutDto ToDto(DashboardLayout layout) => new(
        layout.TentId,
        layout.Sections.Select(section => new DashboardSectionDto(
            section.Id,
            section.Title,
            section.Tiles.Select(tile => new DashboardTileDto(
                tile.Id, tile.Kind.ToString(), tile.MetricKey, tile.EntityId, tile.Label, tile.Unit, tile.Span)).ToList()
        )).ToList());
}
