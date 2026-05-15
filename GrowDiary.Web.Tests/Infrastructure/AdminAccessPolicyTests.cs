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
    [InlineData("/api/system/audit-events")]
    [InlineData("/api/system/error-contract")]
    [InlineData("/api/system/migration-status")]
    [InlineData("/api/system/upgrade-preflight")]
    [InlineData("/api/system/backup/grow-os-backup-20260101-120000.zip/validate")]
    [InlineData("/api/system/backup/grow-os-backup-20260101-120000.zip/restore-plan")]
    [InlineData("/api/exports/grows/1")]
    [InlineData("/api/exports/grows/validate")]
    [InlineData("/api/exports/grows/import-plan")]
    [InlineData("/api/grows")]
    [InlineData("/api/grows/1")]
    [InlineData("/api/grows/1/addback")]
    [InlineData("/api/grows/1/measurements")]
    [InlineData("/api/hydro-setups")]
    [InlineData("/api/hardware-items")]
    [InlineData("/api/measurements/1")]
    [InlineData("/api/tasks/1")]
    [InlineData("/api/journal/1")]
    [InlineData("/api/plants")]
    [InlineData("/api/strains")]
    [InlineData("/api/risk-events")]
    [InlineData("/api/sop-instances")]
    [InlineData("/api/maintenance-events")]
    [InlineData("/api/calibration-events")]
    [InlineData("/api/auto-measurements")]
    [InlineData("/api/light-schedules")]
    [InlineData("/api/light-transitions")]
    [InlineData("/api/knowledge")]
    [InlineData("/tents/1/camera.jpg")]
    [InlineData("/tents/1/camera-stream")]
    [InlineData("/tents/1/latest-snapshot")]
    public void IsProtectedPath_ProtectsAdminBackupExportProductApiAndLegacyCameraRoutes(string path)
    {
        Assert.True(AdminAccessPolicy.IsProtectedPath(new PathString(path)));
    }

    [Theory]
    [InlineData("/api/system/backend-health")]
    [InlineData("/api/error")]
    [InlineData("/tents")]
    [InlineData("/tents/1")]
    public void IsProtectedPath_DoesNotProtectExplicitlySafeReadOnlyRoutes(string path)
    {
        Assert.False(AdminAccessPolicy.IsProtectedPath(new PathString(path)));
    }


    [Fact]
    public void ProductApiRoutePrefixes_AreListedSeparatelyForSecurityStatus()
    {
        Assert.Contains(AdminAccessPolicy.ProtectedProductApiRoutePrefixes, prefix => prefix == "/api/grows");
        Assert.Contains(AdminAccessPolicy.ProtectedProductApiRoutePrefixes, prefix => prefix == "/api/hydro-setups");
        Assert.Contains(AdminAccessPolicy.ProtectedProductApiRoutePrefixes, prefix => prefix == "/api/hardware-items");
        Assert.Contains(AdminAccessPolicy.ProtectedRoutePrefixes, prefix => prefix == "/api/grows");
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
