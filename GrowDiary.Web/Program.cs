using GrowDiary.Web.Components;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC (weiterhin für API-Endpoints: Export, Camera-Stream, Form-POSTs)
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Blazor Server (neues UI)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var paths = new AppPaths(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<GrowRepository>();
builder.Services.AddSingleton<TaskRepository>();
builder.Services.AddSingleton<JournalRepository>();
builder.Services.AddSingleton<AuditRepository>();
builder.Services.AddSingleton<TemplateRepository>();
builder.Services.AddSingleton<HarvestRepository>();
builder.Services.AddSingleton<CultivationKnowledgeService>();
builder.Services.AddSingleton<MeasurementSanityService>();
builder.Services.AddSingleton<RecommendationEngine>();
builder.Services.AddSingleton<DeviationAnalyzerService>();
builder.Services.AddSingleton<WeekCounterService>();
builder.Services.AddSingleton<ChartService>();
builder.Services.AddSingleton<HomeAssistantService>();
builder.Services.AddSingleton<GrowDashboardComposer>();
builder.Services.AddSingleton<TimelineComposer>();
builder.Services.AddScoped<SensorReadingRepository>();
builder.Services.AddHostedService<HomeAssistantSnapshotWorker>();

var defaultUrls = builder.Configuration["Hosting:DefaultUrls"];
if (!string.IsNullOrWhiteSpace(defaultUrls))
{
    builder.WebHost.UseUrls(defaultUrls);
}

var app = builder.Build();

app.Services.GetRequiredService<DatabaseInitializer>().Initialize();

HaConfigLoader.Apply(
    app.Services.GetRequiredService<AppPaths>(),
    app.Services.GetRequiredService<GrowRepository>());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// MVC-Attribute-Routes (Camera-Stream, Export, Form-POSTs, etc.)
app.MapControllers();

// Blazor Server (alle UI-Seiten)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
