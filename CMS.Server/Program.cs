using CMS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CmsConnection");
var defaultConnectionConfigured = !string.IsNullOrWhiteSpace(connectionString);

if (!defaultConnectionConfigured)
    connectionString = "Server=(unconfigured);Database=(unconfigured);Trusted_Connection=True;TrustServerCertificate=True";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var isDevelopment = builder.Environment.IsDevelopment();

// ── Core singletons ──────────────────────────────────────────────────────
builder.Services.AddSingleton<MainPlcService>();
builder.Services.AddSingleton<SubPlcService>();

// ── Scoped (per-request / per-scope) ──────────────────────────────────────────
builder.Services.AddScoped<ExcelGenerationService>();
builder.Services.AddScoped<ReportExportService>(sp =>
    new ReportExportService(
        connectionString,
        sp.GetRequiredService<SettingService>(),
        sp.GetRequiredService<ILogger<ReportExportService>>()));

builder.Services.AddSingleton<BaseService>(sp =>
    new BaseService(connectionString, sp.GetRequiredService<MainPlcService>(), sp.GetRequiredService<ILogger<BaseService>>(), isDevelopment));

// ── Schema init ───────────────────────────────────────
builder.Services.AddSingleton(new SchemaInitializerService(connectionString));

// ── Page-specific data services ──────
builder.Services.AddSingleton<OEEService>(sp =>
    new OEEService(sp.GetRequiredService<MainPlcService>(), connectionString, sp.GetRequiredService<ILogger<BaseService>>(), isDevelopment));
builder.Services.AddSingleton<SupervisorService>(sp =>
    new SupervisorService(sp.GetRequiredService<MainPlcService>(), connectionString, sp.GetRequiredService<ILogger<BaseService>>(), isDevelopment));
builder.Services.AddSingleton<DashboardService>(sp =>
    new DashboardService(sp.GetRequiredService<MainPlcService>(), connectionString, sp.GetRequiredService<ILogger<BaseService>>(), isDevelopment));
builder.Services.AddSingleton<MachinesService>(sp =>
    new MachinesService(sp.GetRequiredService<MainPlcService>(), connectionString, sp.GetRequiredService<ILogger<BaseService>>(), isDevelopment));
builder.Services.AddSingleton<SettingService>(sp =>
    new SettingService(sp.GetRequiredService<MainPlcService>(), sp.GetRequiredService<SubPlcService>(), connectionString, sp.GetRequiredService<ILogger<BaseService>>(), isDevelopment));

// ── Background services (production only) ───────────────
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MainPlcService>());
    builder.Services.AddHostedService(sp =>
        new ShiftReportBackgroundService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            connectionString,
            sp.GetRequiredService<ILogger<ShiftReportBackgroundService>>()));
}

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var schema = scope.ServiceProvider.GetRequiredService<SchemaInitializerService>();
    await schema.EnsureDatabaseSchemaAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();