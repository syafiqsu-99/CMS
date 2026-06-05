using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

public class SettingService(MainPlcService mainPlcService, SubPlcService subPlcService, string connectionString)
    : BaseService(connectionString, mainPlcService)
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
}