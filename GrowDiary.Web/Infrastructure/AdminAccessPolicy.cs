using System.Net;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Infrastructure;

public static class AdminAccessPolicy
{
    private const string AllowRemoteAdminEnvironmentVariable = "GROWDIARY_ALLOW_REMOTE_ADMIN";

    public static bool IsProtectedPath(PathString path)
        => path.StartsWithSegments("/settings", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/einstellungen", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/api/settings", StringComparison.OrdinalIgnoreCase);

    public static bool CanAccess(HttpContext context)
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(AllowRemoteAdminEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
