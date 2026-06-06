using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Infrastructure;

public static class AdminAccessPolicy
{
    public const string AllowRemoteAdminEnvironmentVariable = "GROWDIARY_ALLOW_REMOTE_ADMIN";
    public const string AdminKeyEnvironmentVariable = "GROWDIARY_ADMIN_KEY";
    public const string AdminKeyHeaderName = "X-GrowOS-Admin-Key";

    // Unambiguous alphabet (no 0/o/1/l/i) for keys that are typed on a phone.
    private const string KeyAlphabet = "abcdefghkmnpqrstuvwxyz23456789";

    /// <summary>Returns the currently configured admin key (process environment), or null.</summary>
    public static string? CurrentAdminKey()
    {
        var value = Environment.GetEnvironmentVariable(AdminKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Generates a new random, human-typeable admin key.</summary>
    public static string GenerateKey(int length = 20)
    {
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(KeyAlphabet[RandomNumberGenerator.GetInt32(KeyAlphabet.Length)]);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Stores (or clears, when null/blank) the admin key. Applied to the current
    /// process immediately and persisted to the Windows user environment so it
    /// survives restarts.
    /// </summary>
    public static void StoreAdminKey(string? key)
    {
        var value = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        Environment.SetEnvironmentVariable(AdminKeyEnvironmentVariable, value, EnvironmentVariableTarget.Process);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                Environment.SetEnvironmentVariable(AdminKeyEnvironmentVariable, value, EnvironmentVariableTarget.User);
            }
            catch
            {
                // Persistence is best-effort; the process-level value still applies for this run.
            }
        }
    }

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
