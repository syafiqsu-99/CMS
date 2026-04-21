using CMS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<PlcService>();

// ── Scoped (per-request) ──────────────────────────────────────────────────────
builder.Services.AddScoped<ExcelGenerationService>();
builder.Services.AddSingleton<BaseService>(sp =>
{
    var plc = sp.GetRequiredService<PlcService>();
    return new BaseService(connectionString, plc);
});

// ── Schema init ───────────────────────────────────────
builder.Services.AddSingleton(new SchemaInitializerService(connectionString));

// ── Page-specific data services ──────
builder.Services.AddSingleton<OEEService>(sp =>
    new OEEService(sp.GetRequiredService<PlcService>(), connectionString));
builder.Services.AddSingleton<SupervisorService>(sp =>
    new SupervisorService(sp.GetRequiredService<PlcService>(), connectionString));
builder.Services.AddSingleton<DashboardService>(sp =>
    new DashboardService(sp.GetRequiredService<PlcService>(), connectionString));
builder.Services.AddSingleton<MachinesService>(sp =>
    new MachinesService(sp.GetRequiredService<PlcService>(), connectionString));
builder.Services.AddSingleton<SettingService>(sp =>
    new SettingService(sp.GetRequiredService<PlcService>(), connectionString));

if (!builder.Environment.IsDevelopment())
    builder.Services.AddHostedService(sp => sp.GetRequiredService<PlcService>());

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