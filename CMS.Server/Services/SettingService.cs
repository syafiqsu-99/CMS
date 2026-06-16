using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

public class SettingService(MainPlcService mainPlcService, SubPlcService subPlcService, string connectionString, ILogger<BaseService> logger) : BaseService(connectionString, mainPlcService, logger)
{
    public async Task<List<object>> LoadDepartmentPasswords()
    {
        const string sql = @"
            SELECT department, password, updated_at
            FROM plc_passwords
            ORDER BY department";

        var result = new List<object>();
        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result.Add(new
            {
                department = reader.GetString(0),
                password = reader.GetInt32(1),
                updatedAt = reader.GetDateTime(2)
            });

        return result;
    }

    public async Task<object> ChangeDepartmentPasswords(Dictionary<string, int> passwords)
    {
        const string upsertSql = @"
            MERGE plc_passwords AS target
            USING (SELECT @dept AS department, @pass AS password) AS source
                  ON target.department = source.department
            WHEN MATCHED THEN
                UPDATE SET password = source.password, updated_at = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (department, password) VALUES (source.department, source.password);";

        using var conn = await CreateConnectionAsync();
        foreach (var (dept, pass) in passwords)
        {
            await using var cmd = new SqlCommand(upsertSql, conn);
            cmd.Parameters.AddWithValue("@dept", dept.ToLower());
            cmd.Parameters.AddWithValue("@pass", pass);
            await cmd.ExecuteNonQueryAsync();
        }

        mainPlcService.ChangePassword(passwords);

        return new { success = true, updated = passwords.Keys };
    }

    public async Task<Dictionary<string, object?>> ReadAllSubPlcSignals()
        => await subPlcService.ReadAllAsync();

    public async Task<Dictionary<string, object?>> ReadSubPlcSignalById(int id)
        => await subPlcService.ReadOneAsync(id);

    public async Task<object> GetLogsAsync(string? process, int? id_machine)
    {
        var where = new List<string>();
        using var conn = await CreateConnectionAsync();
        using var cmd = new SqlCommand();
        cmd.Connection = conn;

        if (!string.IsNullOrWhiteSpace(process))
        {
            where.Add("process LIKE @process");
            cmd.Parameters.AddWithValue("@process", $"%{process}%");
        }
        if (id_machine.HasValue)
        {
            where.Add("id_machine = @id_machine");
            cmd.Parameters.AddWithValue("@id_machine", id_machine.Value);
        }

        string whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        cmd.CommandText = $@"
            SELECT TOP 1000 id, id_machine, time, process, details, error_message
            FROM db_log {whereClause}
            ORDER BY time DESC";

        var items = new List<Dictionary<string, object?>>();
        using var reader = await cmd.ExecuteReaderAsync();

        int fieldCount = reader.FieldCount;
        string[] columns = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++)
        {
            columns[i] = reader.GetName(i);
        }

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < fieldCount; i++)
            {
                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            items.Add(row);
        }

        return items;
    }
}