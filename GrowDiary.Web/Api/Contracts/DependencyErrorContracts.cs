using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Api.Contracts;

public sealed record DependencyItemDto(
    int Id,
    string Name,
    string? Status = null,
    string? Type = null);

public sealed record TentDependencySummaryDto(
    IReadOnlyList<DependencyItemDto> ActiveGrows,
    IReadOnlyList<DependencyItemDto> ArchivedGrows,
    IReadOnlyList<DependencyItemDto> HydroSetups,
    IReadOnlyList<DependencyItemDto> Sensors,
    IReadOnlyList<DependencyItemDto> Measurements,
    IReadOnlyList<DependencyItemDto> Other);

public sealed record TentDependencyError(
    string Code,
    string Message,
    TentDependencySummaryDto Dependencies,
    int? Status = StatusCodes.Status409Conflict,
    string? TraceId = null,
    string SchemaVersion = ApiErrorFactory.SchemaVersion);
