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
        "/api/alerts",
        "/api/notifications",
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
    /// Das interne Add-on-Netz von Home Assistant.
    /// </summary>
    /// <remarks>
    /// Der Supervisor legt dafür eine eigene Docker-Brücke an; darin steckt nur,
    /// was der Betreiber selbst als Add-on installiert hat. Von aussen ist das
    /// Netz nicht erreichbar — Grow OS veröffentlicht keinen Port.
    /// Quelle: Home-Assistant-Dokumentation zur Add-on-Kommunikation.
    /// </remarks>
    private static readonly (IPAddress Netz, int Bits)[] AddonNetworks =
    [
        (IPAddress.Parse("172.30.32.0"), 23),
        (IPAddress.Parse("fd0c:ac1e:2100::"), 48),
    ];

    /// <summary>
    /// Access is allowed for loopback requests and for requests proxied through the
    /// Home Assistant ingress (which Home Assistant has already authenticated).
    /// </summary>
    /// <remarks>
    /// Dazu kommt ein dritter Weg: <b>lesende</b> Anfragen aus dem internen
    /// Add-on-Netz. Ohne ihn könnte ein zweites Add-on — etwa der Grow-Berater —
    /// nichts abrufen, denn es ist weder Loopback noch Ingress. Bewusst nur
    /// lesend: mitlesen kann ein Nachbar-Add-on damit, schalten oder dosieren
    /// nicht. Für einen Schlüssel hat sich der Betreiber ausdrücklich nicht
    /// entschieden; das Netz enthält nur selbst installierte Software.
    /// </remarks>
    public static bool CanAccess(HttpContext context)
        => IsLocalRequest(context) || IsIngressRequest(context) || IsInternalAddonRead(context);

    /// <summary>Eine lesende Anfrage eines anderen Add-ons im internen Netz.</summary>
    public static bool IsInternalAddonRead(HttpContext context)
        => HttpMethods.IsGet(context.Request.Method)
           && context.Connection.RemoteIpAddress is { } ip
           && AddonNetworks.Any(bereich => IsInSubnet(ip, bereich.Netz, bereich.Bits));

    /// <summary>Liegt die Adresse im angegebenen Netz?</summary>
    private static bool IsInSubnet(IPAddress adresse, IPAddress netz, int bits)
    {
        // Eine per IPv4-mapped-IPv6 hereinkommende Adresse (::ffff:172.30.33.2)
        // sonst nie erkannt worden — Kestrel liefert die je nach Aufbau.
        if (adresse.IsIPv4MappedToIPv6) adresse = adresse.MapToIPv4();
        if (adresse.AddressFamily != netz.AddressFamily) return false;

        var links = adresse.GetAddressBytes();
        var rechts = netz.GetAddressBytes();
        if (links.Length != rechts.Length) return false;

        for (var i = 0; i < links.Length && bits > 0; i++, bits -= 8)
        {
            var maske = bits >= 8 ? (byte)0xFF : (byte)(0xFF << (8 - bits));
            if ((links[i] & maske) != (rechts[i] & maske)) return false;
        }

        return true;
    }

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
