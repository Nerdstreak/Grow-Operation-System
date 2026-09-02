using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Controllers;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Reflection;

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
        var controller = new SettingsController();
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
        var controller = new GrowsController();

        var result = controller.Export(42);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/api/exports/grows/42", redirect.Url);
    }

    /// <summary>
    /// Die alten MVC-Routen kollidieren nicht mit den API-Routen.
    /// </summary>
    /// <remarks>
    /// <para>Diese Prüfung hielt früher <c>SystemApiController</c> gegen
    /// <c>SystemController</c>. Den zweiten gibt es seit dem 02.09.2026 nicht
    /// mehr: seine einzige Aktion <c>GET /api/system/network</c> gab die
    /// privaten LAN-Adressen der Maschine heraus — nachgeprüft an der
    /// laufenden App — und hatte keinen einzigen Aufrufer. Sein Nachfolger
    /// <c>GET /api/system/mobile-access</c> wird von <c>MobilePage.tsx</c>
    /// benutzt.</para>
    ///
    /// <para>Geblieben sind die MVC-Reste, die es noch gibt. Kollisionsfrei
    /// müssen sie weiterhin sein: zwei gleiche Routen in einer App sind ein
    /// Startfehler, kein Testfehler — er fällt erst beim Anlauf auf.</para>
    /// </remarks>
    [Fact]
    public void ApiRoutenKollidierenNichtMitDenMvcResten()
    {
        var doppelte = GetControllerRoutes(typeof(SystemApiController), typeof(GrowsController), typeof(SettingsController))
            .GroupBy(route => $"{route.Method} {route.Template}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(route => route.ControllerName))}")
            .ToList();

        Assert.True(doppelte.Count == 0,
            "Diese Routen gibt es zweimal: " + string.Join(" | ", doppelte)
            + ". Zwei gleiche Routen sind ein Startfehler — er faellt erst beim Anlauf auf.");
    }

    private static IEnumerable<(string ControllerName, string Method, string Template)> GetControllerRoutes(params Type[] controllerTypes)
    {
        foreach (var controllerType in controllerTypes)
        {
            var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template?.Trim('/') ?? string.Empty;
            foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var route in method.GetCustomAttributes().OfType<IActionHttpMethodProvider>())
                {
                    var methodRoute = route switch
                    {
                        IRouteTemplateProvider templateProvider => templateProvider.Template?.Trim('/') ?? string.Empty,
                        _ => string.Empty
                    };
                    var template = string.Join('/', new[] { controllerRoute, methodRoute }.Where(part => !string.IsNullOrWhiteSpace(part)));
                    foreach (var httpMethod in route.HttpMethods.DefaultIfEmpty("*"))
                    {
                        yield return (controllerType.Name, httpMethod, template);
                    }
                }
            }
        }
    }
}
