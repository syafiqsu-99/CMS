using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

public class SettingService(PlcService plcService, string connectionString) : BaseService(connectionString, plcService)
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

        plcService.ChangePassword(passwords);

        return new { success = true, updated = passwords.Keys };
    }

    public object ReadSubPlcSignals(int machineId)
        => plcService.ReadSubPlcSignals(machineId);

    public async Task<Dictionary<string, object?>> ReadAllPlcSignals()
    {
        const string sql = "SELECT id_machine, machine_name FROM machine_master ORDER BY id_machine";

        var machines = new List<(int id, string name)>();
        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            machines.Add((reader.GetInt32(0), reader.GetString(1)));

        var result = new Dictionary<string, object?>();

        await Parallel.ForEachAsync(machines, async (machine, _) =>
        {
            try
            {
                var data = await Task.Run(() => plcService.ReadSubPlcSignals(machine.id));
                if (data != null) data["machine_name"] = machine.name;
                lock (result) result[machine.id.ToString()] = data;
            }
            catch
            {
                lock (result) result[machine.id.ToString()] = null;
            }
        });

        return result;
    }
}