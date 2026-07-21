using System.Text.Json;
using System.Text.Json.Serialization;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Controller-Endpoints fuer JSON-APIs, Kamera-Routen und verbleibende Kompatibilitaets-POSTs
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddHttpClient();

var paths = new AppPaths(builder.Environment.ContentRootPath);
Directory.CreateDirectory(paths.DataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(paths.DataProtectionKeysPath))
    .SetApplicationName("GrowDiary.Web");
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<TentRepository>();
builder.Services.AddSingleton<HydroSetupRepository>();
builder.Services.AddSingleton<AddbackRepository>();
builder.Services.AddSingleton<HardwareRepository>();
builder.Services.AddSingleton<SetupRepository>();
builder.Services.AddSingleton<AutoMeasurementRepository>();
builder.Services.AddSingleton<LightRepository>();
builder.Services.AddSingleton<SopRepository>();
builder.Services.AddSingleton<PhotoRepository>();
builder.Services.AddSingleton<HomeAssistantSettingsRepository>();
builder.Services.AddSingleton<CameraFrameCache>();
builder.Services.AddSingleton<GrowCoreRepository>();
builder.Services.AddSingleton<MeasurementRepository>();
builder.Services.AddSingleton<GrowRepository>();
builder.Services.AddSingleton<TaskRepository>();
builder.Services.AddSingleton<JournalRepository>();
builder.Services.AddSingleton<AuditRepository>();
builder.Services.AddSingleton<SystemAuditRepository>();
builder.Services.AddSingleton<TemplateRepository>();
builder.Services.AddSingleton<HarvestRepository>();
builder.Services.AddSingleton<KnowledgeBaseLoader>();
builder.Services.AddSingleton<CultivationKnowledgeService>();
builder.Services.AddSingleton<TargetValueService>();
builder.Services.AddSingleton<MeasurementSanityService>();
builder.Services.AddSingleton<RecommendationEngine>();
builder.Services.AddSingleton<GrowAlertService>();
builder.Services.AddSingleton<DeviationAnalyzerService>();
builder.Services.AddSingleton<TreatmentRecommender>();
builder.Services.AddSingleton<DeviationRiskEventSyncService>();
builder.Services.AddSingleton<RiskEventSopRecommender>();
builder.Services.AddSingleton<WeekCounterService>();
builder.Services.AddSingleton<ChartService>();
builder.Services.AddSingleton<HomeAssistantService>();
builder.Services.AddSingleton<LightStatusTransitionService>();
builder.Services.AddSingleton<AutoMeasurementValueGuard>();
builder.Services.AddSingleton<AutoMeasurementStatusService>();
builder.Services.AddSingleton<PhotoStorageService>();
builder.Services.AddSingleton<GrowDashboardComposer>();
builder.Services.AddScoped<SensorReadingRepository>();
builder.Services.AddScoped<AutoMeasurementExecutionService>();
builder.Services.AddHostedService<HomeAssistantSnapshotWorker>();
builder.Services.AddHostedService<AutoMeasurementWorker>();

var defaultUrls = builder.Configuration["Hosting:DefaultUrls"];
if (!string.IsNullOrWhiteSpace(defaultUrls))
{
    builder.WebHost.UseUrls(defaultUrls);
}

var app = builder.Build();

app.Services.GetRequiredService<DatabaseInitializer>().Initialize();
app.Services.GetRequiredService<KnowledgeBaseLoader>().Initialize();

HaConfigLoader.Apply(
    app.Services.GetRequiredService<AppPaths>(),
    app.Services.GetRequiredService<GrowRepository>());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/api/error");
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");

    await next();
});

// When running as a Home Assistant add-on, requests arrive through the ingress
// proxy under a dynamic base path (e.g. /api/hassio_ingress/<token>). Home
// Assistant already strips that prefix, so we only need to record it as PathBase
// so any server-generated URLs point back through the ingress.
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue(AdminAccessPolicy.IngressPathHeaderName, out var ingressPath))
    {
        var value = ingressPath.ToString().TrimEnd('/');
        // PathString requires a leading slash; guard against a malformed header.
        if (value.StartsWith('/'))
        {
            context.Request.PathBase = new PathString(value);
        }
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (AdminAccessPolicy.IsProtectedPath(context.Request.Path))
    {
        var isLocal = AdminAccessPolicy.IsLocalRequest(context);
        var canAccess = AdminAccessPolicy.CanAccess(context);
        if (!isLocal)
        {
            TryLogAdminAccess(context, canAccess);
        }

        if (!canAccess)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                ApiErrorFactory.Forbidden(
                    "admin_access_required",
                    "Dieser Bereich ist nur lokal oder ueber Home Assistant (Ingress) erreichbar.",
                    context.TraceIdentifier),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return;
        }
    }

    await next();
});

static void TryLogAdminAccess(HttpContext context, bool allowed)
{
    try
    {
        var audit = context.RequestServices.GetService<SystemAuditRepository>();
        audit?.Add(new GrowDiary.Web.Models.SystemAuditEvent
        {
            EventType = "security",
            Action = allowed ? "remote-admin-access-allowed" : "remote-admin-access-blocked",
            Summary = $"{context.Request.Method} {context.Request.Path}",
            Severity = allowed ? "warning" : "critical",
            Source = "admin-access-middleware",
            RemoteAddress = context.Connection.RemoteIpAddress?.ToString(),
            Success = allowed
        });
    }
    catch
    {
        // Audit logging must never block request handling.
    }
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        // index.html must always revalidate, otherwise clients keep loading a stale
        // shell that points at old asset hashes (hashed /assets/* files stay immutably
        // cacheable).
        if (name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        }
    }
});

// Grow photos / uploads live under the persistent data root (outside wwwroot) so they
// survive updates and are captured by Home Assistant backups. Serve them at /uploads.
Directory.CreateDirectory(paths.UploadRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(paths.UploadRootPath),
    RequestPath = "/uploads"
});

app.UseRouting();

// API-Attribute-Routes, Kamera-Routen und Export-Endpoints
app.MapControllers();

// SPA-Fallback fuer alle non-API-Routen — index.html immer frisch (kein Stale-Shell).
// Injects a <base href> so the app's relative asset/API URLs resolve correctly both
// at the site root ("/") and behind the Home Assistant ingress (the request PathBase).
app.MapFallback(async context =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.ContentType = "text/html; charset=utf-8";

    var html = await File.ReadAllTextAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
    var pathBase = context.Request.PathBase.HasValue ? context.Request.PathBase.Value! : string.Empty;
    var baseHref = string.IsNullOrEmpty(pathBase) ? "/" : pathBase + "/";
    await context.Response.WriteAsync(InjectBaseHref(html, baseHref));
});

static string InjectBaseHref(string html, string baseHref)
{
    var tag = $"<base href=\"{baseHref}\" />";
    var headIndex = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
    if (headIndex < 0)
    {
        return html;
    }

    var insertAt = headIndex + "<head>".Length;
    return string.Concat(html.AsSpan(0, insertAt), tag, html.AsSpan(insertAt));
}

app.Run();
