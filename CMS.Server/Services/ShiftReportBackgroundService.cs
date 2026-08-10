using Microsoft.Data.SqlClient;
using System.Data;

namespace CMS.Server.Services;

public class ShiftReportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _connectionString;
    private readonly ILogger<ShiftReportBackgroundService> _logger;

    private static readonly TimeSpan MorningTrigger = new(6, 5, 0);
    private static readonly TimeSpan NightTrigger = new(18, 5, 0);

    public ShiftReportBackgroundService(
        IServiceScopeFactory scopeFactory,
        string connectionString,
        ILogger<ShiftReportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionString = connectionString;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var (nextRun, shift) = GetNextRun(now);

            var delay = nextRun - now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var productionDate = GetStartDate(nextRun, shift);
            await RunExportAsync(productionDate, shift, stoppingToken);
        }
    }

    private static (DateTime nextRun, int shift) GetNextRun(DateTime now)
    {
        var todayMorning = now.Date + MorningTrigger;
        var todayNight = now.Date + NightTrigger;

        if (now < todayMorning) return (todayMorning, 2);
        if (now < todayNight) return (todayNight, 1);

        return (now.Date.AddDays(1) + MorningTrigger, 2);
    }

    private static DateOnly GetStartDate(DateTime runTime, int shift)
    {
        // Shift 1 (morning) trigger fires at 18:05 same day -> start date is that day.
        // Shift 2 (night) trigger fires at 06:05 -> the night shift started 18:00 the previous day.
        var date = shift == 2 ? runTime.Date.AddDays(-1) : runTime.Date;
        return DateOnly.FromDateTime(date);
    }

    private async Task RunExportAsync(DateOnly productionDate, int shift, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var exportService = scope.ServiceProvider.GetRequiredService<ReportExportService>();
            await exportService.ExportShiftAsync(productionDate, shift, ct);

            await LogDbAsync("ShiftReportExport", $"Exported {productionDate} shift {shift}.", null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ShiftReportExport] Failed for {Date} shift {Shift}.", productionDate, shift);
            await LogDbAsync("ShiftReportExport", $"Failed {productionDate} shift {shift}.", ex.Message);
        }
    }

    private async Task LogDbAsync(string process, string? details, string? error)
    {
        try
        {
            const string sql = @"
                INSERT INTO db_log (process, details, error_message)
                VALUES (@process, @details, @error_message)";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@process", SqlDbType.NVarChar, 64).Value = process;
            cmd.Parameters.Add("@details", SqlDbType.NVarChar, 512).Value = (object?)details ?? DBNull.Value;
            cmd.Parameters.Add("@error_message", SqlDbType.NVarChar, 1024).Value = (object?)error ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ShiftReportExport] Failed to write db_log entry.");
        }
    }
}