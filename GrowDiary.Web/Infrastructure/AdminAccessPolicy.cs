using System.Net;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Infrastructure;

public static class AdminAccessPolicy
{
    public const string AllowRemoteAdminEnvironmentVariable = "GROWDIARY_ALLOW_REMOTE_ADMIN";
    public const string AdminKeyEnvironmentVariable = "GROWDIARY_ADMIN_KEY";
    public const string AdminKeyHeaderName = "X-GrowOS-Admin-Key";

    private static readonly string[] ProtectedPrefixes =
    {
        "/settings",
        "/einstellungen",
        "/api/settings",
        "/api/system/backup",
        "/api/system/release-readiness",
        "/api/system/database-status",
        "/api/system/api-manifest",
        "/api/system/security-status",
        "/api/system/audit-events",
        "/api/system/error-contract",
        "/api/system/migration-status",
        "/api/system/upgrade-preflight",
        "/api/exports"
    };

    private static readonly string[] ProtectedLegacyCameraSuffixes =
    {
        "/camera.jpg",
        "/camera-stream",
        "/latest-snapshot"
    };

    public static IReadOnlyList<string> ProtectedRoutePrefixes => ProtectedPrefixes;

    public static bool IsProtectedPath(PathString path)
    {
        if (ProtectedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IsProtectedLegacyTentCameraPath(path);
    }

    private static bool IsProtectedLegacyTentCameraPath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/tents/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ProtectedLegacyCameraSuffixes.Any(suffix => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanAccess(HttpContext context)
    {
        if (IsLocalRequest(context))
        {
            return true;
        }

        if (IsAdminKeyConfigured() && HasValidAdminKey(context))
        {
            return true;
        }

        return IsRemoteAdminExplicitlyAllowed();
    }

    public static bool IsRemoteAdminExplicitlyAllowed()
        => string.Equals(
            Environment.GetEnvironmentVariable(AllowRemoteAdminEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool IsAdminKeyConfigured()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable));

    public static bool IsInsecureRemoteAdminOverrideActive()
        => IsRemoteAdminExplicitlyAllowed() && !IsAdminKeyConfigured();

    public static bool IsLocalRequest(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        var localIp = context.Connection.LocalIpAddress;
        if (remoteIp is null)
        {
            return false;
        }

        return IPAddress.IsLoopback(remoteIp)
               || (localIp is not null && remoteIp.Equals(localIp));
    }

    private static bool HasValidAdminKey(HttpContext context)
    {
        var expected = Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue(AdminKeyHeaderName, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(provided)
               && string.Equals(provided, expected, StringComparison.Ordinal);
    }
}
