using CMS.server.Services;
using CMS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<ExcelGenerationService>();
builder.Services.AddSingleton<PlcService>();
builder.Services.AddSingleton<MachineLogService>(provider =>
{
    var plcService = provider.GetRequiredService<PlcService>();
    return new MachineLogService(connectionString, plcService);
});
builder.Services.AddSingleton(new SchemaInitializerService(connectionString));

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<PlcMonitorService>();
}

//builder.Services.AddHostedService<PlcMonitorService>();

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
