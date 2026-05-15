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
var dataProtectionKeyPath = Path.Combine(paths.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("GrowDiary.Web");
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<DatabaseInitializer>();
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
                new ApiError(
                    "admin_access_required",
                    "Dieser administrative Bereich ist standardmaessig nur lokal erreichbar. Fuer Remote-Adminzugriff ist ein Admin-Key oder eine bewusst gesetzte Remote-Freigabe erforderlich."),
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

app.UseStaticFiles();
app.UseRouting();

// API-Attribute-Routes, Kamera-Routen und Export-Endpoints
app.MapControllers();

// SPA-Fallback fuer alle non-API-Routen
app.MapFallbackToFile("index.html");

app.Run();
