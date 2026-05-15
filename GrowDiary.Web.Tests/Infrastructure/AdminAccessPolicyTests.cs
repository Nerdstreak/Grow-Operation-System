using System.Net;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class AdminAccessPolicyTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(AdminAccessPolicy.AdminKeyEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(AdminAccessPolicy.AllowRemoteAdminEnvironmentVariable, null);
    }

    [Theory]
    [InlineData("/api/settings")]
    [InlineData("/api/settings/tents")]
    [InlineData("/api/system/backup")]
    [InlineData("/api/system/backup/grow-os-backup-20260101-120000.zip")]
    [InlineData("/api/system/release-readiness")]
    [InlineData("/api/system/database-status")]
    [InlineData("/api/system/api-manifest")]
    [InlineData("/api/system/security-status")]
    [InlineData("/api/system/migration-status")]
    [InlineData("/api/system/upgrade-preflight")]
    [InlineData("/api/system/backup/grow-os-backup-20260101-120000.zip/validate")]
    [InlineData("/api/exports/grows/1")]
    [InlineData("/api/exports/grows/validate")]
    public void IsProtectedPath_ProtectsAdminBackupAndExportRoutes(string path)
    {
        Assert.True(AdminAccessPolicy.IsProtectedPath(new PathString(path)));
    }

    [Theory]
    [InlineData("/api/system/backend-health")]
    [InlineData("/api/grows")]
    [InlineData("/api/hydro-setups")]
    public void IsProtectedPath_DoesNotProtectReadOnlyProductRoutes(string path)
    {
        Assert.False(AdminAccessPolicy.IsProtectedPath(new PathString(path)));
    }

    [Fact]
    public void CanAccess_AllowsLoopbackRequests()
    {
        var context = CreateContext(IPAddress.Loopback, IPAddress.Loopback);

        Assert.True(AdminAccessPolicy.CanAccess(context));
    }

    [Fact]
    public void CanAccess_RejectsRemoteRequestsWithoutAdminKeyOrOverride()
    {
        var context = CreateContext(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("192.168.1.20"));

        Assert.False(AdminAccessPolicy.CanAccess(context));
    }

    [Fact]
    public void CanAccess_AllowsRemoteRequestsWithValidAdminKey()
    {
        Environment.SetEnvironmentVariable(AdminAccessPolicy.AdminKeyEnvironmentVariable, "test-key");
        var context = CreateContext(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("192.168.1.20"));
        context.Request.Headers[AdminAccessPolicy.AdminKeyHeaderName] = "test-key";

        Assert.True(AdminAccessPolicy.CanAccess(context));
    }

    [Fact]
    public void CanAccess_RejectsRemoteRequestsWithInvalidAdminKey()
    {
        Environment.SetEnvironmentVariable(AdminAccessPolicy.AdminKeyEnvironmentVariable, "test-key");
        var context = CreateContext(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("192.168.1.20"));
        context.Request.Headers[AdminAccessPolicy.AdminKeyHeaderName] = "wrong";

        Assert.False(AdminAccessPolicy.CanAccess(context));
    }

    [Fact]
    public void CanAccess_AllowsRemoteRequestsWhenExplicitOverrideIsEnabled()
    {
        Environment.SetEnvironmentVariable(AdminAccessPolicy.AllowRemoteAdminEnvironmentVariable, "true");
        var context = CreateContext(IPAddress.Parse("203.0.113.10"), IPAddress.Parse("192.168.1.20"));

        Assert.True(AdminAccessPolicy.CanAccess(context));
        Assert.True(AdminAccessPolicy.IsInsecureRemoteAdminOverrideActive());
    }

    private static DefaultHttpContext CreateContext(IPAddress remoteIp, IPAddress localIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIp;
        context.Connection.LocalIpAddress = localIp;
        return context;
    }
}
