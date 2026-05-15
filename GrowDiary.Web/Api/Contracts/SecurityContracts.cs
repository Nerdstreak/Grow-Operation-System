namespace GrowDiary.Web.Api.Contracts;

public sealed record BackendSecurityStatusDto(
    string SecuritySchema,
    DateTime CheckedAtUtc,
    string AdminAccessMode,
    bool LocalOnlyAdminDefault,
    bool RemoteAdminExplicitlyAllowed,
    bool AdminKeyConfigured,
    bool AdminKeyRequiredForRemoteAdmin,
    bool InsecureRemoteAdminOverrideActive,
    string AdminKeyHeaderName,
    IReadOnlyList<string> ProtectedRoutePrefixes,
    IReadOnlyList<string> RemoteAccessWarnings,
    IReadOnlyList<string> RecommendedRemoteAccessModes,
    IReadOnlyList<string> SecretHandling);
