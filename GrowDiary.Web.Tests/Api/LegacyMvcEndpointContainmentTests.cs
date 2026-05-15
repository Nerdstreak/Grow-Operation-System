using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Controllers;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace GrowDiary.Web.Tests.Api;

public sealed class LegacyMvcEndpointContainmentTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AppPaths _paths;

    public LegacyMvcEndpointContainmentTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"grow-legacy-containment-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(Path.GetTempPath());
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
    }

    [Fact]
    public void SettingsBackupDatabase_DoesNotReturnRawSqliteDatabase()
    {
        var controller = new SettingsController(
            new GrowRepository(_paths),
            new TemplateRepository(_paths),
            _paths);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = controller.BackupDatabase();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, objectResult.StatusCode);
        var error = Assert.IsType<ApiError>(objectResult.Value);
        Assert.Equal("legacy_backup_disabled", error.Code);
    }

    [Fact]
    public void GrowsLegacyExport_RedirectsToVersionedApiExport()
    {
        var controller = new GrowsController(
            new GrowRepository(_paths),
            new TaskRepository(_paths),
            new JournalRepository(_paths),
            new AuditRepository(_paths));

        var result = controller.Export(42);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/api/exports/grows/42", redirect.Url);
    }
}
