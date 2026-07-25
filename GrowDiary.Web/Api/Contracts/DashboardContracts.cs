namespace GrowDiary.Web.Api.Contracts;

public sealed record DashboardTileDto(
    string? Id,
    string? Kind,
    string? MetricKey,
    string? EntityId,
    string? Label,
    string? Unit);

public sealed record DashboardSectionDto(
    string? Id,
    string? Title,
    List<DashboardTileDto>? Tiles);

public sealed record DashboardLayoutDto(
    int TentId,
    List<DashboardSectionDto>? Sections);

/// <summary>The live reading of a Home Assistant entity a custom tile points at.</summary>
public sealed record DashboardEntityValueDto(
    string EntityId,
    string? FriendlyName,
    string? State,
    string? Unit);
