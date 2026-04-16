using Microsoft.Data.SqlClient;

namespace CMS.Server.Services.Base;

/// <summary>
/// Shared base for all data services — owns connection creation only.
/// </summary>
public abstract class BaseDataService
{
    protected readonly string ConnectionString;

    protected BaseDataService(string connectionString)
    {
        ConnectionString = connectionString;
    }

    protected async ValueTask<SqlConnection> CreateConnectionAsync()
    {
        var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Derives production date and shift from a wall-clock time.
    /// Night shift (18:00–06:00) belongs to the calendar date the shift *started*.
    /// </summary>
    protected static (DateOnly productionDate, int shift) GetProductionDate(DateTime time)
    {
        var hour = time.Hour;
        DateTime date = (hour >= 0 && hour < 6) ? time.AddDays(-1).Date : time.Date;
        int shift = (hour >= 6 && hour < 18) ? 1 : 2;
        return (DateOnly.FromDateTime(date), shift);
    }

    public static string GetColor(string? category) => category switch
    {
        "PRODUCTION RUNNING" => "#00ff00",
        "PRODUCT BUYOFF" => "#808080",
        "NO OPERATOR"
        or "NO SCHEDULE"
        or "MATERIAL DRYING"
        or "OTHERS PROD" => "#ffff00",
        "QUALITY ISSUE"
        or "SAMPLE RUNNING"
        or "MOULD CHANGE"
        or "OTHERS TECH" => "#ff0000",
        "SCHEDULED MAINTENANCE"
        or "MACHINE BREAKDOWN"
        or "OTHERS MAIN" => "#ffa500",
        _ => "#808080"
    };
}