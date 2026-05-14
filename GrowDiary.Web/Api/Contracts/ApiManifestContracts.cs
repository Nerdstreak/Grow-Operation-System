namespace GrowDiary.Web.Api.Contracts;

public sealed record ApiManifestDto(
    string SchemaVersion,
    string BackendSchema,
    DateTime GeneratedAtUtc,
    IReadOnlyList<string> GlobalRules,
    IReadOnlyList<ApiAreaDto> Areas);

public sealed record ApiAreaDto(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<ApiEndpointDto> Endpoints);

public sealed record ApiEndpointDto(
    string Method,
    string Path,
    string Purpose,
    bool LocalAdminOnly,
    IReadOnlyList<string> Rules);
