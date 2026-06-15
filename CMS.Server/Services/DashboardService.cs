using Microsoft.Data.SqlClient;
namespace CMS.Server.Services;
public class DashboardService(MainPlcService mainPlcService, string connectionString, ILogger<BaseService> logger) : BaseService(connectionString, mainPlcService, logger)
{
    public async Task<object> LoadAttendance()
    {
        var time = DateTime.Now;
        var (productionDate, shift) = GetProductionDate(time);

        var sql = @"
                SELECT * FROM staff_list WHERE @production_date BETWEEN start_date AND end_date";

        var result = new List<object>();

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@production_date", productionDate);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                staff_id = Convert.ToInt32(reader["staff_id"]),
                staff_name = Convert.ToString(reader["staff_name"]),
                staff_role = Convert.ToString(reader["staff_role"]),
                status = Convert.ToString(reader["status"]),
                machine_name = Convert.ToString(reader["machine_name"]),
                start_date = Convert.ToDateTime(reader["start_date"]),
                end_date = Convert.ToDateTime(reader["end_date"]),
                shift = reader["work_shift"] != DBNull.Value ? Convert.ToInt32(reader["work_shift"]) : (int?)null
            });
        }
        return result;
    }
}
