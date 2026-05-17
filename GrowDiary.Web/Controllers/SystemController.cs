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
        var requestOrigin = ResolveRequestOrigin(frontendOrigin);
        var uri = Uri.TryCreate(requestOrigin, UriKind.Absolute, out var parsed)
            ? parsed
            : new Uri($"{Request.Scheme}://{Request.Host}");

        var scheme = uri.Scheme;
        var portPart = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var addresses = GetLocalIPv4Addresses()
            .Select(address => new NetworkAddressDto(
                Label: Classify(address),
                Host: address.ToString(),
                Url: $"{scheme}://{address}{portPart}",
                IsLoopback: IPAddress.IsLoopback(address),
                IsPrivate: IsPrivate(address),
                IsCurrent: string.Equals(uri.Host, address.ToString(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var warnings = new List<string>();
        if (IsLoopbackHost(uri.Host))
        {
            warnings.Add("Die aktuell erkannte Adresse ist localhost/Loopback. Für Handy/PWA im gleichen WLAN wird eine LAN-IP benötigt.");
        }

        if (addresses.Count == 0)
        {
            warnings.Add("Es wurde keine aktive IPv4-LAN-Adresse gefunden. Prüfe Netzwerkadapter, WLAN/LAN und Firewall.");
        }

        var recommended = addresses.FirstOrDefault(address => address.IsPrivate && !address.IsLoopback)?.Url
            ?? addresses.FirstOrDefault(address => !address.IsLoopback)?.Url
            ?? requestOrigin;

        var apiOrigin = $"{Request.Scheme}://{Request.Host}";
        return Ok(new NetworkOverviewDto(
            RequestOrigin: requestOrigin,
            RecommendedBaseUrl: recommended,
            ApiBaseUrl: apiOrigin,
            LocalAddresses: addresses,
            Warnings: warnings));
    }

    private string ResolveRequestOrigin(string? frontendOrigin)
    {
        if (!string.IsNullOrWhiteSpace(frontendOrigin) &&
            Uri.TryCreate(frontendOrigin.Trim(), UriKind.Absolute, out var frontendUri) &&
            (frontendUri.Scheme == Uri.UriSchemeHttp || frontendUri.Scheme == Uri.UriSchemeHttps))
        {
            return frontendUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedHost))
        {
            var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? Request.Scheme : forwardedProto;
            return $"{scheme}://{forwardedHost}".TrimEnd('/');
        }

        return $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
    }

    private static IReadOnlyList<IPAddress> GetLocalIPv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter =>
                adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Where(address =>
                address.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address.Address) &&
                !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .Select(address => address.Address)
            .Distinct()
            .OrderByDescending(IsPrivate)
            .ThenBy(address => address.ToString())
            .ToList();
    }

    private static bool IsLoopbackHost(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
           || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
               || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
               || bytes[0] == 192 && bytes[1] == 168;
    }

    private static string Classify(IPAddress address)
        => IsPrivate(address) ? "LAN" : "IPv4";
}

public sealed record NetworkOverviewDto(
    string RequestOrigin,
    string RecommendedBaseUrl,
    string ApiBaseUrl,
    IReadOnlyList<NetworkAddressDto> LocalAddresses,
    IReadOnlyList<string> Warnings);

public sealed record NetworkAddressDto(
    string Label,
    string Host,
    string Url,
    bool IsLoopback,
    bool IsPrivate,
    bool IsCurrent);
