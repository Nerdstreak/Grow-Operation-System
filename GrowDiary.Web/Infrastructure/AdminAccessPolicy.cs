using System.Net;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Infrastructure;

/// <summary>
/// Gates administrative and product API routes. Grow OS runs as a Home Assistant
/// add-on: the add-on port is ingress-only (never published to the network), so all
/// real traffic arrives either from loopback or through the Home Assistant ingress
/// proxy, which has already authenticated the user. Any other (direct, non-ingress
/// remote) request to a protected route is refused as defense-in-depth.
/// </summary>
public static class AdminAccessPolicy
{
    // Home Assistant's ingress proxy sets this header on every request it forwards.
    // Its presence means Home Assistant has already authenticated the user.
    public const string IngressPathHeaderName = "X-Ingress-Path";

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
        "/api/system/migration-plan",
        "/api/system/upgrade-preflight",
        "/api/exports"
    };

    private static readonly string[] ProtectedProductApiPrefixes =
    {
        "/api/auto-measurements",
        "/api/calibration-events",
        "/api/grows",
        "/api/hardware-items",
        "/api/hydro-setups",
        "/api/journal",
        "/api/home-assistant",
        "/api/knowledge",
        "/api/light-schedules",
        "/api/light-transitions",
        "/api/maintenance-events",
        "/api/measurements",
        "/api/plants",
        "/api/risk-events",
        "/api/setups",
        "/api/sop-instances",
        "/api/strains",
        "/api/tasks"
    };

    private static readonly string[] ProtectedLegacyCameraSuffixes =
    {
        "/camera.jpg",
        "/camera-stream",
        "/latest-snapshot"
    };

    public static IReadOnlyList<string> ProtectedRoutePrefixes => ProtectedPrefixes.Concat(ProtectedProductApiPrefixes).ToArray();

    public static IReadOnlyList<string> ProtectedProductApiRoutePrefixes => ProtectedProductApiPrefixes;

    public static bool IsProtectedPath(PathString path)
    {
        if (ProtectedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (ProtectedProductApiPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
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

    /// <summary>
    /// Access is allowed for loopback requests and for requests proxied through the
    /// Home Assistant ingress (which Home Assistant has already authenticated).
    /// </summary>
    public static bool CanAccess(HttpContext context)
        => IsLocalRequest(context) || IsIngressRequest(context);

    /// <summary>True when the request is proxied through the Home Assistant ingress.</summary>
    public static bool IsIngressRequest(HttpContext context)
        => context.Request.Headers.ContainsKey(IngressPathHeaderName);

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
}
