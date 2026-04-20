using CMS.server.Services;
using CMS.Server.Models;
using CMS.Server.Services.Base;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

/// <summary>
/// Handles all data operations for the Supervisor page:
/// production reports, staff management, staff scheduling, shift calendar, attendance.
/// Raw SQL is preserved verbatim from MachineLogService — no query modifications.
/// </summary>
public class SupervisorService(string connectionString, PlcService plcService) : BaseDataService(connectionString)
{
    // ── Daily / Prev Report ───────────────────────────────────────────────────

    public async Task<List<object>> LoadDailyReport(DateOnly production_date, int shift)
    {
        var sql = @"
            SELECT r.*, m.machine_name, m.packer,
                   sap.type, sap.material, sap.qty_perct,
                   sap.gross_weight, sap.part_weight, sap.sap_ct,
                   sap.qty_order, sap.qty_accum, sap.finish_good
            FROM report r
            INNER JOIN machine_master m ON r.id_machine = m.id_machine
            LEFT JOIN sap ON r.id_type = sap.id_type AND r.mould = sap.mould
            WHERE r.production_date = @production_date AND r.shift = @shift
            ORDER BY m.machine_name";

        return await ExecuteReportQueryAsync(sql, production_date, shift);
    }

    public async Task<List<object>> LoadPrevReport(DateOnly production_date, int shift)
    {
        var sql = @"
            SELECT r.*, m.machine_name, m.packer,
                   sap.type, sap.material, sap.qty_perct,
                   sap.gross_weight, sap.part_weight, sap.sap_ct,
                   sap.qty_order, sap.qty_accum, sap.finish_good
            FROM report r
            INNER JOIN machine_master m ON r.id_machine = m.id_machine
            LEFT JOIN sap ON r.id_type = sap.id_type AND r.mould = sap.mould
            WHERE r.production_date = @production_date AND r.shift = @shift
            ORDER BY m.machine_name";

        return await ExecuteReportQueryAsync(sql, production_date, shift);
    }

