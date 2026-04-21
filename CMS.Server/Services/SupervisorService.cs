using CMS.Server.Models;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

public class SupervisorService(PlcService plcService, string connectionString) : BaseService(connectionString, plcService)
{
    public async Task<object> LoadDailyReport(DateOnly production_date, int shift)
    {
        var logUnion = await BuildMachineLogUnionAsync("production_date = @production_date AND shift = @shift");

        var sql = $@"
                WITH all_logs AS (
                    {logUnion}
                ),log_aggregation AS (
                    SELECT 
                        ml.id_machine,
                        ml.id_type,
                        ml.mould,
                        ROUND(SUM(CASE WHEN ml.category = 'PRODUCTION RUNNING' THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS production_running,
                        ROUND(SUM(CASE WHEN ml.category = 'MOULD CHANGE' AND ml.mould_category = 1 THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS change_full_set,
                        ROUND(SUM(CASE WHEN ml.category = 'MOULD CHANGE' AND ml.mould_category = 2 THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS change_half_set,
                        ROUND(SUM(CASE WHEN ml.category = 'MOULD CHANGE' AND ml.mould_category = 3 THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS change_parts,
                        ROUND(SUM(CASE WHEN ml.category IN ('MACHINE BREAKDOWN', 'SCHEDULED MAINTENANCE', 'OTHERS MAIN') THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS maintenance_dt,
                        ROUND(SUM(CASE WHEN ml.category IN ('QUALITY ISSUE', 'SAMPLE RUNNING', 'OTHERS TECH') THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS technician_dt,
                        ROUND(SUM(CASE WHEN ml.category IN ('NO OPERATOR', 'NO SCHEDULE', 'PRODUCT BUYOFF', 'MATERIAL DRYING', 'OTHERS PROD') THEN DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) END)/3600.0, 2) AS production_dt,
                        ROUND(SUM(CASE WHEN category IS NULL THEN DATEDIFF(SECOND, start, COALESCE(finish, GETDATE())) END)/3600.0, 2) AS unallocated,
                        STUFF((
                            SELECT ', ' + FORMAT(m2.start, 'h:mmtt') + ' - ' + FORMAT(m2.finish, 'h:mmtt') + ': ' + m2.problem
                            FROM all_logs m2
                            WHERE m2.id_machine = ml.id_machine
                              AND m2.id_type = ml.id_type
                              AND m2.mould = ml.mould
                              AND m2.problem IS NOT NULL
                            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS remark
                    FROM all_logs ml
                    GROUP BY ml.id_machine, ml.id_type, ml.mould
                )
                SELECT 
                    COALESCE(mm.id_machine, 0) AS id_machine,
                    COALESCE(mm.shift, 1) AS shift,
                    COALESCE(mm.machine_name, '') AS machine_name,
                    COALESCE(mm.packer, '') AS packer,
                    COALESCE(mm.material, '') as material,
                    COALESCE(mm.id_type, 0) AS id_type,
                    COALESCE(mm.mould, 0) AS mould,
                    COALESCE(mm.type, '') AS type,
                    COALESCE(mm.jo_no, '') AS jo_no,
                    COALESCE(mm.qty_perct, 0) AS qty_perct,
                    COALESCE(mm.gross_weight, 0.0) AS gross_weight,
                    COALESCE(mm.part_weight, 0.0) AS part_weight,
                    COALESCE(mm.shot, 0) AS shot,
                    COALESCE(mm.qty_order, 0) AS qty_order,
                    COALESCE(mm.wip_opening, 0) AS wip_opening,
                    COALESCE(mm.wip_closing, 0) AS wip_closing,
                    COALESCE(mm.shift_output, 0) AS shift_output,
                    COALESCE(mm.finish_good, 0) AS finish_good,
                    COALESCE(mm.inward, 0.0) AS inward,
                    COALESCE(mm.qty_accum, 0) AS qty_accum,
                    COALESCE(mm.qty_balance, 0) AS qty_balance,
                    COALESCE(mm.material_used, 0.0) AS material_used,
                    COALESCE(mm.runner, 0.0) AS runner,
                    COALESCE(rej.reject_startup, 0.0) AS reject_startup,
                    COALESCE(((rej.reject_startup / NULLIF(mm.material_used,0)) * 100.0), 0.0) AS reject_startup_per,
                    COALESCE((rej.reject_panelling + rej.reject_lumpy + rej.reject_black_dot + rej.reject_burst + rej.reject_others), 0.0) AS reject_prod,
                    COALESCE((((rej.reject_panelling + rej.reject_lumpy + rej.reject_black_dot + rej.reject_burst + rej.reject_others) / NULLIF(mm.material_used,0)) * 100.0), 0.0) AS reject_prod_per,
                    COALESCE(mm.act_ct, 0.0) AS act_ct,
                    COALESCE(la.production_running, 0.0) AS production_running,
                    COALESCE(mm.sap_ct, 0.0) AS sap_ct,
                    COALESCE(la.change_full_set, 0.0) AS change_full_set,
                    COALESCE(la.change_half_set, 0.0) AS change_half_set,
                    COALESCE(la.change_parts, 0.0) AS change_parts,
                    COALESCE(la.maintenance_dt, 0.0) AS maintenance_dt,
                    COALESCE(la.technician_dt, 0.0) AS technician_dt,
                    COALESCE(la.production_dt, 0.0) AS production_dt,
                    COALESCE(la.remark, '') AS remark,
                    COALESCE(la.unallocated, 0.0) AS unallocated,
                    COALESCE(mm.part_scrap, 0) AS part_scrap,
                    COALESCE(rej.reject_purging, 0.0) AS reject_purging,
                    COALESCE(rej.reject_preform, 0.0) AS reject_preform,
                    COALESCE((rej.total_weight / NULLIF(mm.part_weight, 0)), 0) AS reject_total_pcs
                FROM machine_master mm
                LEFT JOIN reject rej 
                    ON mm.id_machine = rej.id_machine 
                    AND rej.production_date = @production_date 
                    AND rej.shift = @shift
                    AND rej.id_type = mm.id_type 
                    AND rej.mould = mm.mould
                LEFT JOIN log_aggregation la 
                    ON mm.id_machine = la.id_machine 
                    AND mm.id_type = la.id_type 
                    AND mm.mould = la.mould";

        var result = new List<object>();

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@production_date", production_date);
        cmd.Parameters.AddWithValue("@shift", shift);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                shift = Convert.ToInt32(reader["shift"]) == 1 ? "Morning" : "Night",
                machine_name = Convert.ToString(reader["machine_name"]),
                packer = Convert.ToString(reader["packer"]),
                material = Convert.ToString(reader["material"]),
                id_type = Convert.ToInt32(reader["id_type"]),
                mould = Convert.ToInt32(reader["mould"]),
                type = Convert.ToString(reader["type"]),
                jo_no = Convert.ToString(reader["jo_no"]),
                qty_perct = Convert.ToInt32(reader["qty_perct"]),
                gross_weight = Convert.ToSingle(reader["gross_weight"]),
                part_weight = Convert.ToSingle(reader["part_weight"]),
                shot_accum = Convert.ToInt32(reader["shot"]),
                qty_order = Convert.ToInt32(reader["qty_order"]),
                wip_opening = Convert.ToInt32(reader["wip_opening"]),
                wip_closing = Convert.ToInt32(reader["wip_closing"]),
                shift_output = Convert.ToInt32(reader["shift_output"]),
                finish_good = Convert.ToInt32(reader["finish_good"]),
                inward = Convert.ToSingle(reader["inward"]),
                qty_accum = Convert.ToInt32(reader["qty_accum"]),
                qty_balance = Convert.ToInt32(reader["qty_balance"]),
                material_used = Convert.ToSingle(reader["material_used"]),
                runner = Convert.ToSingle(reader["runner"]),
                reject_startup = Convert.ToSingle(reader["reject_startup"]),
                reject_startup_per = Convert.ToSingle(reader["reject_startup_per"]),
                reject_prod = Convert.ToSingle(reader["reject_prod"]),
                reject_prod_per = Convert.ToSingle(reader["reject_prod_per"]),
                act_ct = Convert.ToSingle(reader["act_ct"]),
                production_running = Convert.ToSingle(reader["production_running"]),
                sap_ct = Convert.ToSingle(reader["sap_ct"]),
                change_full_set = Convert.ToSingle(reader["change_full_set"]),
                change_half_set = Convert.ToSingle(reader["change_half_set"]),
                change_parts = Convert.ToSingle(reader["change_parts"]),
                maintenance_dt = Convert.ToSingle(reader["maintenance_dt"]),
                technician_dt = Convert.ToSingle(reader["technician_dt"]),
                production_dt = Convert.ToSingle(reader["production_dt"]),
                remark = Convert.ToString(reader["remark"]),
                unallocated = Convert.ToSingle(reader["unallocated"]),
                part_scrap = Convert.ToSingle(reader["part_scrap"]),
                reject_purging = Convert.ToSingle(reader["reject_purging"]),
                reject_preform = Convert.ToSingle(reader["reject_preform"]),
                reject_total_pcs = Convert.ToInt32(reader["reject_total_pcs"]),
            });
        }
        return result;
    }

    public async Task<object> LoadPrevReport(DateOnly production_date, int shift)
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

        var result = new List<object>();

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@production_date", production_date);
        cmd.Parameters.AddWithValue("@shift", shift);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                shift = Convert.ToInt32(reader["shift"]) == 1 ? "Morning" : "Night",
                machine_name = Convert.ToString(reader["machine_name"]),
                packer = Convert.ToString(reader["packer"]),
                material = Convert.ToString(reader["material"]),
                id_type = Convert.ToInt32(reader["id_type"]),
                mould = Convert.ToInt32(reader["mould"]),
                type = Convert.ToString(reader["type"]),
                jo_no = Convert.ToString(reader["jo_no"]),
                qty_perct = Convert.ToInt32(reader["qty_perct"]),
                gross_weight = Convert.ToSingle(reader["gross_weight"]),
                part_weight = Convert.ToSingle(reader["part_weight"]),
                shot_accum = Convert.ToInt32(reader["shot"]),
                qty_order = Convert.ToInt32(reader["qty_order"]),
                wip_opening = Convert.ToInt32(reader["wip_opening"]),
                wip_closing = Convert.ToInt32(reader["wip_closing"]),
                shift_output = Convert.ToInt32(reader["shift_output"]),
                finish_good = Convert.ToInt32(reader["finish_good"]),
                inward = Convert.ToSingle(reader["inward"]),
                qty_accum = Convert.ToInt32(reader["qty_accum"]),
                qty_balance = Convert.ToInt32(reader["qty_balance"]),
                material_used = Convert.ToSingle(reader["material_used"]),
                runner = Convert.ToSingle(reader["runner"]),
                reject_startup = Convert.ToSingle(reader["reject_startup"]),
                reject_startup_per = Convert.ToSingle(reader["reject_startup_per"]),
                reject_prod = Convert.ToSingle(reader["reject_prod"]),
                reject_prod_per = Convert.ToSingle(reader["reject_prod_per"]),
                act_ct = Convert.ToSingle(reader["act_ct"]),
                production_running = Convert.ToSingle(reader["production_running"]),
                sap_ct = Convert.ToSingle(reader["sap_ct"]),
                change_full_set = Convert.ToSingle(reader["change_full_set"]),
                change_half_set = Convert.ToSingle(reader["change_half_set"]),
                change_parts = Convert.ToSingle(reader["change_parts"]),
                maintenance_dt = Convert.ToSingle(reader["maintenance_dt"]),
                technician_dt = Convert.ToSingle(reader["technician_dt"]),
                production_dt = Convert.ToSingle(reader["production_dt"]),
                remark = Convert.ToString(reader["remark"]),
                unallocated = Convert.ToSingle(reader["unallocated"]),
                part_scrap = Convert.ToSingle(reader["part_scrap"]),
                reject_purging = Convert.ToSingle(reader["reject_purging"]),
                reject_preform = Convert.ToSingle(reader["reject_preform"]),
                reject_total_pcs = Convert.ToInt32(reader["reject_total_pcs"]),
            });
        }

        return result;
    }

    public async Task UpsertDailyReport(DateOnly production_date, int shift, List<Dictionary<string, JsonElement>> reportList)
    {
        await using var conn = await CreateConnectionAsync();
        foreach (var row in reportList)
        {
            if (!row.TryGetValue("id_machine", out var idMachineEl) || !idMachineEl.TryGetInt32(out int idMachine)) continue;

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
        => await UpsertDailyReport(production_date, shift, reportList);

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
                id_machine = Convert.ToInt32(reader["id_machine"]),
                shift = Convert.ToInt32(reader["shift"]),
                machine_name = Convert.ToString(reader["machine_name"]) ?? string.Empty,
                packer = Convert.ToString(reader["packer"]),
                material = Convert.ToString(reader["material"]) ?? string.Empty,
                id_type = Convert.ToInt32(reader["id_type"]),
                mould = Convert.ToInt32(reader["mould"]),
                type = Convert.ToString(reader["type"]) ?? string.Empty,
                jo_no = Convert.ToString(reader["jo_no"]),
                qty_perct = Convert.ToInt32(reader["qty_perct"]),
                gross_weight = Convert.ToDouble(reader["gross_weight"]),
                part_weight = Convert.ToDouble(reader["part_weight"]),
                shot_accum = Convert.ToInt32(reader["shot"]),
                qty_order = Convert.ToInt32(reader["qty_order"]),
                wip_opening = Convert.ToInt32(reader["wip_opening"]),
                wip_closing = Convert.ToInt32(reader["wip_closing"]),
                shift_output = Convert.ToInt32(reader["shift_output"]),
                finish_good = Convert.ToInt32(reader["finish_good"]),
                inward = Convert.ToDouble(reader["inward"]),
                qty_accum = Convert.ToInt32(reader["qty_accum"]),
                qty_balance = Convert.ToInt32(reader["qty_balance"]),
                material_used = Convert.ToDouble(reader["material_used"]),
                runner = Convert.ToDouble(reader["runner"]),
                reject_startup = Convert.ToDouble(reader["reject_startup"]),
                reject_startup_per = Convert.ToDouble(reader["reject_startup_per"]),
                reject_prod = Convert.ToDouble(reader["reject_prod"]),
                reject_prod_per = Convert.ToDouble(reader["reject_prod_per"]),
                act_ct = Convert.ToDouble(reader["act_ct"]),
                production_running = Convert.ToDouble(reader["production_running"]),
                sap_ct = Convert.ToDouble(reader["sap_ct"]),
                change_full_set = Convert.ToDouble(reader["change_full_set"]),
                change_half_set = Convert.ToDouble(reader["change_half_set"]),
                change_parts = Convert.ToDouble(reader["change_parts"]),
                maintenance_dt = Convert.ToDouble(reader["maintenance_dt"]),
                technician_dt = Convert.ToDouble(reader["technician_dt"]),
                production_dt = Convert.ToDouble(reader["production_dt"]),
                remark = Convert.ToString(reader["remark"]),
                unallocated = Convert.ToDouble(reader["unallocated"]),
                part_scrap = Convert.ToDouble(reader["part_scrap"]),
                reject_purging = Convert.ToDouble(reader["reject_purging"]),
                reject_preform = Convert.ToDouble(reader["reject_preform"]),
                reject_total_pcs = Convert.ToInt32(reader["reject_total_pcs"]),
            });
        }

        return result;
    }

    public async Task<object> ImportReport(List<Dictionary<string, JsonElement>> reportList, DateOnly production_date, int shift)
    {
        const string checkSql = @"
                SELECT id_type, mould
                FROM   report
                WHERE  id_machine      = @id_machine
                  AND  production_date = @production_date
                  AND  shift           = @shift;";

        const string sapSql = @"
                SELECT material, type, qty_perct, gross_weight, part_weight, sap_ct
                FROM   sap
                WHERE  id_type = @id_type
                  AND  mould   = @mould;";

        const string updateSql = @"
                UPDATE report
                SET
                    packer             = @packer,
                    jo_no              = CASE WHEN jo_no = '0' THEN @jo_no ELSE jo_no END,
                    id_type            = @new_id_type,
                    mould              = @new_mould,
                    material           = @material,
                    type               = @type,
                    qty_perct          = @qty_perct,
                    gross_weight       = @gross_weight,
                    part_weight        = @part_weight,
                    sap_ct             = @sap_ct,
                    shot               = @shot_accum,
                    qty_order          = @qty_order,
                    wip_opening        = @wip_opening,
                    wip_closing        = @wip_closing,
                    finish_good        = @finish_good,
                    qty_accum          = @qty_accum,
                    reject_startup     = @reject_startup,
                    reject_prod        = @reject_prod,
                    act_ct             = @act_ct,
                    production_running = @production_running,
                    change_full_set    = @change_full_set,
                    change_half_set    = @change_half_set,
                    change_parts       = @change_parts,
                    maintenance_dt     = @maintenance_dt,
                    technician_dt      = @technician_dt,
                    production_dt      = @production_dt,
                    unallocated        = 0,
                    remark             = @remark,
                    reject_purging     = @reject_purging,
                    reject_preform     = @reject_preform,
                    reject_total_pcs   = @reject_total_pcs
                WHERE  id_machine      = @id_machine
                  AND  id_type         = @orig_id_type
                  AND  mould           = @orig_mould
                  AND  production_date = @production_date
                  AND  shift           = @shift
                  AND  (jo_no = '0' OR jo_no = @jo_no);";

        const string insertSql = @"
                INSERT INTO report (
                    id_machine, production_date, shift,
                    packer, jo_no,
                    id_type, mould, material, type,
                    qty_perct, gross_weight, part_weight, sap_ct,
                    shot, qty_order, wip_opening, wip_closing,
                    finish_good, qty_accum,
                    reject_startup, reject_prod,
                    act_ct, production_running,
                    change_full_set, change_half_set, change_parts,
                    maintenance_dt, technician_dt, unallocated,
                    remark, reject_purging, reject_preform, reject_total_pcs
                ) VALUES (
                    @id_machine, @production_date, @shift,
                    @packer, @jo_no,
                    @new_id_type, @new_mould, @material, @type,
                    @qty_perct, @gross_weight, @part_weight, @sap_ct,
                    @shot_accum, @qty_order, @wip_opening, @wip_closing,
                    @finish_good, @qty_accum,
                    @reject_startup, @reject_prod,
                    @act_ct, @production_running,
                    @change_full_set, @change_half_set, @change_parts,
                    @maintenance_dt, @technician_dt, 0,
                    @remark, @reject_purging, @reject_preform, @reject_total_pcs
                );";

        using var conn = await CreateConnectionAsync();

        var grouped = reportList
            .GroupBy(r => (
                IdMachine: Convert.ToInt32(r["id_machine"].GetDouble()),
                Date: DateOnly.TryParse(r["production_date"].GetString(), out var date) ? date : DateOnly.MinValue,
                Shift: Convert.ToInt32(r["shift"].GetDouble())
            ))
            .ToList();

        foreach (var group in grouped)
        {
            int idMachine = group.Key.IdMachine;
            DateOnly rowDate = group.Key.Date;
            int rowShift = group.Key.Shift;

            List<Dictionary<string, JsonElement>> csvRows = group.ToList();

            var dbRows = new List<(int IdType, int Mould)>();

            await using (var checkCmd = new SqlCommand(checkSql, conn))
            {
                checkCmd.Parameters.AddWithValue("@id_machine", idMachine);
                checkCmd.Parameters.AddWithValue("@production_date", rowDate);
                checkCmd.Parameters.AddWithValue("@shift", rowShift);

                await using var reader = await checkCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    dbRows.Add((Convert.ToInt32(reader["id_type"]), Convert.ToInt32(reader["mould"])));
            }

            foreach (var csvRow in csvRows)
            {
                int csvIdType = Convert.ToInt32(csvRow["id_type"].GetDouble());
                int csvMould = Convert.ToInt32(csvRow["mould"].GetDouble());

                var matchedDb = dbRows.FirstOrDefault(db => db.IdType == csvIdType && db.Mould == csvMould);
                bool hasMatch = matchedDb != default;

                string material = Convert.ToString(csvRow["material"].GetString()) ?? string.Empty;
                string type = Convert.ToString(csvRow["type"].GetString()) ?? string.Empty;
                int qtyPerct = Convert.ToInt32(csvRow["qty_perct"].GetDouble());
                float grossWeight = Convert.ToSingle(csvRow["gross_weight"].GetDouble());
                float partWeight = Convert.ToSingle(csvRow["part_weight"].GetDouble());
                float sapCt = Convert.ToSingle(csvRow["sap_ct"].GetDouble());

                await using var sapCmd = new SqlCommand(sapSql, conn);
                sapCmd.Parameters.AddWithValue("@id_type", csvIdType);
                sapCmd.Parameters.AddWithValue("@mould", csvMould);

                await using var sapReader = await sapCmd.ExecuteReaderAsync();
                if (await sapReader.ReadAsync())
                {
                    material = Convert.ToString(sapReader["material"]) ?? string.Empty;
                    type = Convert.ToString(sapReader["type"]) ?? string.Empty;
                    qtyPerct = Convert.ToInt32(sapReader["qty_perct"]);
                    grossWeight = Convert.ToSingle(sapReader["gross_weight"]);
                    partWeight = Convert.ToSingle(sapReader["part_weight"]);
                    sapCt = Convert.ToSingle(sapReader["sap_ct"]);
                }

                int origIdType = hasMatch ? matchedDb.IdType : (dbRows.Count > 0 ? dbRows[0].IdType : csvIdType);
                int origMould = hasMatch ? matchedDb.Mould : (dbRows.Count > 0 ? dbRows[0].Mould : csvMould);

                bool shouldInsert = !hasMatch && dbRows.Count == 0;

                if (!hasMatch && dbRows.Count > 0)
                    dbRows.RemoveAt(0);

                string sql = shouldInsert ? insertSql : updateSql;

                await using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@id_machine", idMachine);
                cmd.Parameters.AddWithValue("@production_date", rowDate);
                cmd.Parameters.AddWithValue("@shift", rowShift);
                cmd.Parameters.AddWithValue("@orig_id_type", origIdType);
                cmd.Parameters.AddWithValue("@orig_mould", origMould);
                cmd.Parameters.AddWithValue("@new_id_type", csvIdType);
                cmd.Parameters.AddWithValue("@new_mould", csvMould);
                cmd.Parameters.AddWithValue("@material", material);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@qty_perct", qtyPerct);
                cmd.Parameters.AddWithValue("@gross_weight", grossWeight);
                cmd.Parameters.AddWithValue("@part_weight", partWeight);
                cmd.Parameters.AddWithValue("@sap_ct", sapCt);
                cmd.Parameters.AddWithValue("@packer", Convert.ToString(csvRow["packer"].GetString()));
                cmd.Parameters.AddWithValue("@jo_no", Convert.ToString(csvRow["jo_no"].GetString()));
                cmd.Parameters.AddWithValue("@shot_accum", Convert.ToInt32(csvRow["shot"].GetDouble()));
                cmd.Parameters.AddWithValue("@qty_order", Convert.ToInt32(csvRow["qty_order"].GetDouble()));
                cmd.Parameters.AddWithValue("@wip_opening", Convert.ToInt32(csvRow["wip_opening"].GetDouble()));
                cmd.Parameters.AddWithValue("@wip_closing", Convert.ToInt32(csvRow["wip_closing"].GetDouble()));
                cmd.Parameters.AddWithValue("@finish_good", Convert.ToInt32(csvRow["finish_good"].GetDouble()));
                cmd.Parameters.AddWithValue("@qty_accum", Convert.ToInt32(csvRow["qty_accum"].GetDouble()));
                cmd.Parameters.AddWithValue("@reject_startup", Convert.ToSingle(csvRow["reject_startup"].GetDouble()));
                cmd.Parameters.AddWithValue("@reject_prod", Convert.ToSingle(csvRow["reject_prod"].GetDouble()));
                cmd.Parameters.AddWithValue("@act_ct", Convert.ToSingle(csvRow["act_ct"].GetDouble()));
                cmd.Parameters.AddWithValue("@production_running", Convert.ToSingle(csvRow["production_running"].GetDouble()));
                cmd.Parameters.AddWithValue("@change_full_set", Convert.ToSingle(csvRow["change_full_set"].GetDouble()));
                cmd.Parameters.AddWithValue("@change_half_set", Convert.ToSingle(csvRow["change_half_set"].GetDouble()));
                cmd.Parameters.AddWithValue("@change_parts", Convert.ToSingle(csvRow["change_parts"].GetDouble()));
                cmd.Parameters.AddWithValue("@maintenance_dt", Convert.ToSingle(csvRow["maintenance_dt"].GetDouble()));
                cmd.Parameters.AddWithValue("@technician_dt", Convert.ToSingle(csvRow["technician_dt"].GetDouble()));
                cmd.Parameters.AddWithValue("@production_dt", Convert.ToSingle(csvRow["production_dt"].GetDouble()));
                cmd.Parameters.AddWithValue("@remark", Convert.ToString(csvRow["remark"].GetString()));
                cmd.Parameters.AddWithValue("@reject_purging", Convert.ToSingle(csvRow["reject_purging"].GetDouble()));
                cmd.Parameters.AddWithValue("@reject_preform", Convert.ToSingle(csvRow["reject_preform"].GetDouble()));
                cmd.Parameters.AddWithValue("@reject_total_pcs", Convert.ToInt32(csvRow["reject_total_pcs"].GetDouble()));

                await cmd.ExecuteNonQueryAsync();
            }
        }

        var reloadDate = DateOnly.TryParse(reportList.First()["production_date"].GetString(), out var date) ? date : DateOnly.MinValue;
        var reloadShift = Convert.ToInt32(reportList.First()["shift"].GetDouble());

        return await LoadPrevReport(reloadDate, reloadShift);
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

    public async Task<object> LoadStaffSchedule()
    {
        var sql = @"
                SELECT * FROM staff_list";

        var result = new List<object>();

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);

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
                start_date = reader["start_date"] == DBNull.Value ? (DateOnly?)null : DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("start_date"))),
                end_date = reader["end_date"] == DBNull.Value ? (DateOnly?)null : DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("end_date"))),
                work_shift = reader["work_shift"] == DBNull.Value ? (int?)null : reader.GetInt32(reader.GetOrdinal("work_shift"))
            });
        }

        return result;
    }

    public async Task UpsertStaffSchedule(List<Dictionary<string, JsonElement>> staffList)
    {
        var time = DateTime.Now;
        var (productionDate, shift) = GetProductionDate(time);

        var sqlUpdateStaff = @"
                SET NOCOUNT ON;
                UPDATE staff_list
                SET staff_name   = @staff_name,
                    staff_role   = @staff_role,
                    status       = @status,
                    machine_name = @machine_name,
                    start_date   = @start_date,
                    end_date     = @end_date,
                    work_shift   = @work_shift
                WHERE staff_id = @staff_id;";

        var sqlUpsertAttendanceInRange = @"
                IF EXISTS (
                    SELECT 1 FROM attendance
                    WHERE staff_id        = @staff_id
                      AND production_date = @production_date
                      AND shift           = @shift
                )
                    UPDATE attendance
                    SET staff_name   = @staff_name,
                        staff_role   = @staff_role,
                        status       = @status,
                        machine_name = @machine_name
                    WHERE staff_id        = @staff_id
                      AND production_date = @production_date
                      AND shift           = @shift;
                ELSE
                    INSERT INTO attendance (staff_id, staff_name, staff_role, status, machine_name, production_date, shift)
                    VALUES (@staff_id, @staff_name, @staff_role, @status, @machine_name, @production_date, @shift);";

        var sqlUpsertAttendanceOutOfRange = @"
                IF EXISTS (
                    SELECT 1 FROM attendance
                    WHERE staff_id        = @staff_id
                      AND production_date = @production_date
                      AND shift           = @shift
                )
                    UPDATE attendance
                    SET staff_name   = @staff_name,
                        staff_role   = @staff_role,
                        status       = 'INACTIVE',
                        machine_name = NULL
                    WHERE staff_id        = @staff_id
                      AND production_date = @production_date
                      AND shift           = @shift;
                ELSE
                    INSERT INTO attendance (staff_id, staff_name, staff_role, status, machine_name, production_date, shift)
                    VALUES (@staff_id, @staff_name, @staff_role, 'INACTIVE', NULL, @production_date, @shift);";

        var sqlUpdatePacker = @"
                DECLARE @PackerTable TABLE (machine_name NVARCHAR(255), work_shift INT, packer NVARCHAR(MAX));

                INSERT INTO @PackerTable (machine_name, work_shift, packer)
                    SELECT 
                        LTRIM(RTRIM(MSplit.machine_name)) AS machine_name,
                        sl.work_shift,
                        STUFF((
                            SELECT '/' + sl2.staff_name
                            FROM staff_list sl2
                            CROSS APPLY (
                                SELECT LTRIM(RTRIM(x2.value('.', 'NVARCHAR(255)'))) AS machine_name
                                FROM (SELECT CAST('<M>' + REPLACE(sl2.machine_name, '/', '</M><M>') + '</M>' AS XML)) AS A2(XMLData)
                                CROSS APPLY XMLData.nodes('/M') AS T2(x2)
                            ) AS Split2
                            WHERE sl2.status = 'ACTIVE'
                              AND sl2.work_shift = sl.work_shift
                              AND Split2.machine_name = MSplit.machine_name
                            FOR XML PATH(''), TYPE
                        ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS packer
                    FROM staff_list sl
                    CROSS APPLY (
                        SELECT LTRIM(RTRIM(x.value('.', 'NVARCHAR(255)'))) AS machine_name
                        FROM (SELECT CAST('<M>' + REPLACE(sl.machine_name, '/', '</M><M>') + '</M>' AS XML)) AS A(XMLData)
                        CROSS APPLY XMLData.nodes('/M') AS T(x)
                    ) AS MSplit
                    WHERE sl.status = 'ACTIVE'
                    GROUP BY MSplit.machine_name, sl.work_shift;

                UPDATE mm
                    SET mm.packer = COALESCE(pt.packer, '')
                    FROM machine_master mm
                    LEFT JOIN @PackerTable pt
                        ON mm.machine_name = pt.machine_name
                       AND mm.shift        = pt.work_shift;

                SELECT * FROM machine_master ORDER BY id_machine;";

        using var conn = await CreateConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            foreach (var staff in staffList)
            {
                if (!staff.ContainsKey("staff_id") || staff["staff_id"].ValueKind == JsonValueKind.Null ||
                    !staff.ContainsKey("staff_name") || staff["staff_name"].ValueKind == JsonValueKind.Null ||
                    !staff.ContainsKey("staff_role") || staff["staff_role"].ValueKind == JsonValueKind.Null)
                    continue;

                int staffId = staff["staff_id"].GetInt32();
                string staffName = staff["staff_name"].GetString() ?? string.Empty;
                string staffRole = staff["staff_role"].GetString() ?? string.Empty;

                string status = staff.TryGetValue("status", out var statusEl) && statusEl.ValueKind != JsonValueKind.Null
                    ? statusEl.GetString() ?? string.Empty : string.Empty;

                string? machineName = null;
                if (staff.TryGetValue("machine_name", out var machineEl) && machineEl.ValueKind != JsonValueKind.Null)
                {
                    string raw = machineEl.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var parts = raw.Split('/').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).OrderBy(s => s);
                        machineName = string.Join("/", parts);
                    }
                }

                DateOnly? startDate = null;
                DateOnly? endDate = null;

                if (staff.TryGetValue("start_date", out var startEl) && startEl.ValueKind == JsonValueKind.String)
                    startDate = DateOnly.TryParse(startEl.GetString(), out var sd) ? sd : (DateOnly?)null;

                if (staff.TryGetValue("end_date", out var endEl) && endEl.ValueKind == JsonValueKind.String)
                    endDate = DateOnly.TryParse(endEl.GetString(), out var ed) ? ed : (DateOnly?)null;

                int? workShift = staff.TryGetValue("work_shift", out var shiftEl) && shiftEl.ValueKind != JsonValueKind.Null
                    ? shiftEl.GetInt32() : (int?)null;

                using (var cmd = new SqlCommand(sqlUpdateStaff, conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@staff_id", staffId);
                    cmd.Parameters.AddWithValue("@staff_name", staffName);
                    cmd.Parameters.AddWithValue("@staff_role", staffRole);
                    cmd.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@machine_name", (object?)machineName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@start_date", startDate.HasValue ? (object)startDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
                    cmd.Parameters.AddWithValue("@end_date", endDate.HasValue ? (object)endDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value);
                    cmd.Parameters.AddWithValue("@work_shift", (object?)workShift ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                bool dateInRange = startDate.HasValue && endDate.HasValue
                    && productionDate >= startDate.Value
                    && productionDate <= endDate.Value;

                bool isInRange = dateInRange
                    && workShift.HasValue
                    && workShift.Value == shift;

                bool isLeaveInRange = dateInRange
                    && workShift.HasValue
                    && (status == "ANNUAL LEAVE" || status == "MEDICAL LEAVE" || status == "OTHER LEAVE");

                string sqlAttendance = (isInRange || isLeaveInRange)
                    ? sqlUpsertAttendanceInRange
                    : sqlUpsertAttendanceOutOfRange;

                using (var cmd2 = new SqlCommand(sqlAttendance, conn, (SqlTransaction)tx))
                {
                    cmd2.Parameters.AddWithValue("@staff_id", staffId);
                    cmd2.Parameters.AddWithValue("@staff_name", staffName);
                    cmd2.Parameters.AddWithValue("@staff_role", staffRole);
                    cmd2.Parameters.AddWithValue("@production_date", productionDate);
                    cmd2.Parameters.AddWithValue("@shift", workShift ?? shift);
                    cmd2.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
                    cmd2.Parameters.AddWithValue("@machine_name", (object?)machineName ?? DBNull.Value);
                    await cmd2.ExecuteNonQueryAsync();
                }
            }

            List<dynamic> master = new();
            await using (var cmd3 = new SqlCommand(sqlUpdatePacker, conn, (SqlTransaction)tx))
            await using (var reader = await cmd3.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    master.Add(new
                    {
                        id_machine = Convert.ToInt32(reader["id_machine"]),
                        machine_name = Convert.ToString(reader["machine_name"]),
                        packer = Convert.ToString(reader["packer"])
                    });
                }
            }

            await tx.CommitAsync();
            _plcService.UpdatePacker(master);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<int> AddStaff(int staff_id, string staff_name, string staff_role)
    {
        const string sql = @"
            INSERT INTO staff_list (staff_id, staff_name, staff_role, status) 
            VALUES (@staff_id, @staff_name, @staff_role, 'INACTIVE');
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@staff_id", staff_id);
        cmd.Parameters.AddWithValue("@staff_name", staff_name);
        cmd.Parameters.AddWithValue("@staff_role", staff_role);

        var result = await cmd.ExecuteScalarAsync();
        return result != DBNull.Value && result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task UpdateStaff(int? staffId, string staffName, string staffRole)
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

        foreach (var old in Directory.GetFiles(folder, $"{staffId}.*"))
            File.Delete(old);

        var savePath = Path.Combine(folder, $"{staffId}{ext}");
        await using var stream = new FileStream(savePath, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    public async Task<object> LoadShiftCalendar(int year, int month)
    {
        var sql = @"
                SELECT
                    *
                FROM calendar
                WHERE YEAR(production_date)  = @year
                  AND MONTH(production_date) = @month
                ORDER BY production_date, shift, start";

        var result = new List<object>();
        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                production_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["production_date"])),
                shift = Convert.ToInt32(reader["shift"]),
                day_type = Convert.ToString(reader["day_type"]),
                planned_hours = Convert.ToSingle(reader["planned_hours"]),
                start = Convert.ToString(reader["start"]),
                finish = Convert.ToString(reader["finish"]),
            });
        }
        return result;
    }

    public async Task UpsertShiftCalendar(List<Calendar> entries)
    {
        var groups = entries
            .GroupBy(e => (e.production_date, e.shift))
            .ToList();

        using var conn = await CreateConnectionAsync();

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

    public async Task<List<object>> LoadSAP()
    {
        const string sql = "SELECT * FROM sap ORDER BY id_type, mould";
        var result = new List<object>();

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result.Add(MapSapRow(reader));

        return result;
    }

    public async Task<(List<object> Items, int TotalCount)> LoadSAPPaged(int page, int pageSize, string? search)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        int offset = (page - 1) * pageSize;

        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        string whereClause = hasSearch
            ? @"WHERE CAST(id_type AS NVARCHAR) LIKE @search
                   OR type LIKE @search"
            : "";

        string sql = $@"
            SELECT COUNT(*) FROM sap {whereClause};

            SELECT id_type, mould, type, qty_perct, process,
                   material, part_weight, tolerance, gross_weight, sap_ct
            FROM   sap
            {whereClause}
            ORDER BY id_type, mould
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        var items = new List<object>();
        int total = 0;

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@offset", offset);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        if (hasSearch)
            cmd.Parameters.AddWithValue("@search", $"%{search!.Trim()}%");

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
            total = reader.GetInt32(0);

        await reader.NextResultAsync();
        while (await reader.ReadAsync())
            items.Add(MapSapRow(reader));

        return (items, total);
    }

    public async Task UpdateSAP(Dictionary<string, JsonElement> SAPList)
    {
        var sql = @"
            UPDATE sap
            SET id_type      = @id_type,
                mould        = @mould,
                type         = @type,
                qty_perct    = @qty_perct,
                process      = @process,
                material     = @material,
                part_weight  = @part_weight,
                tolerance    = @tolerance,
                gross_weight = @gross_weight,
                sap_ct       = @sap_ct
            WHERE id_type = @keys_id_type AND mould = @keys_mould;

            UPDATE m
            SET m.material     = s.material,
                m.type         = s.type,
                m.qty_perct    = s.qty_perct,
                m.sap_ct       = s.sap_ct,
                m.part_weight  = s.part_weight,
                m.gross_weight = s.gross_weight
            FROM machine_master AS m
            JOIN sap AS s ON s.id_type = @id_type AND s.mould = @mould
            WHERE m.id_type = s.id_type AND m.mould = s.mould;

            UPDATE r
            SET r.material     = s.material,
                r.type         = s.type,
                r.qty_perct    = s.qty_perct,
                r.sap_ct       = s.sap_ct,
                r.part_weight  = s.part_weight,
                r.gross_weight = s.gross_weight
            FROM report AS r
            JOIN sap AS s ON s.id_type = @id_type AND s.mould = @mould
            WHERE r.id_type = s.id_type AND r.mould = s.mould;";

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@keys_id_type", SAPList["keys_id_type"].GetInt32());
        cmd.Parameters.AddWithValue("@keys_mould", SAPList["keys_mould"].GetInt32());
        cmd.Parameters.AddWithValue("@id_type", SAPList["id_type"].GetInt32());
        cmd.Parameters.AddWithValue("@mould", SAPList["mould"].GetInt32());
        cmd.Parameters.AddWithValue("@type", SAPList["type"].GetString());
        cmd.Parameters.AddWithValue("@qty_perct", SAPList["qty_perct"].GetInt32());
        cmd.Parameters.AddWithValue("@process", SAPList["process"].GetString());
        cmd.Parameters.AddWithValue("@material", SAPList["material"].GetString());
        cmd.Parameters.AddWithValue("@part_weight", SAPList["part_weight"].GetDouble());
        cmd.Parameters.AddWithValue("@tolerance", SAPList["tolerance"].GetDouble());
        cmd.Parameters.AddWithValue("@gross_weight", SAPList["gross_weight"].GetDouble());
        cmd.Parameters.AddWithValue("@sap_ct", SAPList["sap_ct"].GetDouble());

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSAP(int id_type, int mould)
    {
        const string sql = "DELETE FROM sap WHERE id_type = @id_type AND mould = @mould";

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id_type", id_type);
        cmd.Parameters.AddWithValue("@mould", mould);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertSAP(Dictionary<string, JsonElement> SAPList)
    {
        const string sql = @"
            INSERT INTO sap (id_type, mould, type, qty_perct, process, material, part_weight, tolerance, gross_weight, sap_ct)
            VALUES (@id_type, @mould, @type, @qty_perct, @process, @material, @part_weight, @tolerance, @gross_weight, @sap_ct)";

        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id_type", SAPList["id_type"].GetInt32());
        cmd.Parameters.AddWithValue("@mould", SAPList["mould"].GetInt32());
        cmd.Parameters.AddWithValue("@type", SAPList["type"].GetString());
        cmd.Parameters.AddWithValue("@qty_perct", SAPList["qty_perct"].GetInt32());
        cmd.Parameters.AddWithValue("@process", SAPList["process"].GetString());
        cmd.Parameters.AddWithValue("@material", SAPList["material"].GetString());
        cmd.Parameters.AddWithValue("@part_weight", SAPList["part_weight"].GetDouble());
        cmd.Parameters.AddWithValue("@tolerance", SAPList["tolerance"].GetDouble());
        cmd.Parameters.AddWithValue("@gross_weight", SAPList["gross_weight"].GetDouble());
        cmd.Parameters.AddWithValue("@sap_ct", SAPList["sap_ct"].GetDouble());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ImportSAP(List<Dictionary<string, JsonElement>> sapList)
    {
        using var conn = await CreateConnectionAsync();
        await using var transaction = conn.BeginTransaction();
        try
        {
            const string deleteSql = "DELETE FROM sap WHERE id_type = @id_type AND mould = @mould";
            const string insertSql = @"
                INSERT INTO sap (id_type, mould, type, qty_perct, process, material, part_weight, tolerance, gross_weight, sap_ct)
                VALUES (@id_type, @mould, @type, @qty_perct, @process, @material, @part_weight, @tolerance, @gross_weight, @sap_ct)";

            foreach (var item in sapList)
            {
                await using var del = new SqlCommand(deleteSql, conn, transaction);
                del.Parameters.AddWithValue("@id_type", item["id_type"].GetInt32());
                del.Parameters.AddWithValue("@mould", item["mould"].GetInt32());
                await del.ExecuteNonQueryAsync();

                await using var ins = new SqlCommand(insertSql, conn, transaction);
                ins.Parameters.AddWithValue("@id_type", item["id_type"].GetInt32());
                ins.Parameters.AddWithValue("@mould", item["mould"].GetInt32());
                ins.Parameters.AddWithValue("@type", item["type"].GetString());
                ins.Parameters.AddWithValue("@qty_perct", item["qty_perct"].GetInt32());
                ins.Parameters.AddWithValue("@process", item["process"].GetString());
                ins.Parameters.AddWithValue("@material", item["material"].GetString());
                ins.Parameters.AddWithValue("@part_weight", item["part_weight"].GetDouble());
                ins.Parameters.AddWithValue("@tolerance", item["tolerance"].GetDouble());
                ins.Parameters.AddWithValue("@gross_weight", item["gross_weight"].GetDouble());
                ins.Parameters.AddWithValue("@sap_ct", item["sap_ct"].GetDouble());
                await ins.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static object MapSapRow(SqlDataReader reader) => new
    {
        id_type = Convert.ToInt32(reader["id_type"]),
        mould = Convert.ToInt32(reader["mould"]),
        type = Convert.ToString(reader["type"]),
        qty_perct = Convert.ToInt32(reader["qty_perct"]),
        process = Convert.ToString(reader["process"]),
        material = Convert.ToString(reader["material"]),
        part_weight = Convert.ToSingle(reader["part_weight"]),
        tolerance = Convert.ToSingle(reader["tolerance"]),
        gross_weight = Convert.ToSingle(reader["gross_weight"]),
        sap_ct = Convert.ToSingle(reader["sap_ct"]),
    };
}