using CMS.Server.Models;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

public class SupervisorService(PlcService plcService, string connectionString) : BaseService(connectionString, plcService)
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

    public async Task<List<Report>> LoadExcelReport(DateOnly production_date, int shift)
    {
        var sql = @"
                SELECT 
                    COALESCE(id_machine, 1) AS id_machine,
                    COALESCE(shift, 1) AS shift,
                    COALESCE(machine_name, '') AS machine_name,
                    COALESCE(packer, '') AS packer,
                    COALESCE(material, '') AS material,
                    COALESCE(id_type, 123456) AS id_type,
                    COALESCE(mould, 0) AS mould,
                    COALESCE(type, '') AS type,
                    COALESCE(jo_no, '') AS jo_no,
                    COALESCE(qty_perct, 1) AS qty_perct,
                    COALESCE(gross_weight, 0.0) AS gross_weight,
                    COALESCE(part_weight, 0.0) AS part_weight,
                    COALESCE(shot, 0) AS shot,
                    COALESCE(qty_order, 0) AS qty_order,
                    COALESCE(wip_opening, 0) AS wip_opening,
                    COALESCE(wip_closing, 0) AS wip_closing,
                    COALESCE(shift_output, 0) AS shift_output,
                    COALESCE(finish_good, 0) AS finish_good,
                    COALESCE(inward, 0.0) AS inward,
                    COALESCE(qty_accum, 0) AS qty_accum,
                    COALESCE(qty_balance, 0) AS qty_balance,
                    COALESCE(material_used, 0.0) AS material_used,
                    COALESCE(runner, 0.0) AS runner,
                    COALESCE(reject_startup, 0.0) AS reject_startup,
                    COALESCE(reject_startup_per, 0.0) AS reject_startup_per,
                    COALESCE(reject_prod, 0.0) AS reject_prod,
                    COALESCE(reject_prod_per, 0.0) AS reject_prod_per,
                    COALESCE(act_ct, 0.0) AS act_ct,
                    COALESCE(production_running, 0.0) AS production_running,
                    COALESCE(sap_ct, 0.0) AS sap_ct,
                    COALESCE(change_full_set, 0.0) AS change_full_set,
                    COALESCE(change_half_set, 0.0) AS change_half_set,
                    COALESCE(change_parts, 0.0) AS change_parts,
                    COALESCE(maintenance_dt, 0.0) AS maintenance_dt,
                    COALESCE(technician_dt, 0.0) AS technician_dt,
                    COALESCE(production_dt, 0.0) AS production_dt,
                    COALESCE(remark, '') AS remark,
                    COALESCE(unallocated, 0.0) AS unallocated,
                    COALESCE(part_scrap, 0.0) AS part_scrap,
                    COALESCE(reject_purging, 0.0) AS reject_purging,
                    COALESCE(reject_preform, 0.0) AS reject_preform,
                    COALESCE(reject_total_pcs, 0) AS reject_total_pcs
                FROM report
                WHERE production_date = @production_date 
                AND shift = @shift";

        var result = new List<Report>();

        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@production_date", production_date);
        cmd.Parameters.AddWithValue("@shift", shift);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new Report
            {
                IdMachine = Convert.ToInt32(reader["id_machine"]),
                Shift = Convert.ToInt32(reader["shift"]),
                MachineName = Convert.ToString(reader["machine_name"]) ?? string.Empty,
                Packer = Convert.ToString(reader["packer"]),
                Material = Convert.ToString(reader["material"]) ?? string.Empty,
                IdType = Convert.ToInt32(reader["id_type"]),
                Mould = Convert.ToInt32(reader["mould"]),
                Type = Convert.ToString(reader["type"]) ?? string.Empty,
                JoNo = Convert.ToString(reader["jo_no"]),
                QtyPerct = Convert.ToInt32(reader["qty_perct"]),
                GrossWeight = Convert.ToDouble(reader["gross_weight"]),
                PartWeight = Convert.ToDouble(reader["part_weight"]),
                ShotAccum = Convert.ToInt32(reader["shot"]),
                QtyOrder = Convert.ToInt32(reader["qty_order"]),
                WipOpening = Convert.ToInt32(reader["wip_opening"]),
                WipClosing = Convert.ToInt32(reader["wip_closing"]),
                ShiftOutput = Convert.ToInt32(reader["shift_output"]),
                FinishGood = Convert.ToInt32(reader["finish_good"]),
                Inward = Convert.ToDouble(reader["inward"]),
                QtyAccum = Convert.ToInt32(reader["qty_accum"]),
                QtyBalance = Convert.ToInt32(reader["qty_balance"]),
                MaterialUsed = Convert.ToDouble(reader["material_used"]),
                Runner = Convert.ToDouble(reader["runner"]),
                RejectStartup = Convert.ToDouble(reader["reject_startup"]),
                RejectStartupPer = Convert.ToDouble(reader["reject_startup_per"]),
                RejectProd = Convert.ToDouble(reader["reject_prod"]),
                RejectProdPer = Convert.ToDouble(reader["reject_prod_per"]),
                ActCt = Convert.ToDouble(reader["act_ct"]),
                ProductionRunning = Convert.ToDouble(reader["production_running"]),
                SapCt = Convert.ToDouble(reader["sap_ct"]),
                ChangeFullSet = Convert.ToDouble(reader["change_full_set"]),
                ChangeHalfSet = Convert.ToDouble(reader["change_half_set"]),
                ChangeParts = Convert.ToDouble(reader["change_parts"]),
                MaintenanceDt = Convert.ToDouble(reader["maintenance_dt"]),
                TechnicianDt = Convert.ToDouble(reader["technician_dt"]),
                ProductionDt = Convert.ToDouble(reader["production_dt"]),
                Remark = Convert.ToString(reader["remark"]),
                Unallocated = Convert.ToDouble(reader["unallocated"]),
                PartScrap = Convert.ToDouble(reader["part_scrap"]),
                RejectPurging = Convert.ToDouble(reader["reject_purging"]),
                RejectPreform = Convert.ToDouble(reader["reject_preform"]),
                RejectTotalPcs = Convert.ToInt32(reader["reject_total_pcs"])
            });
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

    public async Task UpsertShiftCalendar(List<Calendar> entries)
    {
        var groups = entries
            .GroupBy(e => (e.production_date, e.shift))
            .ToList();

        await using var conn = await CreateConnectionAsync();

        foreach (var group in groups)
        {
            var (production_date, shift) = group.Key;
            var baseDate = production_date.ToDateTime(TimeOnly.MinValue);

            const string deleteSql = @"
                    DELETE FROM calendar
                    WHERE production_date = @production_date AND shift = @shift";

            await using var delCmd = new SqlCommand(deleteSql, conn);
            delCmd.Parameters.AddWithValue("@production_date", baseDate);
            delCmd.Parameters.AddWithValue("@shift", shift);
            await delCmd.ExecuteNonQueryAsync();

            const string insertSql = @"
                    INSERT INTO calendar (production_date, shift, day_type, planned_hours, start, finish)
                    VALUES (@production_date, @shift, @day_type, @planned_hours, @start, @finish)";

            foreach (var e in group)
            {
                var defaultStart = e.shift == 1 ? baseDate.AddHours(6) : baseDate.AddHours(18);
                var defaultFinish = e.shift == 1 ? baseDate.AddHours(18) : baseDate.AddDays(1).AddHours(6);

                DateTime? parsedStart = string.IsNullOrEmpty(e.start_time)
                    ? defaultStart
                    : DateTime.Parse(e.start_time);

                DateTime? parsedFinish = string.IsNullOrEmpty(e.finish_time)
                    ? defaultFinish
                    : DateTime.Parse(e.finish_time);

                await using var insCmd = new SqlCommand(insertSql, conn);
                insCmd.Parameters.AddWithValue("@production_date", baseDate);
                insCmd.Parameters.AddWithValue("@shift", e.shift);
                insCmd.Parameters.AddWithValue("@day_type", e.day_type);
                insCmd.Parameters.AddWithValue("@planned_hours", e.planned_hours);
                insCmd.Parameters.AddWithValue("@start", parsedStart ?? (object)DBNull.Value);
                insCmd.Parameters.AddWithValue("@finish", parsedFinish ?? (object)DBNull.Value);
                await insCmd.ExecuteNonQueryAsync();
            }
        }
    }
    public async Task UpdateMouldChange(Dictionary<string, JsonElement> payload)
    {
        var time = DateTime.Now;
        var (productionDate, shift) = GetProductionDate(time);

        var tableName = $"machine_log_{payload["id_machine"].GetInt32()}";

        var sql = $@"
            IF NOT EXISTS (SELECT 1 FROM reject WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
            BEGIN
                INSERT INTO reject (id_machine, machine_name, id_type, mould, total_weight, reject_panelling, reject_lumpy, reject_black_dot, reject_burst, reject_startup, reject_preform, reject_purging, reject_others, shift, production_date) 
                VALUES (@id_machine, @machine_name, @id_type, @mould, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, @shift, @production_date);
            END

            DECLARE @status_start INT;
            DECLARE @category NVARCHAR(MAX);
            DECLARE @problem NVARCHAR(MAX);
            DECLARE @mould_category NVARCHAR(MAX);

            SELECT TOP 1
                @status_start = status_start,
                @category = category,
                @problem = problem,
                @mould_category = mould_category
            FROM [{tableName}]
            WHERE finish IS NULL;

            IF EXISTS (SELECT 1 FROM [{tableName}] WHERE finish IS NULL)
            BEGIN
                UPDATE [{tableName}] SET finish = @time WHERE finish IS NULL;
            END

            INSERT INTO [{tableName}] (machine_name, id_type, mould, start, shot, category, problem, mould_category, shift, production_date, status_start) 
            VALUES (@machine_name, @id_type, @mould, @time, 0, 
            CASE
                WHEN @status_start = 1 THEN 'PRODUCTION RUNNING'
                ELSE NULLIF(@category, '')
            END,
            NULLIF(@problem, ''),
            CASE
                WHEN NULLIF(@category, '') = 'MOULD CHANGE'
                     AND COALESCE(@mould_category, '') <> ''
                THEN @mould_category
                ELSE '0'
            END,
            @shift, @production_date, @status_start);

            IF NOT EXISTS (SELECT 1 FROM report WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
            BEGIN
                INSERT INTO report (id_machine, machine_name, time, shift, production_date, id_type, mould) 
                VALUES (@id_machine, @machine_name, @time, @shift, @production_date, @id_type, @mould);
            END
            ELSE
            BEGIN
                UPDATE report SET time = @time
                WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift
            END

            UPDATE m
            SET
                m.material = s.material,
                m.id_type = @id_type,
                m.mould = @mould,
                m.type = s.type,
                m.jo_no = 0,
                m.qty_order = 0,
                m.wip_opening = 0,
                m.wip_closing = 0,
                m.finish_good = 0,
                m.qty_accum = 0,
                m.qty_perct = s.qty_perct,
                m.sap_ct = s.sap_ct,
                m.part_weight = s.part_weight,
                m.gross_weight = s.gross_weight,
                m.shift = @shift
            FROM machine_master m
            LEFT JOIN sap s
                ON s.id_type = @id_type
                AND s.mould = @mould
            WHERE m.id_machine = @id_machine;

            SELECT id_machine, part_weight, type, packer FROM machine_master WHERE id_machine = @id_machine";

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id_machine", payload["id_machine"].GetInt32());
        cmd.Parameters.AddWithValue("@machine_name", payload["machine_name"].GetString());
        cmd.Parameters.AddWithValue("@id_type", payload["id_type"].GetInt32());
        cmd.Parameters.AddWithValue("@mould", payload["mould"].GetInt32());
        cmd.Parameters.AddWithValue("@shift", shift);
        cmd.Parameters.AddWithValue("@production_date", productionDate);
        cmd.Parameters.AddWithValue("@time", time);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var result = new
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                packer = Convert.ToString(reader["packer"]),
                type = Convert.ToString(reader["type"]),
                part_weight = Convert.ToSingle(reader["part_weight"]),
            };

            _plcService.UpdatePLCS(result);
        }
    }

}