    private async Task<List<object>> ExecuteReportQueryAsync(string sql, DateOnly date, int shift)
    {
        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@production_date", date);
        cmd.Parameters.AddWithValue("@shift", shift);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Add(row);
        }
        return result;
    }

    public async Task UpsertDailyReport(DateOnly production_date, int shift, List<Dictionary<string, JsonElement>> reportList)
    {
        // Delegates to the same upsert logic — connection opened per operation
        await using var conn = await CreateConnectionAsync();
        foreach (var row in reportList)
        {
            if (!row.TryGetValue("id_machine", out var idMachineEl) ||
                !idMachineEl.TryGetInt32(out int idMachine)) continue;

            var sql = @"
                UPDATE report SET
                    shot = @shot, shot_accum = @shot_accum,
                    shift_output = @shift_output, finish_good = @finish_good,
                    reject_startup = @reject_startup, reject_prod = @reject_prod,
                    reject_purging = @reject_purging, reject_preform = @reject_preform,
                    material_used = @material_used, runner = @runner,
                    inward = @inward, remark = @remark,
                    id_type = @id_type, mould = @mould
                WHERE id_machine = @id_machine
                  AND production_date = @production_date
                  AND shift = @shift";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_machine", idMachine);
            cmd.Parameters.AddWithValue("@production_date", production_date);
            cmd.Parameters.AddWithValue("@shift", shift);
            AddReportParams(cmd, row);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task UpsertPrevReport(DateOnly production_date, int shift, List<Dictionary<string, JsonElement>> reportList)
        => await UpsertDailyReport(production_date, shift, reportList); // same logic, different semantic intent

    private static void AddReportParams(SqlCommand cmd, Dictionary<string, JsonElement> row)
    {
        void AddParam(string name, string key, Func<JsonElement, object?> convert)
        {
            if (row.TryGetValue(key, out var el))
                cmd.Parameters.AddWithValue(name, convert(el) ?? DBNull.Value);
            else
                cmd.Parameters.AddWithValue(name, DBNull.Value);
        }

        AddParam("@shot", "shot", e => e.TryGetInt32(out int v) ? v : null);
        AddParam("@shot_accum", "shot_accum", e => e.TryGetInt32(out int v) ? v : null);
        AddParam("@shift_output", "shift_output", e => e.TryGetInt32(out int v) ? v : null);
        AddParam("@finish_good", "finish_good", e => e.TryGetInt32(out int v) ? v : null);
        AddParam("@reject_startup", "reject_startup", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@reject_prod", "reject_prod", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@reject_purging", "reject_purging", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@reject_preform", "reject_preform", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@material_used", "material_used", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@runner", "runner", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@inward", "inward", e => e.TryGetDouble(out double v) ? v : null);
        AddParam("@remark", "remark", e => e.ValueKind != JsonValueKind.Null ? e.GetString() : null);
        AddParam("@id_type", "id_type", e => e.TryGetInt32(out int v) ? v : null);
        AddParam("@mould", "mould", e => e.TryGetInt32(out int v) ? v : null);
    }

    // ── Excel Export ──────────────────────────────────────────────────────────

    public async Task<List<Dictionary<string, object?>>> LoadExcelReport(DateOnly date, int shift)
    {
        var sql = @"
            SELECT r.*, m.machine_name, m.packer,
                   sap.type, sap.material, sap.qty_perct,
                   sap.gross_weight, sap.part_weight, sap.sap_ct
            FROM report r
            INNER JOIN machine_master m ON r.id_machine = m.id_machine
            LEFT JOIN sap ON r.id_type = sap.id_type AND r.mould = sap.mould
            WHERE r.production_date = @production_date AND r.shift = @shift
            ORDER BY m.machine_name";

        var result = new List<Dictionary<string, object?>>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@production_date", date);
        cmd.Parameters.AddWithValue("@shift", shift);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Add(row);
        }
        return result;
    }

    // ── Staff Schedule ────────────────────────────────────────────────────────

    public async Task<List<object>> LoadStaffSchedule()
    {
        const string sql = @"
            SELECT s.staff_id, s.staff_name, s.staff_role,
                   ss.shift, ss.status, ss.start_date, ss.end_date,
                   ss.machine_name, ss.id_machine
            FROM staff s
            LEFT JOIN staff_schedule ss ON s.staff_id = ss.staff_id
            ORDER BY s.staff_name";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                staff_id = Convert.ToInt32(reader["staff_id"]),
                staff_name = reader["staff_name"].ToString(),
                staff_role = reader["staff_role"].ToString(),
                shift = reader.IsDBNull(reader.GetOrdinal("shift")) ? (object?)null : Convert.ToInt32(reader["shift"]),
                status = reader["status"].ToString(),
                start_date = reader.IsDBNull(reader.GetOrdinal("start_date")) ? (object?)null : reader["start_date"],
                end_date = reader.IsDBNull(reader.GetOrdinal("end_date")) ? (object?)null : reader["end_date"],
                machine_name = reader["machine_name"].ToString(),
                id_machine = reader["id_machine"].ToString(),
            });
        }
        return result;
    }

    public async Task UpsertStaffSchedule(List<Dictionary<string, JsonElement>> payload)
    {
        const string sql = @"
            MERGE staff_schedule AS target
            USING (SELECT @staff_id AS staff_id) AS source ON target.staff_id = source.staff_id
            WHEN MATCHED THEN UPDATE SET
                shift = @shift, status = @status, start_date = @start_date, end_date = @end_date,
                machine_name = @machine_name, id_machine = @id_machine
            WHEN NOT MATCHED THEN INSERT (staff_id, shift, status, start_date, end_date, machine_name, id_machine)
                VALUES (@staff_id, @shift, @status, @start_date, @end_date, @machine_name, @id_machine);";

        await using var conn = await CreateConnectionAsync();
        foreach (var item in payload)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@staff_id", item["staff_id"].GetInt32());
            cmd.Parameters.AddWithValue("@shift", item.TryGetValue("shift", out var sh) && sh.TryGetInt32(out int sv) ? sv : DBNull.Value);
            cmd.Parameters.AddWithValue("@status", item.TryGetValue("status", out var st) ? st.GetString() ?? (object)DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@start_date", item.TryGetValue("start_date", out var sd) && sd.ValueKind != JsonValueKind.Null ? sd.GetString() ?? (object)DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@end_date", item.TryGetValue("end_date", out var ed) && ed.ValueKind != JsonValueKind.Null ? ed.GetString() ?? (object)DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@machine_name", item.TryGetValue("machine_name", out var mn) ? mn.GetString() ?? (object)DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@id_machine", item.TryGetValue("id_machine", out var im) ? im.GetString() ?? (object)DBNull.Value : DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ── Staff CRUD ────────────────────────────────────────────────────────────

    public async Task<int> AddStaff(string staffName, string staffRole)
    {
        const string sql = "INSERT INTO staff (staff_name, staff_role) OUTPUT INSERTED.staff_id VALUES (@name, @role)";
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", staffName);
        cmd.Parameters.AddWithValue("@role", staffRole);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateStaff(int staffId, string staffName, string staffRole)
    {
        const string sql = "UPDATE staff SET staff_name = @name, staff_role = @role WHERE staff_id = @id";
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", staffId);
        cmd.Parameters.AddWithValue("@name", staffName);
        cmd.Parameters.AddWithValue("@role", staffRole);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteStaff(int staffId)
    {
        const string sql = "DELETE FROM staff WHERE staff_id = @id";
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", staffId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Staff Photo ───────────────────────────────────────────────────────────

    public string GetStaffPhotoPath(int staffId)
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "staff_list");
        var jpgPath = Path.Combine(folder, $"{staffId}.jpg");
        var pngPath = Path.Combine(folder, $"{staffId}.png");
        return File.Exists(jpgPath) ? jpgPath : File.Exists(pngPath) ? pngPath : string.Empty;
    }

    public async Task SaveStaffPhoto(int staffId, IFormFile file)
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "staff_list");
        Directory.CreateDirectory(folder);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            throw new InvalidOperationException("Only JPG and PNG files are allowed.");

        // Remove old photo(s) to avoid stale files
        foreach (var old in Directory.GetFiles(folder, $"{staffId}.*"))
            File.Delete(old);

        var savePath = Path.Combine(folder, $"{staffId}{ext}");
        await using var stream = new FileStream(savePath, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    // ── Attendance ────────────────────────────────────────────────────────────

    public async Task<List<object>> LoadAttendance()
    {
        const string sql = @"
            SELECT s.staff_id, s.staff_name, ss.machine_name,
                   ss.status, ss.shift
            FROM staff s
            LEFT JOIN staff_schedule ss ON s.staff_id = ss.staff_id
            ORDER BY s.staff_name";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                staff_id = Convert.ToInt32(reader["staff_id"]),
                staff_name = reader["staff_name"].ToString(),
                machine_name = reader["machine_name"].ToString(),
                status = reader["status"].ToString(),
                shift = reader.IsDBNull(reader.GetOrdinal("shift")) ? (object?)null : Convert.ToInt32(reader["shift"]),
            });
        }
        return result;
    }

    // ── Shift Calendar ────────────────────────────────────────────────────────

    public async Task<List<object>> LoadShiftCalendar(int year, int month)
    {
        const string sql = @"
            SELECT calendar_date, shift, status, remark
            FROM calendar
            WHERE YEAR(calendar_date) = @year AND MONTH(calendar_date) = @month
            ORDER BY calendar_date";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                calendar_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["calendar_date"])).ToString("yyyy-MM-dd"),
                shift = Convert.ToInt32(reader["shift"]),
                status = reader["status"].ToString(),
                remark = reader["remark"].ToString(),
            });
        }
        return result;
    }

    public async Task UpsertShiftCalendar(List<calendar> entries)
    {
        const string sql = @"
            MERGE calendar AS target
            USING (SELECT @calendar_date AS calendar_date) AS source ON target.calendar_date = source.calendar_date
            WHEN MATCHED THEN UPDATE SET shift = @shift, status = @status, remark = @remark
            WHEN NOT MATCHED THEN INSERT (calendar_date, shift, status, remark)
                VALUES (@calendar_date, @shift, @status, @remark);";

        await using var conn = await CreateConnectionAsync();
        foreach (var entry in entries)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@calendar_date", entry.calendar_date);
            cmd.Parameters.AddWithValue("@shift", entry.shift);
            cmd.Parameters.AddWithValue("@status", entry.status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@remark", entry.remark ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}