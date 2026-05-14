using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Tests.Infrastructure;

public sealed class AdminAccessPolicyTests
{
    [Theory]
    [InlineData("/api/settings")]
    [InlineData("/api/settings/tents")]
    [InlineData("/api/system/backup")]
    [InlineData("/api/system/backup/grow-os-backup-20260101-120000.zip")]
    [InlineData("/api/system/release-readiness")]
    public void IsProtectedPath_ProtectsAdminAndBackupRoutes(string path)
    {
        Assert.True(AdminAccessPolicy.IsProtectedPath(new PathString(path)));
    }

    [Theory]
    [InlineData("/api/system/backend-health")]
    [InlineData("/api/exports/grows/1")]
    [InlineData("/api/grows")]
    public void IsProtectedPath_DoesNotProtectReadOnlyProductRoutes(string path)
    {
        Assert.False(AdminAccessPolicy.IsProtectedPath(new PathString(path)));
    }
}
