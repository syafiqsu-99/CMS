// CMS.Server/Program.cs
using CMS.server.Services;
using CMS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Scoped
builder.Services.AddScoped<ExcelGenerationService>();

// Singletons
builder.Services.AddSingleton<PlcService>();
builder.Services.AddSingleton(new SchemaInitializerService(connectionString));
builder.Services.AddSingleton<MachinesService>(sp =>
    new MachinesService(connectionString, sp.GetRequiredService<PlcService>()));
builder.Services.AddSingleton<OEEService>(_ => new OEEService(connectionString));
builder.Services.AddSingleton<SAPService>(_ => new SAPService(connectionString));
builder.Services.AddSingleton<MachineLogService>(sp =>
    new MachineLogService(connectionString, sp.GetRequiredService<PlcService>()));
builder.Services.AddSingleton<SupervisorService>(sp =>
    new SupervisorService(connectionString, sp.GetRequiredService<PlcService>()));

builder.Services.AddSingleton<SettingService>(sp =>
    new SettingService(sp.GetRequiredService<PlcService>()));

// Background PLC monitor
if (!builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<PlcMonitorService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var schemaService = scope.ServiceProvider.GetRequiredService<SchemaInitializerService>();
    await schemaService.EnsureDatabaseSchemaAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();