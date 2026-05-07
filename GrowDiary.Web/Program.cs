using System.Text.Json;
using System.Text.Json.Serialization;
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
    if (AdminAccessPolicy.IsProtectedPath(context.Request.Path)
        && !AdminAccessPolicy.CanAccess(context))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Admin-Bereich nur lokal erreichbar.");
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseRouting();

// API-Attribute-Routes, Kamera-Routen und Export-Endpoints
app.MapControllers();

// SPA-Fallback fuer alle non-API-Routen
app.MapFallbackToFile("index.html");

app.Run();
