using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("network")]
    public ActionResult<NetworkOverviewDto> GetNetwork([FromQuery] string? frontendOrigin = null)
    {
        var requestOrigin = ResolveRequestOrigin(frontendOrigin, Request);
        var requestUri = TryCreateUri(requestOrigin);
        var preferredPort = requestUri?.Port is > 0 ? requestUri.Port : Request.Host.Port ?? 80;
        var preferredScheme = requestUri?.Scheme ?? Request.Scheme;

        var addresses = GetLanAddresses()
            .Select((address, index) =>
            {
                var url = BuildBaseUrl(preferredScheme, address.ToString(), preferredPort);
                return new NetworkAddressDto(
                    index == 0 ? "Empfohlen" : $"LAN {index + 1}",
                    address.ToString(),
                    url,
                    IPAddress.IsLoopback(address),
                    IsPrivateIpv4(address),
                    requestUri is not null && string.Equals(requestUri.Host, address.ToString(), StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        var browserIsLoopback = requestUri is null || IsLoopbackHost(requestUri.Host);
        var recommended = addresses.FirstOrDefault(address => address.IsPrivate)?.Url
            ?? addresses.FirstOrDefault(address => !address.IsLoopback)?.Url
            ?? requestOrigin;

        var warnings = new List<string>();
        if (browserIsLoopback && addresses.Count > 0)
        {
            warnings.Add("Du bist ueber localhost/127.0.0.1 verbunden. Fuer Handy/PWA wird automatisch die LAN-Adresse empfohlen.");
        }

        if (addresses.Count == 0)
        {
            warnings.Add("Es wurde keine private LAN-IPv4 gefunden. Pruefe WLAN/LAN, Firewall und ob der Vite-Server mit --host 0.0.0.0 laeuft.");
        }

        if (!browserIsLoopback && requestUri is not null && requestUri.HostNameType != UriHostNameType.Dns)
        {
            recommended = requestOrigin;
        }

        return Ok(new NetworkOverviewDto(
            requestOrigin,
            recommended,
            $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/'),
            addresses,
            warnings));
    }

    private static string ResolveRequestOrigin(string? frontendOrigin, HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(frontendOrigin) && Uri.TryCreate(frontendOrigin.Trim(), UriKind.Absolute, out var parsed))
        {
            return parsed.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        return $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
    }

    private static Uri? TryCreateUri(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;

    private static List<IPAddress> GetLanAddresses()
    {
        var addresses = new List<IPAddress>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                {
                    continue;
                }

                var bytes = address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    continue;
                }

                if (addresses.All(existing => !existing.Equals(address)))
                {
                    addresses.Add(address);
                }
            }
        }

        return addresses
            .OrderByDescending(IsPrivateIpv4)
            .ThenBy(address => address.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLoopbackHost(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
           || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivateIpv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static string BuildBaseUrl(string scheme, string host, int port)
    {
        var builder = new UriBuilder { Scheme = scheme, Host = host, Port = port };
        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}

public sealed record NetworkOverviewDto(string RequestOrigin, string RecommendedBaseUrl, string ApiBaseUrl, IReadOnlyList<NetworkAddressDto> LocalAddresses, IReadOnlyList<string> Warnings);
public sealed record NetworkAddressDto(string Label, string Host, string Url, bool IsLoopback, bool IsPrivate, bool IsCurrent);
