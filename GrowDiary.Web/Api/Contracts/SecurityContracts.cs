namespace GrowDiary.Web.Api.Contracts;

public sealed record BackendSecurityStatusDto(
    string SecuritySchema,
    DateTime CheckedAtUtc,
    string AdminAccessMode,
    bool LocalOnlyAdminDefault,
    bool IngressTrusted,
    IReadOnlyList<string> ProtectedRoutePrefixes,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> SecretHandling);
