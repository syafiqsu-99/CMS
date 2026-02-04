using CMS.Server.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;

namespace CMS.server.Services
{
    public class MachineLogService
    {
        private readonly string _connectionString;
        private readonly PlcService _plcService;
        private readonly ConcurrentDictionary<int, (dynamic plcData, DateOnly productionDate, int shift, bool measure_qc)> _lastMachineMaster = new();

        public MachineLogService(string connectionString, PlcService plcService)
        {
            _connectionString = connectionString;
            _plcService = plcService;
        }
        private async ValueTask<SqlConnection> CreateConnection()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        #region API query
        public async Task<object> LoadDailyReport(DateOnly production_date, int shift)
        {
            var sql = @"
                WITH all_logs AS (
                    SELECT 1 AS id_machine, * FROM machine_log_1 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 2 AS id_machine, * FROM machine_log_2 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 3 AS id_machine, * FROM machine_log_3 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 4 AS id_machine, * FROM machine_log_4 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 5 AS id_machine, * FROM machine_log_5 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 6 AS id_machine, * FROM machine_log_6 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 7 AS id_machine, * FROM machine_log_7 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 8 AS id_machine, * FROM machine_log_8 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 9 AS id_machine, * FROM machine_log_9 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 10 AS id_machine, * FROM machine_log_10 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 11 AS id_machine, * FROM machine_log_11 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 12 AS id_machine, * FROM machine_log_12 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 13 AS id_machine, * FROM machine_log_13 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 14 AS id_machine, * FROM machine_log_14 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 15 AS id_machine, * FROM machine_log_15 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 16 AS id_machine, * FROM machine_log_16 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 17 AS id_machine, * FROM machine_log_17 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 18 AS id_machine, * FROM machine_log_18 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 19 AS id_machine, * FROM machine_log_19 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 20 AS id_machine, * FROM machine_log_20 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 21 AS id_machine, * FROM machine_log_21 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 22 AS id_machine, * FROM machine_log_22 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 23 AS id_machine, * FROM machine_log_23 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 24 AS id_machine, * FROM machine_log_24 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 25 AS id_machine, * FROM machine_log_25 WHERE production_date = @production_date and shift = @shift
                    UNION ALL
                    SELECT 26 AS id_machine, * FROM machine_log_26 WHERE production_date = @production_date and shift = @shift
                ),log_aggregation AS (
                    SELECT 
                        ml.id_machine,
                        ml.id_type,
                        ml.mould,
                        ROUND(SUM(CASE WHEN ml.category = 'PRODUCTION RUNNING' THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS production_running,
                        ROUND(SUM(CASE WHEN ml.category = 'MOULD CHANGE' AND ml.mould_category = 1 THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS change_full_set,
                        ROUND(SUM(CASE WHEN ml.category = 'MOULD CHANGE' AND ml.mould_category = 2 THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS change_half_set,
                        ROUND(SUM(CASE WHEN ml.category = 'MOULD CHANGE' AND ml.mould_category = 3 THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS change_parts,
                        ROUND(SUM(CASE WHEN ml.category IN ('MACHINE BREAKDOWN', 'SCHEDULED MAINTENANCE', 'OTHERS MAIN') THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS maintenance_dt,
                        ROUND(SUM(CASE WHEN ml.category IN ('QUALITY ISSUE', 'SAMPLE RUNNING', 'OTHERS TECH') THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS technician_dt,
                        ROUND(SUM(CASE WHEN ml.category IN ('NO OPERATOR', 'NO SCHEDULE', 'PRODUCT BUYOFF', 'MATERIAL DRYING', 'OTHERS PROD') THEN DATEDIFF(SECOND, ml.start, ISNULL(ml.finish, GETDATE())) END)/3600.0, 2) AS production_dt,
                        ROUND(SUM(CASE WHEN category IS NULL THEN DATEDIFF(SECOND, start, ISNULL(finish, GETDATE())) END)/3600.0, 2) AS unallocated,
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

            using var conn = await CreateConnection();
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
        public async Task<object> UpdateDailyReport(List<Dictionary<string, JsonElement>> reportList, DateOnly production_date, int shift)
        {
            var sql = @"
                UPDATE machine_master 
                SET packer = @packer,
                    jo_no = @jo_no,
                    qty_order = @qty_order,
                    wip_opening = @wip_opening,
                    wip_closing = @wip_closing,
                    finish_good = @finish_good,
                    qty_accum = @qty_accum,
                    part_scrap = @part_scrap
                WHERE id_machine = @id_machine

                UPDATE r
                SET packer = m.packer,
                    material = m.material,
                    type = m.type,
                    jo_no = CASE WHEN r.jo_no = '0' THEN m.jo_no ELSE r.jo_no END,
                    gross_weight = m.gross_weight,
                    part_weight = m.part_weight,
                    shot = m.shot,
                    qty_perct = m.qty_perct,
                    qty_order = m.qty_order,
                    wip_opening = m.wip_opening,
                    wip_closing = m.wip_closing,
                    finish_good = m.finish_good,
                    qty_accum = m.qty_accum,
                    act_ct = m.act_ct,
                    sap_ct = m.sap_ct,
                    part_scrap = m.part_scrap
                FROM report r
                INNER JOIN machine_master m ON m.id_machine = r.id_machine
                WHERE r.id_machine = @id_machine
                    AND r.id_type = @id_type
                    AND r.mould = @mould
                    AND r.production_date = @production_date
                    AND r.shift = @shift
                    AND (r.jo_no = '0' OR r.jo_no = @jo_no);
                ";

            var result = new List<object>();

            using var conn = await CreateConnection();
            foreach (var row in reportList)
            {
                await using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@id_machine", row["id_machine"].GetInt32());
                cmd.Parameters.AddWithValue("@packer", row["packer"].GetString());
                cmd.Parameters.AddWithValue("@jo_no", row["jo_no"].GetString());
                cmd.Parameters.AddWithValue("@qty_order", row["qty_order"].GetInt32());
                cmd.Parameters.AddWithValue("@wip_opening", row["wip_opening"].GetInt32());
                cmd.Parameters.AddWithValue("@wip_closing", row["wip_closing"].GetInt32());
                cmd.Parameters.AddWithValue("@finish_good", row["finish_good"].GetInt32());
                cmd.Parameters.AddWithValue("@qty_accum", row["qty_accum"].GetInt32());
                cmd.Parameters.AddWithValue("@part_scrap", (float)row["part_scrap"].GetDouble());

                cmd.Parameters.AddWithValue("@id_type", row["id_type"].GetInt32());
                cmd.Parameters.AddWithValue("@mould", row["mould"].GetInt32());
                cmd.Parameters.AddWithValue("@production_date", production_date);
                cmd.Parameters.AddWithValue("@shift", shift);

                await cmd.ExecuteNonQueryAsync();
                result.Add(row);
            }

            return result;
        }
        public async Task<List<Report>> LoadExcelReport(DateOnly production_date, int shift)
        {
            var sql = @"
                SELECT 
                    ISNULL(id_machine, 1) AS id_machine,
                    ISNULL(shift, 1) AS shift,
                    ISNULL(machine_name, '') AS machine_name,
                    ISNULL(packer, '') AS packer,
                    ISNULL(material, '') AS material,
                    ISNULL(id_type, 123456) AS id_type,
                    ISNULL(mould, 0) AS mould,
                    ISNULL(type, '') AS type,
                    ISNULL(jo_no, '') AS jo_no,
                    ISNULL(qty_perct, 1) AS qty_perct,
                    ISNULL(gross_weight, 0.0) AS gross_weight,
                    ISNULL(part_weight, 0.0) AS part_weight,
                    ISNULL(shot, 0) AS shot,
                    ISNULL(qty_order, 0) AS qty_order,
                    ISNULL(wip_opening, 0) AS wip_opening,
                    ISNULL(wip_closing, 0) AS wip_closing,
                    ISNULL(shift_output, 0) AS shift_output,
                    ISNULL(finish_good, 0) AS finish_good,
                    ISNULL(inward, 0.0) AS inward,
                    ISNULL(qty_accum, 0) AS qty_accum,
                    ISNULL(qty_balance, 0) AS qty_balance,
                    ISNULL(material_used, 0.0) AS material_used,
                    ISNULL(runner, 0.0) AS runner,
                    ISNULL(reject_startup, 0.0) AS reject_startup,
                    ISNULL(reject_startup_per, 0.0) AS reject_startup_per,
                    ISNULL(reject_prod, 0.0) AS reject_prod,
                    ISNULL(reject_prod_per, 0.0) AS reject_prod_per,
                    ISNULL(act_ct, 0.0) AS act_ct,
                    ISNULL(production_running, 0.0) AS production_running,
                    ISNULL(sap_ct, 0.0) AS sap_ct,
                    ISNULL(change_full_set, 0.0) AS change_full_set,
                    ISNULL(change_half_set, 0.0) AS change_half_set,
                    ISNULL(change_parts, 0.0) AS change_parts,
                    ISNULL(maintenance_dt, 0.0) AS maintenance_dt,
                    ISNULL(technician_dt, 0.0) AS technician_dt,
                    ISNULL(production_dt, 0.0) AS production_dt,
                    ISNULL(remark, '') AS remark,
                    ISNULL(unallocated, 0.0) AS unallocated,
                    ISNULL(part_scrap, 0.0) AS part_scrap,
                    ISNULL(reject_purging, 0.0) AS reject_purging,
                    ISNULL(reject_preform, 0.0) AS reject_preform,
                    ISNULL(reject_total_pcs, 0) AS reject_total_pcs
                FROM report
                WHERE production_date = @production_date 
                AND shift = @shift";

            var result = new List<Report>();

            using var conn = await CreateConnection();
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
                    MachineName = Convert.ToString(reader["machine_name"]),
                    Packer = Convert.ToString(reader["packer"]),
                    Material = Convert.ToString(reader["material"]),
                    IdType = Convert.ToInt32(reader["id_type"]),
                    Mould = Convert.ToInt32(reader["mould"]),
                    Type = Convert.ToString(reader["type"]),
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
        public async Task<object> OEECalculation(DateOnly start_date, DateOnly end_date, int shift)
        {
            var time = DateTime.Now;
            var (productionDate, current_shift) = GetProductionDate(time);

            string sql;

            if (start_date == end_date && start_date == productionDate)
            {
                sql = @"
                    WITH CombinedLogs AS (
                        SELECT 1 AS id_machine, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_1
                        UNION ALL SELECT 2, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_2
                        UNION ALL SELECT 3, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_3
                        UNION ALL SELECT 4, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_4
                        UNION ALL SELECT 5, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_5
                        UNION ALL SELECT 6, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_6
                        UNION ALL SELECT 7, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_7
                        UNION ALL SELECT 8, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_8
                        UNION ALL SELECT 9, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_9
                        UNION ALL SELECT 10, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_10
                        UNION ALL SELECT 11, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_11
                        UNION ALL SELECT 12, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_12
                        UNION ALL SELECT 13, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_13
                        UNION ALL SELECT 14, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_14
                        UNION ALL SELECT 15, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_15
                        UNION ALL SELECT 16, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_16
                        UNION ALL SELECT 17, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_17
                        UNION ALL SELECT 18, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_18
                        UNION ALL SELECT 19, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_19
                        UNION ALL SELECT 20, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_20
                        UNION ALL SELECT 21, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_21
                        UNION ALL SELECT 22, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_22
                        UNION ALL SELECT 23, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_23
                        UNION ALL SELECT 24, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_24
                        UNION ALL SELECT 25, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date FROM machine_log_25
                    ),
                    MachineAgg AS (
                        SELECT
                            id_machine,
                            machine_name,
                            id_type,
                            mould,
                            SUM(shot) as shot,
                            ISNULL(SUM(CASE WHEN category='PRODUCTION RUNNING' THEN (DATEDIFF(SECOND, start, ISNULL(finish, GETDATE())) / 3600.0) ELSE 0 END),0) AS run_time,
                            ISNULL(SUM(CASE WHEN category NOT IN ('PRODUCTION RUNNING') THEN (DATEDIFF(SECOND, start, ISNULL(finish, GETDATE())) / 3600.0) ELSE 0 END),0) AS down_time,
                            AVG(NULLIF(act_ct, 0)) AS act_ct
                        FROM CombinedLogs
                        WHERE production_date=@today AND shift=@shift
                        GROUP BY id_machine, machine_name, id_type, mould
                    ),
                    JoinedWithSap AS (
                        SELECT
                            m.id_machine,
                            m.machine_name,
                            m.id_type,
                            m.mould,
                            m.run_time,
                            m.down_time,
                            ISNULL(m.act_ct,0) AS act_ct,
                            ISNULL(s.sap_ct,0) AS sap_ct,
                            ISNULL(m.shot * s.qty_perct * s.part_weight,0) AS material_used
                        FROM MachineAgg m
                        LEFT JOIN sap s ON s.id_type=m.id_type AND s.mould=m.mould
                    ),
                    JoinedWithReject AS (
                        SELECT
                            j.id_machine,
                            j.machine_name,
                            j.id_type,
                            j.mould,
                            ISNULL(j.run_time,0) AS run_time,
                            ISNULL(j.down_time,0) AS down_time,
                            ISNULL(j.act_ct,0) AS act_ct,
                            ISNULL(j.sap_ct,0) AS sap_ct,
                            ISNULL(j.material_used,0) AS material_used,
                            ISNULL(r.total_weight,0) AS reject_weight
                        FROM JoinedWithSap j
                        LEFT JOIN reject r
                            ON r.id_machine=j.id_machine AND r.id_type=j.id_type AND r.mould=j.mould
                            AND r.production_date=@today AND r.shift=@shift
                    )
                    SELECT
                        id_machine,
                        machine_name,
                        ISNULL(SUM(run_time),0) AS run_time,
                        ISNULL(SUM(down_time),0) AS down_time,
                        CASE 
                            WHEN (SUM(down_time) + SUM(run_time)) = 0 OR id_type = 123456 THEN 0 
                            WHEN (SUM(run_time) * 100.0 / (SUM(down_time) + SUM(run_time))) < 0 THEN 0
                            ELSE (SUM(run_time) * 100.0 / (SUM(down_time) + SUM(run_time))) 
                        END AS availability,
                        CASE 
                            WHEN AVG(act_ct) = 0 OR id_type = 123456 THEN 0 
                            WHEN (MAX(sap_ct) * 100.0 / AVG(act_ct)) < 0 THEN 0
                            ELSE (MAX(sap_ct) * 100.0 / AVG(act_ct)) 
                        END AS performance,
                        CASE 
                            WHEN SUM(run_time) = 0 OR SUM(material_used) = 0 OR id_type = 123456 THEN 0
                            WHEN ((SUM(material_used) - SUM(reject_weight)) * 1.0 / SUM(material_used)) < 0 THEN 0
                            ELSE ((SUM(material_used) - SUM(reject_weight)) * 1.0 / SUM(material_used)) * 100
                        END AS quality,
                        CASE 
                            WHEN (SUM(run_time) + SUM(down_time)) = 0 OR AVG(act_ct) = 0 OR SUM(material_used) = 0 OR id_type = 123456 THEN 0
                            ELSE ((SUM(run_time)*1.0/(SUM(down_time) + SUM(run_time)))*(MAX(sap_ct)/AVG(act_ct))*((SUM(material_used)-SUM(reject_weight))*1.0/SUM(material_used))*100)
                        END AS oee
                    FROM JoinedWithReject
                    GROUP BY id_machine, machine_name, id_type
                    ORDER BY id_machine;";
            }
            else
            {
                sql = @"
                    WITH ReportAgg AS (
                        SELECT
                            r.id_machine,
                            r.machine_name,
                            r.id_type,
                            r.mould,
                            ISNULL(SUM(r.production_running), 0) AS run_time,
                            ISNULL(SUM(
                                ISNULL(r.change_full_set,0) +
                                ISNULL(r.change_half_set,0) +
                                ISNULL(r.change_parts,0) +
                                ISNULL(r.maintenance_dt,0) +
                                ISNULL(r.technician_dt,0) +
                                ISNULL(r.production_dt,0)
                            ), 0) AS down_time,
                            ISNULL(SUM(r.unallocated), 0) AS unallocated,
                            ISNULL(AVG(NULLIF(r.act_ct,0)), 0) AS act_ct,
                            ISNULL(MAX(r.sap_ct), 0) AS sap_ct,
                            ISNULL(SUM(r.material_used), 0) AS material_used,
                            ISNULL(SUM(
                                ISNULL(r.reject_startup,0) +
                                ISNULL(r.reject_prod,0) +
                                ISNULL(r.reject_purging,0) +
                                ISNULL(r.reject_preform,0)
                            ), 0) AS reject_weight
                        FROM report r
                        WHERE r.production_date BETWEEN @start_date AND @end_date AND id_machine <> 26
                        GROUP BY r.id_machine, r.machine_name, r.id_type, r.mould
                    ),
                    MachineSummary AS (
                        SELECT
                            id_machine,
                            machine_name,
                            SUM(run_time) AS run_time,
                            SUM(down_time) AS down_time,
                            SUM(unallocated) AS unallocated,
                            ISNULL(SUM(act_ct * run_time) / NULLIF(SUM(run_time),0),0) AS act_ct,
                            MAX(sap_ct) AS sap_ct,
                            SUM(material_used) AS material_used,
                            SUM(reject_weight) AS reject_weight
                        FROM ReportAgg
                        GROUP BY id_machine, machine_name
                    )
                    SELECT
                        id_machine,
                        machine_name,
                        run_time,
                        down_time,
                        unallocated,
                        act_ct,
                        sap_ct,
                        material_used,
                        reject_weight,
                        CASE 
                            WHEN (run_time + down_time) = 0 THEN 0
                            WHEN (run_time * 1.0 / (run_time + down_time)) < 0 THEN 0
                            ELSE (run_time * 1.0 / (run_time + down_time)) * 100
                        END AS availability,
                        CASE 
                            WHEN act_ct = 0 THEN 0
                            WHEN (sap_ct * 1.0 / act_ct) < 0 THEN 0
                            ELSE (sap_ct * 1.0 / act_ct) * 100
                        END AS performance,
                        CASE
                            WHEN material_used = 0 THEN 0
                            WHEN ((material_used - reject_weight) * 1.0 / material_used) < 0 THEN 0
                            ELSE ((material_used - reject_weight) * 1.0 / material_used) * 100
                        END AS quality,
                        CASE 
                            WHEN (run_time + down_time) = 0 
                              OR act_ct = 0 
                              OR material_used = 0 THEN 0
                            WHEN (
                                (run_time * 1.0 / (run_time + down_time)) *
                                (sap_ct * 1.0 / act_ct) *
                                ((material_used - reject_weight) * 1.0 / material_used)
                            ) < 0 THEN 0
                            ELSE
                                (run_time * 1.0 / (run_time + down_time)) *
                                (sap_ct * 1.0 / act_ct) *
                                ((material_used - reject_weight) * 1.0 / material_used) * 100
                        END AS oee
                    FROM MachineSummary
                    ORDER BY id_machine;";
            }

            var result = new List<object>();
            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@today", productionDate);
            cmd.Parameters.AddWithValue("@start_date", start_date);
            cmd.Parameters.AddWithValue("@end_date", end_date);
            cmd.Parameters.AddWithValue("@shift", shift);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id_machine = Convert.ToInt32(reader["id_machine"]),
                    machine_name = Convert.ToString(reader["machine_name"]),
                    run_time = Convert.ToInt32(reader["run_time"]),
                    down_time = Convert.ToInt32(reader["down_time"]),
                    availability = Convert.ToSingle(reader["availability"]),
                    performance = Convert.ToSingle(reader["performance"]),
                    quality = Convert.ToSingle(reader["quality"]),
                    oee = Convert.ToSingle(reader["oee"])
                });
            }
            return result;
        }
        public async Task<object> LoadReject(DateOnly start_date, DateOnly end_date)
        {
            var time = DateTime.Now;
            var (productionDate, current_shift) = GetProductionDate(time);

            var sql = "";

            if (start_date == productionDate && end_date == productionDate)
            {
                sql = @"
                    SELECT TOP 10
	                    reject.id_type,
                        type, 
                        COALESCE(SUM(total_weight), 0) AS total_reject
                    FROM reject
                    LEFT JOIN sap 
                        ON sap.id_type = reject.id_type AND sap.mould = reject.mould
                    WHERE production_date = @today AND shift = @shift AND reject.id_type <> 123456
                    GROUP BY reject.id_type, type
                    ORDER BY COALESCE(SUM(total_weight), 0) DESC;";
            }
            else
            {
                sql = @"
                    SELECT TOP 10
                        id_type,
                        type,
                        COALESCE(SUM(reject_startup + reject_prod + reject_purging + reject_preform), 0) AS total_reject
                    FROM report
                    WHERE production_date BETWEEN @start_date AND @end_date AND id_type <> 123456
                    GROUP BY id_type, type
                    ORDER BY COALESCE(SUM(reject_startup + reject_prod + reject_purging + reject_preform), 0) DESC;";
            }

            var result = new List<object>();
            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@today", productionDate);
            cmd.Parameters.AddWithValue("@shift", current_shift);
            cmd.Parameters.AddWithValue("@start_date", start_date);
            cmd.Parameters.AddWithValue("@end_date", end_date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id_type = Convert.ToInt32(reader["id_type"]),
                    type = Convert.ToString(reader["type"]),
                    total_reject = Convert.ToSingle(reader["total_reject"]),
                });
            }

            return result;
        }
        public async Task<object> LoadOutput(DateOnly start_date, DateOnly end_date)
        {
            var time = DateTime.Now;
            var (productionDate, current_shift) = GetProductionDate(time);
            var sql = "";

            if (start_date == productionDate && end_date == productionDate)
            {
                sql = @"
                    SELECT TOP 10
                        id_type,
                        type,
                        COALESCE(
                            CASE 
                                WHEN part_weight > 0 THEN (shift_output/part_weight)
                                ELSE 0 
                            END, 0) AS total_output
                    FROM machine_master
                    WHERE id_type <> 123456 
                    ORDER BY 
                        CASE 
                            WHEN part_weight > 0 THEN (shift_output/part_weight)
                            ELSE 0 
                        END DESC;";
            }
            else
            {
                sql = @"
                    SELECT TOP 10
                        id_type,
                        type,
                        COALESCE(SUM(
                            CASE 
                                WHEN part_weight > 0 THEN (shift_output/part_weight)
                                ELSE 0 
                            END), 0) AS total_output
                    FROM report
                    WHERE production_date BETWEEN @start_date AND @end_date AND id_type <> 123456
                    GROUP BY id_type, type
                    ORDER BY COALESCE(SUM(
                        CASE 
                            WHEN part_weight > 0 THEN (shift_output/part_weight)
                            ELSE 0 
                        END), 0) DESC;";
            }

            var result = new List<object>();
            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@start_date", start_date);
            cmd.Parameters.AddWithValue("@end_date", end_date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id_type = Convert.ToInt32(reader["id_type"]),
                    type = Convert.ToString(reader["type"]),
                    total_output = Convert.ToSingle(reader["total_output"])
                });
            }

            return result;
        }
        public async Task<object> LoadDowntime(DateOnly start_date, DateOnly end_date)
        {
            var sql = @"
                WITH CombinedLogs AS (
                    SELECT 1 AS id_machine, id_type, mould, category, start, finish, production_date FROM machine_log_1
                    UNION ALL SELECT 2, id_type, mould, category, start, finish, production_date FROM machine_log_2
                    UNION ALL SELECT 3, id_type, mould, category, start, finish, production_date FROM machine_log_3
                    UNION ALL SELECT 4, id_type, mould, category, start, finish, production_date FROM machine_log_4
                    UNION ALL SELECT 5, id_type, mould, category, start, finish, production_date FROM machine_log_5
                    UNION ALL SELECT 6, id_type, mould, category, start, finish, production_date FROM machine_log_6
                    UNION ALL SELECT 7, id_type, mould, category, start, finish, production_date FROM machine_log_7
                    UNION ALL SELECT 8, id_type, mould, category, start, finish, production_date FROM machine_log_8
                    UNION ALL SELECT 9, id_type, mould, category, start, finish, production_date FROM machine_log_9
                    UNION ALL SELECT 10, id_type, mould, category, start, finish, production_date FROM machine_log_10
                    UNION ALL SELECT 11, id_type, mould, category, start, finish, production_date FROM machine_log_11
                    UNION ALL SELECT 12, id_type, mould, category, start, finish, production_date FROM machine_log_12
                    UNION ALL SELECT 13, id_type, mould, category, start, finish, production_date FROM machine_log_13
                    UNION ALL SELECT 14, id_type, mould, category, start, finish, production_date FROM machine_log_14
                    UNION ALL SELECT 15, id_type, mould, category, start, finish, production_date FROM machine_log_15
                    UNION ALL SELECT 16, id_type, mould, category, start, finish, production_date FROM machine_log_16
                    UNION ALL SELECT 17, id_type, mould, category, start, finish, production_date FROM machine_log_17
                    UNION ALL SELECT 18, id_type, mould, category, start, finish, production_date FROM machine_log_18
                    UNION ALL SELECT 19, id_type, mould, category, start, finish, production_date FROM machine_log_19
                    UNION ALL SELECT 20, id_type, mould, category, start, finish, production_date FROM machine_log_20
                    UNION ALL SELECT 21, id_type, mould, category, start, finish, production_date FROM machine_log_21
                    UNION ALL SELECT 22, id_type, mould, category, start, finish, production_date FROM machine_log_22
                    UNION ALL SELECT 23, id_type, mould, category, start, finish, production_date FROM machine_log_23
                    UNION ALL SELECT 24, id_type, mould, category, start, finish, production_date FROM machine_log_24
                    UNION ALL SELECT 25, id_type, mould, category, start, finish, production_date FROM machine_log_25
                )
                SELECT TOP 10
                    sap.id_type,
                    sap.type,
                    SUM(DATEDIFF(SECOND, start, ISNULL(finish, GETDATE()))) / 3600.0 AS hours
                FROM CombinedLogs ml
                LEFT JOIN sap 
                    ON sap.id_type = ml.id_type 
                    AND sap.mould = ml.mould
                WHERE production_date BETWEEN @start_date AND @end_date
                    AND ml.id_type <> 123456
                    AND category NOT IN ('PRODUCTION RUNNING', 'NO SCHEDULE', 'SCHEDULED DOWNTIME', '')
                GROUP BY sap.id_type, sap.type
                ORDER BY hours DESC;";

            var result = new List<object>();

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@start_date", start_date);
            cmd.Parameters.AddWithValue("@end_date", end_date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id_type = Convert.ToInt32(reader["id_type"]),
                    type = Convert.ToString(reader["type"]),
                    hours = Convert.ToDouble(reader["hours"])
                });
            }

            return result;
        }
        public async Task<object> LoadPrevReport(DateOnly production_date, int shift)
        {
            var sql = @"
                SELECT 
                    ISNULL(id_machine, 1) AS id_machine,
                    ISNULL(shift, 1) AS shift,
                    ISNULL(machine_name, '') AS machine_name,
                    ISNULL(packer, '') AS packer,
                    ISNULL(material, '') AS material,
                    ISNULL(id_type, 123456) AS id_type,
                    ISNULL(mould, 0) AS mould,
                    ISNULL(type, '') AS type,
                    ISNULL(jo_no, '') AS jo_no,
                    ISNULL(qty_perct, 1) AS qty_perct,
                    ISNULL(gross_weight, 0.0) AS gross_weight,
                    ISNULL(part_weight, 0.0) AS part_weight,
                    ISNULL(shot, 0) AS shot,
                    ISNULL(qty_order, 0) AS qty_order,
                    ISNULL(wip_opening, 0) AS wip_opening,
                    ISNULL(wip_closing, 0) AS wip_closing,
                    ISNULL(shift_output, 0) AS shift_output,
                    ISNULL(finish_good, 0) AS finish_good,
                    ISNULL(inward, 0.0) AS inward,
                    ISNULL(qty_accum, 0) AS qty_accum,
                    ISNULL(qty_balance, 0) AS qty_balance,
                    ISNULL(material_used, 0.0) AS material_used,
                    ISNULL(runner, 0.0) AS runner,
                    ISNULL(reject_startup, 0.0) AS reject_startup,
                    ISNULL(reject_startup_per, 0.0) AS reject_startup_per,
                    ISNULL(reject_prod, 0.0) AS reject_prod,
                    ISNULL(reject_prod_per, 0.0) AS reject_prod_per,
                    ISNULL(act_ct, 0.0) AS act_ct,
                    ISNULL(production_running, 0.0) AS production_running,
                    ISNULL(sap_ct, 0.0) AS sap_ct,
                    ISNULL(change_full_set, 0.0) AS change_full_set,
                    ISNULL(change_half_set, 0.0) AS change_half_set,
                    ISNULL(change_parts, 0.0) AS change_parts,
                    ISNULL(maintenance_dt, 0.0) AS maintenance_dt,
                    ISNULL(technician_dt, 0.0) AS technician_dt,
                    ISNULL(production_dt, 0.0) AS production_dt,
                    ISNULL(remark, '') AS remark,
                    ISNULL(unallocated, 0.0) AS unallocated,
                    ISNULL(part_scrap, 0.0) AS part_scrap,
                    ISNULL(reject_purging, 0.0) AS reject_purging,
                    ISNULL(reject_preform, 0.0) AS reject_preform,
                    ISNULL(reject_total_pcs, 0) AS reject_total_pcs
                FROM report
                WHERE production_date = @production_date 
                AND shift = @shift";

            var result = new List<object>();

            using var conn = await CreateConnection();
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
        public async Task<object> UpdatePrevReport(List<Dictionary<string, JsonElement>> reportList, DateOnly production_date, int shift)
        {
            var sql = @"
                UPDATE report 
                SET packer = @packer, 
	                material = @material, 
	                type = @type, 
	                jo_no = CASE WHEN r.jo_no = '0' THEN @jo_no ELSE r.jo_no END, 
	                qty_perct = @qty_perct,
	                gross_weight = @gross_weight,
	                part_weight = @part_weight,
	                shot = @shot_accum,
	                qty_order = @qty_order,
	                wip_opening = @wip_opening,
	                wip_closing = @wip_closing,
	                finish_good = @finish_good,
	                reject_startup = @reject_startup,
	                reject_prod = @reject_prod,
	                act_ct = @act_ct,
	                sap_ct = @sap_ct,
	                remark = @remark,
	                part_scrap = @part_scrap,
	                reject_labelling = @reject_labelling,
	                reject_purging = @reject_purging,
	                reject_preform = @reject_preform,
	                reject_total_pcs = @reject_total_pcs
                WHERE id_machine = @id_machine
                    AND id_type = @id_type
                    AND mould = @mould
                    AND production_date = @production_date
                    AND shift = @shift
                    AND (r.jo_no = '0' OR r.jo_no = @jo_no);
                ";

            var result = new List<object>();

            using var conn = await CreateConnection();
            foreach (var row in reportList)
            {
                await using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@packer", row["packer"].GetString());
                cmd.Parameters.AddWithValue("@material", row["material"].GetString());
                cmd.Parameters.AddWithValue("@type", row["type"].GetString());
                cmd.Parameters.AddWithValue("@jo_no", row["jo_no"].GetString());
                cmd.Parameters.AddWithValue("@qty_perct", row["qty_perct"].GetInt32());
                cmd.Parameters.AddWithValue("@gross_weight", (float)row["gross_weight"].GetDouble());
                cmd.Parameters.AddWithValue("@part_weight", (float)row["part_weight"].GetDouble());
                cmd.Parameters.AddWithValue("@shot_accum", row["shot_accum"].GetInt32());
                cmd.Parameters.AddWithValue("@qty_order", row["qty_order"].GetInt32());
                cmd.Parameters.AddWithValue("@wip_opening", row["wip_opening"].GetInt32());
                cmd.Parameters.AddWithValue("@wip_closing", row["wip_closing"].GetInt32());
                cmd.Parameters.AddWithValue("@finish_good", row["finish_good"].GetInt32());
                cmd.Parameters.AddWithValue("@qty_accum", row["qty_accum"].GetInt32());
                cmd.Parameters.AddWithValue("@reject_startup", (float)row["reject_startup"].GetDouble());
                cmd.Parameters.AddWithValue("@reject_prod", (float)row["reject_prod"].GetDouble());
                cmd.Parameters.AddWithValue("@act_ct", (float)row["act_ct"].GetDouble());
                cmd.Parameters.AddWithValue("@production_running", (float)row["production_running"].GetDouble());
                cmd.Parameters.AddWithValue("@sap_ct", (float)row["sap_ct"].GetDouble());
                cmd.Parameters.AddWithValue("@change_full_set", (float)row["change_full_set"].GetDouble());
                cmd.Parameters.AddWithValue("@change_half_set", (float)row["change_half_set"].GetDouble());
                cmd.Parameters.AddWithValue("@change_parts", (float)row["change_parts"].GetDouble());
                cmd.Parameters.AddWithValue("@maintenance_dt", (float)row["maintenance_dt"].GetDouble());
                cmd.Parameters.AddWithValue("@technician_dt", (float)row["technician_dt"].GetDouble());
                cmd.Parameters.AddWithValue("@production_dt", (float)row["production_dt"].GetDouble());
                cmd.Parameters.AddWithValue("@unallocated", (float)row["unallocated"].GetDouble());
                cmd.Parameters.AddWithValue("@remark", row["remark"].GetString());
                cmd.Parameters.AddWithValue("@part_scrap", (float)row["part_scrap"].GetDouble());
                cmd.Parameters.AddWithValue("@reject_labelling", (float)row["reject_labelling"].GetDouble());
                cmd.Parameters.AddWithValue("@reject_purging", (float)row["reject_purging"].GetDouble());
                cmd.Parameters.AddWithValue("@reject_preform", (float)row["reject_preform"].GetDouble());
                cmd.Parameters.AddWithValue("@reject_total_pcs", row["reject_total_pcs"].GetInt32());

                await cmd.ExecuteNonQueryAsync();
                result.Add(row);
            }

            return result;
        }
        public async Task<object> LoadMachineMaster()
        {
            var time = DateTime.Now;
            var (productionDate, shift) = GetProductionDate(time);

            var sql = @"
                WITH latest_logs AS (
                    SELECT TOP 1 1 as id_machine, machine_name, start, finish, category, problem, mould_category FROM machine_log_1 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 2, machine_name, start, finish, category, problem, mould_category FROM machine_log_2 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 3, machine_name, start, finish, category, problem, mould_category FROM machine_log_3 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 4, machine_name, start, finish, category, problem, mould_category FROM machine_log_4 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 5, machine_name, start, finish, category, problem, mould_category FROM machine_log_5 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 6, machine_name, start, finish, category, problem, mould_category FROM machine_log_6 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 7, machine_name, start, finish, category, problem, mould_category FROM machine_log_7 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 8, machine_name, start, finish, category, problem, mould_category FROM machine_log_8 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 9, machine_name, start, finish, category, problem, mould_category FROM machine_log_9 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 10, machine_name, start, finish, category, problem, mould_category FROM machine_log_10 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 11, machine_name, start, finish, category, problem, mould_category FROM machine_log_11 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 12, machine_name, start, finish, category, problem, mould_category FROM machine_log_12 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 13, machine_name, start, finish, category, problem, mould_category FROM machine_log_13 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 14, machine_name, start, finish, category, problem, mould_category FROM machine_log_14 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 15, machine_name, start, finish, category, problem, mould_category FROM machine_log_15 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 16, machine_name, start, finish, category, problem, mould_category FROM machine_log_16 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 17, machine_name, start, finish, category, problem, mould_category FROM machine_log_17 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 18, machine_name, start, finish, category, problem, mould_category FROM machine_log_18 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 19, machine_name, start, finish, category, problem, mould_category FROM machine_log_19 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 20, machine_name, start, finish, category, problem, mould_category FROM machine_log_20 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 21, machine_name, start, finish, category, problem, mould_category FROM machine_log_21 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 22, machine_name, start, finish, category, problem, mould_category FROM machine_log_22 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 23, machine_name, start, finish, category, problem, mould_category FROM machine_log_23 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 24, machine_name, start, finish, category, problem, mould_category FROM machine_log_24 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 25, machine_name, start, finish, category, problem, mould_category FROM machine_log_25 ORDER BY start DESC
                    UNION ALL
                    SELECT TOP 1 26, machine_name, start, finish, category, problem, mould_category FROM machine_log_26 ORDER BY start DESC
                )
                SELECT
                    mm.id_machine, 
                    mm.machine_name, 
                    COALESCE(mm.packer, '') AS packer, 
                    COALESCE(mm.id_type, 123456) AS id_type, 
                    COALESCE(mm.mould, 0) AS mould,
                    COALESCE(mm.type, '') AS type,
                    CAST(COALESCE(mm.status_start, 0) AS BIT) AS status_start,
                    CAST(COALESCE(mm.status_off, 1) AS BIT) AS status_off,
                    COALESCE(mm.qty_perct, 0) as qty_perct,
                    COALESCE(mm.act_ct, 0) as act_ct,
                    COALESCE(mm.sap_ct, 0) as sap_ct,
                    COALESCE(mm.shot, 0) AS shot,
                    COALESCE(mm.material, '') AS material,
                    COALESCE(mm.part_weight, 0) AS part_weight,
                    COALESCE(mm.visual_qc, 0) AS visual_qc,
                    COALESCE(mm.measure_qc, 0) AS measure_qc,
                    COALESCE(mm.shift_output, 0) AS output,
                    CASE 
                        WHEN COALESCE(mm.id_type, 123456) = 123456 AND COALESCE(mm.mould, 0) = 0 THEN 0
                        WHEN COALESCE(mm.sap_ct, 0) = 0 THEN 0
                        ELSE CAST(
                            (DATEDIFF(SECOND, 
                                CASE 
                                    WHEN CAST(GETDATE() AS TIME) >= '06:00:00' AND CAST(GETDATE() AS TIME) < '18:00:00' 
                                        THEN CAST(CAST(GETDATE() AS DATE) AS DATETIME) + CAST('06:00:00' AS DATETIME)
                                    WHEN CAST(GETDATE() AS TIME) >= '18:00:00' 
                                        THEN CAST(CAST(GETDATE() AS DATE) AS DATETIME) + CAST('18:00:00' AS DATETIME)
                                    ELSE CAST(CAST(DATEADD(DAY, -1, GETDATE()) AS DATE) AS DATETIME) + CAST('18:00:00' AS DATETIME)
                                END,
                                GETDATE()
                            ) / NULLIF(mm.sap_ct, 0)) * mm.qty_perct 
                        AS INT)
                    END AS planned_output,
                    COALESCE(r.total_weight, 0) AS reject_weight,
                    CASE 
                        WHEN COALESCE(mm.part_weight, 0) = 0 THEN 0
                        ELSE CAST(COALESCE(r.total_weight, 0) / NULLIF(mm.part_weight, 0) AS INT)
                    END AS reject_pcs,
                    ll.start, 
                    COALESCE(ll.finish,  CAST(GETDATE() AS DATETIME)) AS finish,
                    CASE 
                        WHEN (ll.category IS NULL OR ll.category = '') 
                             AND mm.status_start = 1 
                             AND mm.status_off = 1 
                        THEN 'PRODUCTION RUNNING'
                        ELSE COALESCE(ll.category, '')
                    END AS category,
                    COALESCE(ll.problem, '') AS problem,
                    COALESCE(ll.mould_category, 0) AS mould_category
                FROM machine_master mm
                JOIN latest_logs ll ON mm.id_machine = ll.id_machine
                LEFT JOIN reject r 
                    ON r.id_machine = mm.id_machine 
                    AND r.id_type = mm.id_type 
                    AND r.mould = mm.mould
                    AND r.production_date = @production_date
                    AND r.shift = @shift";

            var result = new List<object>();

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id_machine = Convert.ToInt32(reader["id_machine"]),
                    machine_name = Convert.ToString(reader["machine_name"]),
                    packer = Convert.ToString(reader["packer"]),
                    material = Convert.ToString(reader["material"]),
                    id_type = Convert.ToInt32(reader["id_type"]),
                    mould = Convert.ToInt32(reader["mould"]),
                    type = Convert.ToString(reader["type"]),
                    status_start = Convert.ToBoolean(reader["status_start"]),
                    status_off = Convert.ToBoolean(reader["status_off"]),
                    qty_perct = Convert.ToInt32(reader["qty_perct"]),
                    act_ct = Convert.ToSingle(reader["act_ct"]),
                    sap_ct = Convert.ToSingle(reader["sap_ct"]),
                    shot = Convert.ToInt32(reader["shot"]),
                    output = Convert.ToInt32(reader["output"]),
                    part_weight = Convert.ToSingle(reader["part_weight"]),
                    planned_output = Convert.ToInt32(reader["planned_output"]),
                    reject_weight = Convert.ToSingle(reader["reject_weight"]),
                    reject_pcs = Convert.ToSingle(reader["reject_pcs"]),
                    start = Convert.ToDateTime(reader["start"]),
                    finish = Convert.ToDateTime(reader["finish"]),
                    category = Convert.ToString(reader["category"]),
                    problem = Convert.ToString(reader["problem"]),
                    visual_qc = Convert.ToBoolean(reader["visual_qc"]),
                    measure_qc = Convert.ToBoolean(reader["measure_qc"]),
                    mould_category = Convert.ToInt32(reader["mould_category"]),
                    color = CategoryColorHelper.GetColorByCategory(Convert.ToString(reader["category"])),
                });
            }

            return result;
        }
        public async Task UpdateMouldChange(Dictionary<string, JsonElement> payload)
        {
            var time = DateTime.Now;
            var (productionDate, shift) = GetProductionDate(time);
            var master = _lastMachineMaster.GetValueOrDefault(payload["id_machine"].GetInt32()).plcData;

            var total_weight = master.reject_panelling + master.reject_lumpy + master.reject_black_dot + master.reject_burst + master.reject_startup + master.reject_preform + master.reject_purging + master.reject_others;
            var tableName = $"machine_log_{payload["id_machine"].GetInt32()}";

            var sql = $@"
                IF NOT EXISTS (SELECT 1 FROM reject WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
                BEGIN
                    INSERT INTO reject (id_machine, machine_name, id_type, mould, total_weight, reject_panelling, reject_lumpy, reject_black_dot, reject_burst, reject_startup, reject_preform, reject_purging, reject_others, shift, production_date) 
                    VALUES (@id_machine, @machine_name, @id_type, @mould, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, @shift, @production_date);
                END
                ELSE
                BEGIN
                    UPDATE reject SET total_weight = @total_weight, reject_panelling = @reject_panelling, reject_lumpy = @reject_lumpy, reject_black_dot = @reject_black_dot, reject_burst = @reject_burst, reject_startup = @reject_startup, reject_preform = @reject_preform, reject_purging = @reject_purging, reject_others = @reject_others
                    WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND shift = @shift and production_date = @production_date
                END

                IF EXISTS (SELECT 1 FROM [{tableName}] WHERE finish IS NULL)
                BEGIN
                    UPDATE [{tableName}] SET finish = @time, shot = @shot WHERE finish IS NULL;
                END

                INSERT INTO [{tableName}] (machine_name, id_type, mould, start, shot, category, problem, mould_category, shift, production_date) 
                VALUES (@machine_name, @id_type, @mould, @time, 0, @category, @problem, @mould_category, @shift, @production_date);

                IF NOT EXISTS (SELECT 1 FROM report WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
                BEGIN
                    INSERT INTO report (id_machine, machine_name, time, shift, production_date, id_type, mould) 
                    VALUES (@id_machine, @machine_name, @time, @shift, @production_date, @id_type, @mould);
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

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id_machine", payload["id_machine"].GetInt32());
            cmd.Parameters.AddWithValue("@machine_name", payload["machine_name"].GetString());
            cmd.Parameters.AddWithValue("@id_type", payload["id_type"].GetInt32());
            cmd.Parameters.AddWithValue("@mould", payload["mould"].GetInt32());
            cmd.Parameters.AddWithValue("@shot", master.shot);
            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);
            cmd.Parameters.AddWithValue("@time", time);
            cmd.Parameters.AddWithValue("@category", master.stop_category);
            cmd.Parameters.AddWithValue("@problem", master.remark);
            cmd.Parameters.AddWithValue("@total_weight", total_weight);
            cmd.Parameters.AddWithValue("@reject_panelling", master.reject_panelling);
            cmd.Parameters.AddWithValue("@reject_lumpy", master.reject_lumpy);
            cmd.Parameters.AddWithValue("@reject_black_dot", master.reject_black_dot);
            cmd.Parameters.AddWithValue("@reject_burst", master.reject_burst);
            cmd.Parameters.AddWithValue("@reject_startup", master.reject_startup);
            cmd.Parameters.AddWithValue("@reject_preform", master.reject_preform);
            cmd.Parameters.AddWithValue("@reject_purging", master.reject_purging);
            cmd.Parameters.AddWithValue("@reject_others", master.reject_others);
            cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
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
        public async Task<object> LoadAttendance()
        {
            var time = DateTime.Now;
            var (productionDate, shift) = GetProductionDate(time);

            var sql = @"
                SELECT * FROM staff_list WHERE @production_date BETWEEN start_date AND end_date";

            var result = new List<object>();

            using var conn = await CreateConnection();
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
        public async Task<object> LoadSAP()
        {
            var sql = @"
                SELECT * FROM sap";

            var result = new List<object>();

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
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
                    sap_ct = Convert.ToSingle(reader["sap_ct"])
                });
            }

            return result;
        }
        public async Task UpdateSAP(Dictionary<string, JsonElement> SAPList)
        {
            var sql = @"
                UPDATE sap 
                SET id_type = @id_type,
                    mould = @mould,
                    type = @type,
                    qty_perct = @qty_perct,
                    process = @process,
                    material = @material,
                    part_weight = @part_weight,
                    tolerance = @tolerance,
                    gross_weight = @gross_weight,
                    sap_ct = @sap_ct
                WHERE id_type = @keys_id_type AND mould = @keys_mould

                UPDATE m
                SET 
                    m.material     = s.material,
                    m.type         = s.type,
                    m.qty_perct    = s.qty_perct,
                    m.sap_ct       = s.sap_ct,
                    m.part_weight  = s.part_weight,
                    m.gross_weight = s.gross_weight
                FROM machine_master AS m
                JOIN sap AS s 
                    ON s.id_type = @id_type 
                    AND s.mould   = @mould
                WHERE m.id_type = s.id_type
                    AND m.mould   = s.mould;
                ";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Clear();
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
            var sql = @"DELETE FROM sap WHERE id_type = @id_type AND mould = @mould";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@id_type", id_type);
            cmd.Parameters.AddWithValue("@mould", mould);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task InsertSAP(Dictionary<string, JsonElement> SAPList)
        {
            var sql = @"
                INSERT INTO sap (id_type, mould, type, qty_perct, process, material, part_weight, tolerance, gross_weight, sap_ct)
                VALUES (@id_type, @mould, @type, @qty_perct, @process, @material, @part_weight, @tolerance, @gross_weight, @sap_ct)";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Clear();
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
            using var conn = await CreateConnection();
            await using var transaction = conn.BeginTransaction();

            try
            {
                var deleteSql = "DELETE FROM sap";
                await using (var deleteCmd = new SqlCommand(deleteSql, conn, transaction))
                {
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                var insertSql = @"
                    INSERT INTO sap (id_type, mould, type, qty_perct, process, material, part_weight, tolerance, gross_weight, sap_ct)
                    VALUES (@id_type, @mould, @type, @qty_perct, @process, @material, @part_weight, @tolerance, @gross_weight, @sap_ct)";

                foreach (var sapItem in sapList)
                {
                    await using var cmd = new SqlCommand(insertSql, conn, transaction);
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@id_type", sapItem["id_type"].GetInt32());
                    cmd.Parameters.AddWithValue("@mould", sapItem["mould"].GetInt32());
                    cmd.Parameters.AddWithValue("@type", sapItem["type"].GetString());
                    cmd.Parameters.AddWithValue("@qty_perct", sapItem["qty_perct"].GetInt32());
                    cmd.Parameters.AddWithValue("@process", sapItem["process"].GetString());
                    cmd.Parameters.AddWithValue("@material", sapItem["material"].GetString());
                    cmd.Parameters.AddWithValue("@part_weight", sapItem["part_weight"].GetDouble());
                    cmd.Parameters.AddWithValue("@tolerance", sapItem["tolerance"].GetDouble());
                    cmd.Parameters.AddWithValue("@gross_weight", sapItem["gross_weight"].GetDouble());
                    cmd.Parameters.AddWithValue("@sap_ct", sapItem["sap_ct"].GetDouble());

                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }
        public async Task<object> LoadTimeline()
        {
            var time = DateTime.Now;
            var (productionDate, shift) = GetProductionDate(time);

            var sql = @"
                    WITH timeline AS(
                        SELECT 1 AS id_machine, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()) AS start, COALESCE(ml.finish, GETDATE()) AS finish, COALESCE(UPPER(ml.category), 'N/A') AS category, COALESCE(ml.mould_category, 0) AS mould_category, ml.shift, ml.production_date
                        FROM machine_log_1 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 2, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_2 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 3, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_3 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 4, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_4 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 5, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_5 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 6, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_6 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 7, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_7 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 8, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_8 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 9, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_9 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 10, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_10 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 11, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_11 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 12, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_12 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 13, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_13 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 14, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_14 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 15, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_15 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 16, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_16 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 17, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_17 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 18, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_18 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 19, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_19 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 20, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_20 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 21, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_21 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 22, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_22 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 23, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_23 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 24, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_24 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 25, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_25 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 26, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_26 ml WHERE production_date = @production_date AND shift = @shift
                    )
                    SELECT
                        mm.machine_name,
                        tl.id_machine,
                        mm.type,
                        tl.id_type,
                        tl.mould,
                        tl.start,
                        tl.finish,
                        CAST(DATEDIFF(MINUTE, tl.start, tl.finish) / 60.0 AS FLOAT) AS duration,
                        tl.category,
                        tl.mould_category,
                        (mm.shot * mm.qty_perct) AS output,
                        CAST(COALESCE(
                            CASE
                            WHEN DATEPART(HOUR, GETDATE()) BETWEEN 6 AND 17 THEN
                                (((DATEPART(HOUR, GETDATE()) - 6) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                            WHEN DATEPART(HOUR, GETDATE()) < 6 THEN
                                (((DATEPART(HOUR, GETDATE()) + 24 - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                            ELSE
                                (((DATEPART(HOUR, GETDATE()) - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                        END, 0) AS INT) AS plan_output,
                        CAST(
                            COALESCE(
                                ((mm.shot * mm.qty_perct) /
                                NULLIF(
                                    CASE
                                        WHEN DATEPART(HOUR, GETDATE()) BETWEEN 6 AND 17 THEN
                                            (((DATEPART(HOUR, GETDATE()) - 6) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                                        WHEN DATEPART(HOUR, GETDATE()) < 6 THEN
                                            (((DATEPART(HOUR, GETDATE()) + 24 - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                                        ELSE
                                            (((DATEPART(HOUR, GETDATE()) - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                                    END, 0)
                                ) * 100, 0
                            ) AS FLOAT
                        ) AS efficiency,
                        tl.shift,
                        tl.production_date
                    FROM timeline tl
                    LEFT JOIN machine_master mm
                        ON tl.id_machine = mm.id_machine
                    ORDER BY tl.start DESC;";

            var result = new List<object>();

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    machine_name = Convert.ToString(reader["machine_name"]),
                    id_machine = Convert.ToInt32(reader["id_machine"]),
                    type = Convert.ToString(reader["type"]),
                    id_type = Convert.ToInt32(reader["id_type"]),
                    mould = Convert.ToInt32(reader["mould"]),
                    start = Convert.ToDateTime(reader["start"]),
                    finish = Convert.ToDateTime(reader["finish"]),
                    duration = Convert.ToSingle(reader["duration"]),
                    category = Convert.ToString(reader["category"]),
                    mould_category = Convert.ToInt32(reader["mould_category"]),
                    output = Convert.ToInt32(reader["output"]),
                    plan_output = Convert.ToInt32(reader["plan_output"]),
                    efficiency = Convert.ToSingle(reader["efficiency"]),
                    shift = Convert.ToInt32(reader["shift"]),
                    production_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["production_date"])),
                    color = CategoryColorHelper.GetColorByCategory(Convert.ToString(reader["category"])),
                });
            }

            return result;
        }
        public async Task<object> LoadMachineData()
        {
            var time = DateTime.Now;
            var (productionDate, shift) = GetProductionDate(time);

            var sql = @"
                    WITH timeline AS(
                        SELECT 1 AS id_machine, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()) AS start, COALESCE(ml.finish, GETDATE()) AS finish, COALESCE(UPPER(ml.category), 'N/A') AS category, COALESCE(ml.mould_category, 0) AS mould_category, ml.shift, ml.production_date
                        FROM machine_log_1 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 2, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_2 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 3, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_3 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 4, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_4 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 5, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_5 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 6, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_6 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 7, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_7 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 8, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_8 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 9, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_9 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 10, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_10 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 11, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_11 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 12, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_12 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 13, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_13 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 14, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_14 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 15, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_15 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 16, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_16 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 17, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_17 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 18, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_18 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 19, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_19 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 20, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_20 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 21, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_21 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 22, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_22 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 23, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_23 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 24, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_24 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 25, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_25 ml WHERE production_date = @production_date AND shift = @shift
                        UNION ALL
                        SELECT 26, ml.machine_name, ml.id_type, ml.mould, COALESCE(ml.start, GETDATE()), COALESCE(ml.finish, GETDATE()), COALESCE(UPPER(ml.category), 'N/A'), COALESCE(ml.mould_category, 0), ml.shift, ml.production_date
                        FROM machine_log_26 ml WHERE production_date = @production_date AND shift = @shift
                    )
                    SELECT
                        mm.machine_name,
                        tl.id_machine,
                        mm.type,
                        tl.id_type,
                        tl.mould,
                        tl.start,
                        tl.finish,
                        CAST(DATEDIFF(MINUTE, tl.start, tl.finish) / 60.0 AS FLOAT) AS duration,
                        tl.category,
                        tl.mould_category,
                        (mm.shot * mm.qty_perct) AS output,
                        CAST(COALESCE(
                            CASE
                            WHEN DATEPART(HOUR, GETDATE()) BETWEEN 6 AND 17 THEN
                                (((DATEPART(HOUR, GETDATE()) - 6) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                            WHEN DATEPART(HOUR, GETDATE()) < 6 THEN
                                (((DATEPART(HOUR, GETDATE()) + 24 - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                            ELSE
                                (((DATEPART(HOUR, GETDATE()) - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                        END, 0) AS INT) AS plan_output,
                        CAST(
                            COALESCE(
                                ((mm.shot * mm.qty_perct) /
                                NULLIF(
                                    CASE
                                        WHEN DATEPART(HOUR, GETDATE()) BETWEEN 6 AND 17 THEN
                                            (((DATEPART(HOUR, GETDATE()) - 6) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                                        WHEN DATEPART(HOUR, GETDATE()) < 6 THEN
                                            (((DATEPART(HOUR, GETDATE()) + 24 - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                                        ELSE
                                            (((DATEPART(HOUR, GETDATE()) - 18) * 3600 + DATEPART(MINUTE, GETDATE()) * 60 + DATEPART(SECOND, GETDATE())) / (mm.sap_ct / NULLIF(mm.qty_perct, 0)))
                                    END, 0)
                                ) * 100, 0
                            ) AS FLOAT
                        ) AS efficiency,
                        tl.shift,
                        tl.production_date
                    FROM timeline tl
                    LEFT JOIN machine_master mm
                        ON tl.id_machine = mm.id_machine
                    ORDER BY tl.start DESC;";

            var result = new List<object>();

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    machine_name = Convert.ToString(reader["machine_name"]),
                    id_machine = Convert.ToInt32(reader["id_machine"]),
                    type = Convert.ToString(reader["type"]),
                    id_type = Convert.ToInt32(reader["id_type"]),
                    mould = Convert.ToInt32(reader["mould"]),
                    start = Convert.ToDateTime(reader["start"]),
                    finish = Convert.ToDateTime(reader["finish"]),
                    duration = Convert.ToSingle(reader["duration"]),
                    category = Convert.ToString(reader["category"]),
                    mould_category = Convert.ToInt32(reader["mould_category"]),
                    output = Convert.ToInt32(reader["output"]),
                    plan_output = Convert.ToInt32(reader["plan_output"]),
                    efficiency = Convert.ToSingle(reader["efficiency"]),
                    shift = Convert.ToInt32(reader["shift"]),
                    production_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["production_date"])),
                    color = CategoryColorHelper.GetColorByCategory(Convert.ToString(reader["category"])),
                });
            }

            return result;
        }
        public async Task<object> LoadUtilities(int id_machine)
        {
            var sql = @"
                    DECLARE @production_date DATE = CASE
                        WHEN CAST(GETDATE() AS TIME) BETWEEN '00:00:00' AND '07:59:59'
                            THEN DATEADD(DAY, -1, CAST(GETDATE() AS DATE))
                        ELSE CAST(GETDATE() AS DATE)
                    END;

                    DECLARE @shift INT = CASE
                        WHEN CAST(GETDATE() AS TIME) BETWEEN '08:00:00' AND '19:59:59'
                            THEN 1
                        ELSE 2
                    END;

                    SELECT
                        u.utility_name,
                        COALESCE(u.start, GETDATE())  AS start,
                        COALESCE(u.finish, GETDATE()) AS finish,
                        DATEDIFF(MINUTE, COALESCE(u.start, GETDATE()), COALESCE(u.finish, GETDATE())) AS duration,
                        COALESCE(u.category, 0) AS category
                    FROM utilities u
                    WHERE u.id_machine = @id_machine 
                      AND u.production_date = @production_date
                      AND u.shift = @shift
                    ORDER BY u.start;";

            var result = new List<object>();

            using var conn = await CreateConnection();
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@id_machine", id_machine);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    utility_name = Convert.ToString(reader["utility_name"]),
                    start = Convert.ToDateTime(reader["start"]),
                    finish = Convert.ToDateTime(reader["finish"]),
                    duration = Convert.ToSingle(reader["duration"]),
                    category = Convert.ToInt32(reader["category"]),
                });
            }

            return result;
        }
        public async Task<object> LoadStaffSchedule()
        {
            var sql = @"
                SELECT * FROM staff_list";

            var result = new List<object>();

            using var conn = await CreateConnection();
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
        public async Task UpdateStaffSchedule(List<Dictionary<string, JsonElement>> staffList)
        {
            var time = DateTime.Now;
            var (productionDate, shift) = GetProductionDate(time);

            var sql1 = @"
                SET NOCOUNT ON;

                UPDATE staff_list 
                SET staff_name   = @staff_name,
                    staff_role   = @staff_role,
                    status       = @status,
                    machine_name = @machine_name,
                    start_date   = @start_date,
                    end_date     = @end_date,
                    work_shift   = @work_shift
                WHERE staff_id = @staff_id;

                UPDATE attendance
                SET staff_role   = @staff_role,
                    status       = @status,
                    machine_name = @machine_name
                WHERE staff_id = @staff_id
                    AND production_date BETWEEN @start_date AND @end_date
                    AND shift = @work_shift;";

            var sql2 = @"
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
                    GROUP BY 
                        MSplit.machine_name, 
                        sl.work_shift;

                UPDATE mm
                    SET mm.packer = ISNULL(pt.packer, '')
                    FROM machine_master mm
                    LEFT JOIN @PackerTable pt 
                        ON mm.machine_name = pt.machine_name
                        AND mm.shift = pt.work_shift;

                SELECT * FROM machine_master order by id_machine";

            using var conn = await CreateConnection();

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
                    string staffName = staff["staff_name"].GetString();
                    string staffRole = staff["staff_role"].GetString();
                    string? status = staff.TryGetValue("status", out var statusEl) && statusEl.ValueKind != JsonValueKind.Null ? statusEl.GetString() : null;
                    string? machineName = staff.TryGetValue("machine_name", out var machineEl) && machineEl.ValueKind != JsonValueKind.Null ? machineEl.GetString() : null;
                    DateTime? startDate = staff.TryGetValue("start_date", out var startEl) && startEl.ValueKind == JsonValueKind.String ? startEl.GetDateTime() : (DateTime?)null;
                    DateTime? endDate = staff.TryGetValue("end_date", out var endEl) && endEl.ValueKind == JsonValueKind.String ? endEl.GetDateTime() : (DateTime?)null;
                    int? workShift = staff.TryGetValue("work_shift", out var shiftEl) && shiftEl.ValueKind != JsonValueKind.Null ? shiftEl.GetInt32() : (int?)null;

                    using (var cmd = new SqlCommand(sql1, conn, (SqlTransaction)tx))
                    {
                        cmd.Parameters.AddWithValue("@staff_id", staffId);
                        cmd.Parameters.AddWithValue("@staff_name", staffName);
                        cmd.Parameters.AddWithValue("@staff_role", staffRole);
                        cmd.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@machine_name", (object?)machineName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@start_date", (object?)startDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@end_date", (object?)endDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@work_shift", (object?)workShift ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@production_date", productionDate);
                        cmd.Parameters.AddWithValue("@shift", shift);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                List<dynamic> master = new();
                await using (var cmd2 = new SqlCommand(sql2, conn, (SqlTransaction)tx))
                await using (var reader = await cmd2.ExecuteReaderAsync())
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
        public async Task InsertStaff(List<Dictionary<string, JsonElement>> staffList)
        {
            var sql = @"
                INSERT INTO staff_list (staff_id, staff_name, staff_role, status)
                VALUES ( @staff_id, @staff_name, @staff_role, 'INACTIVE')";

            using var conn = await CreateConnection();

            foreach (var staff in staffList)
            {
                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@staff_id", staff["staff_id"].GetInt32());
                cmd.Parameters.AddWithValue("@staff_name", staff["staff_name"].GetString());
                cmd.Parameters.AddWithValue("@staff_role", staff["staff_role"].GetString());

                await cmd.ExecuteNonQueryAsync();
            }
        }
        public async Task UpdateStaff(List<Dictionary<string, JsonElement>> staffList)
        {
            var sql = @"
                UPDATE staff_list
                SET staff_name = @staff_name,
                    staff_role = @staff_role,
                    status     = @status
                WHERE staff_id = @staff_id";

            using var conn = await CreateConnection();

            foreach (var staff in staffList)
            {
                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@staff_id", staff["staff_id"].GetInt32());
                cmd.Parameters.AddWithValue("@staff_name", staff["staff_name"].GetString());
                cmd.Parameters.AddWithValue("@staff_role", staff["staff_role"].GetString());
                cmd.Parameters.AddWithValue("@status", staff.ContainsKey("status") && staff["status"].ValueKind != JsonValueKind.Null ? staff["status"].GetString() : (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }
        public async Task DeleteStaff(List<Dictionary<string, JsonElement>> staffList)
        {
            var sql = @"DELETE FROM staff_list WHERE staff_id = @staff_id";

            using var conn = await CreateConnection();

            foreach (var staff in staffList)
            {
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@staff_id", staff["staff_id"].GetInt32());

                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion

        #region Machine Master Auto
        //Obtain machine_master data var
        public async Task<machine_master> GetMachineMaster(int id_machine, DateTime time, dynamic plcData)
        {
            var sql = $@"
                SELECT 
                    COALESCE(m.machine_name, '') AS machine_name,
                    COALESCE(m.packer, '') AS packer,
                    COALESCE(m.material, '') AS material,
                    COALESCE(m.id_type, 0) AS id_type,
                    COALESCE(m.mould, 0) AS mould,
                    COALESCE(m.type, '') AS type,
                    COALESCE(m.jo_no, '') AS jo_no,
                    COALESCE(m.qty_order, 0) AS qty_order,
                    COALESCE(m.wip_opening, 0) AS wip_opening,
                    COALESCE(m.wip_closing, 0) AS wip_closing,
                    COALESCE(m.finish_good, 0) AS finish_good,
                    COALESCE(m.qty_accum, 0) AS qty_accum,
                    COALESCE(m.qty_perct, 0) AS qty_perct,
                    COALESCE(m.sap_ct, 0) AS sap_ct,
                    COALESCE(m.part_weight, 0) AS part_weight,
                    COALESCE(m.gross_weight, 0) AS gross_weight,
                    COALESCE(m.measure_qc, 0) AS measure_qc
                FROM machine_master m
                WHERE m.id_machine = @id_machine";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_machine", id_machine);

            using var reader = await cmd.ExecuteReaderAsync();

            var master = new machine_master();

            if (await reader.ReadAsync())
            {
                master.id_machine = id_machine;
                master.machine_name = Convert.ToString(reader["machine_name"]);
                master.packer = Convert.ToString(reader["packer"]);
                master.id_type = Convert.ToInt32(reader["id_type"]);
                master.mould = Convert.ToInt32(reader["mould"]);
                master.type = Convert.ToString(reader["type"]);
                master.jo_no = Convert.ToString(reader["jo_no"]);
                master.qty_order = Convert.ToInt32(reader["qty_order"]);
                master.wip_opening = Convert.ToInt32(reader["wip_opening"]);
                master.wip_closing = Convert.ToInt32(reader["wip_closing"]);
                master.finish_good = Convert.ToInt32(reader["finish_good"]);
                master.qty_accum = Convert.ToInt32(reader["qty_accum"]);
                master.qty_perct = Convert.ToInt32(reader["qty_perct"]);
                master.sap_ct = Convert.ToSingle(reader["sap_ct"]);
                master.part_weight = Convert.ToSingle(reader["part_weight"]);
                master.gross_weight = Convert.ToSingle(reader["gross_weight"]);
                master.measure_qc = Convert.ToBoolean(reader["measure_qc"]);
                master.time = time;
                master.shot = plcData.shot ?? 0;
                master.shot_accum = plcData.shot_accum ?? 0;
                master.act_ct = plcData.act_ct ?? 0f;
                master.mould_category_no = plcData.mould_category_no ?? "";
                master.stop_category = plcData.stop_category ?? "";
                master.remark = plcData.remark ?? "";
                master.reject_panelling = plcData.reject_panelling ?? 0;
                master.reject_lumpy = plcData.reject_lumpy ?? 0;
                master.reject_black_dot = plcData.reject_black_dot ?? 0;
                master.reject_burst = plcData.reject_burst ?? 0;
                master.reject_startup = plcData.reject_startup ?? 0;
                master.reject_preform = plcData.reject_preform ?? 0;
                master.reject_purging = plcData.reject_purging ?? 0;
                master.reject_others = plcData.reject_others ?? 0;
                master.status_start = plcData.status_start ?? false;
                master.status_off = plcData.status_off ?? false;
                master.production_running = plcData.production_running ?? false;
                master.visual_qc = plcData.visual_qc ?? false;
                master.remark_signal = plcData.remark_signal ?? false;
                master.reject_signal = plcData.reject_signal ?? false;
                master.util_barrel = plcData.util_barrel ?? false;
                master.util_hyd_motor = plcData.util_hyd_motor ?? false;
                master.util_dehumidifier = plcData.util_dehumidifier ?? false;
                master.util_chiller = plcData.util_chiller ?? false;
                master.util_material = plcData.util_material ?? false;
                master.util_dry_cycle = plcData.util_dry_cycle ?? false;
            }

            return master;
        }

        // Trigger when machine stop, start, change category, change mould category, problem note changed
        public async Task insertMachineMaster(dynamic plcData)
        {
            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var prev = _lastMachineMaster.GetValueOrDefault(id_machine);
            var (productionDate, shift) = GetProductionDate(time);

            if (prev.plcData == null)
            {
                await shiftChange(plcData);

                _lastMachineMaster[id_machine] = (plcData, productionDate, shift, false);
                return;
            }

            bool shift_change = prev.productionDate != productionDate || prev.shift != shift;
            bool prod_run = prev.plcData.production_running != plcData.production_running;
            bool status_start = prev.plcData.status_start != plcData.status_start;
            bool status_off = prev.plcData.status_off != plcData.status_off;
            bool category = prev.plcData.stop_category != plcData.stop_category;
            bool mould_category_no = prev.plcData.mould_category_no != plcData.mould_category_no;
            bool no_category = !string.IsNullOrEmpty(prev.plcData.stop_category) && string.IsNullOrEmpty(plcData.stop_category) && !plcData.status_start;
            bool remark = prev.plcData.remark_signal != plcData.remark_signal;
            bool reject_signal = prev.plcData.reject_signal != plcData.reject_signal;

            var util_changed = new List<(string utility_name, bool status)>();
            if (prev.plcData.util_barrel != plcData.util_barrel)
                util_changed.Add(("BARREL", plcData.util_barrel));
            if (prev.plcData.util_hyd_motor != plcData.util_hyd_motor)
                util_changed.Add(("HYDRAULIC MOTOR", plcData.util_hyd_motor));
            if (prev.plcData.util_dehumidifier != plcData.util_dehumidifier)
                util_changed.Add(("DEHUMIDIFIER", plcData.util_dehumidifier));
            if (prev.plcData.util_chiller != plcData.util_chiller)
                util_changed.Add(("CHILLER", plcData.util_chiller));
            if (prev.plcData.util_material != plcData.util_material)
                util_changed.Add(("MATERIAL", plcData.util_material));
            if (prev.plcData.util_dry_cycle != plcData.util_dry_cycle)
                util_changed.Add(("DRY CYCLE", plcData.util_dry_cycle));

            var tableName = $"machine_log_{id_machine}";

            var sql = $@"
                SET NOCOUNT ON;

                UPDATE machine_master SET
                    act_ct = @act_ct,
                    status_start = @status_start,
                    status_off = @status_off,
                    shot = @shot_accum,
	                visual_qc = @visual_qc
                WHERE id_machine = @id_machine;
                
                UPDATE [{tableName}] SET shot = @shot, act_ct = @act_ct WHERE finish IS NULL;

                SELECT measure_qc FROM machine_master WHERE id_machine = @id_machine";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_machine", id_machine);
            cmd.Parameters.AddWithValue("@act_ct", plcData.act_ct);
            cmd.Parameters.AddWithValue("@status_start", plcData.status_start);
            cmd.Parameters.AddWithValue("@status_off", plcData.status_off);
            cmd.Parameters.AddWithValue("@shot", plcData.shot);
            cmd.Parameters.AddWithValue("@shot_accum", plcData.shot_accum);
            cmd.Parameters.AddWithValue("@visual_qc", plcData.visual_qc);
            var result = await cmd.ExecuteScalarAsync();

            bool measure_qc = Convert.ToBoolean(result);
            bool measure_qc_changed = prev.plcData == null || prev.measure_qc != measure_qc;

            try
            {
                // Machine running/stopped
                if (status_start || no_category)
                    await insertMachineStop(plcData);

                // Shift changed
                if (shift_change)
                    await shiftChange(plcData);

                // Stop category changed
                if (category && !string.IsNullOrEmpty(plcData.stop_category))
                    await updateCategory(plcData);

                // Mould number changed
                if (mould_category_no && plcData.mould_category_no != 0)
                    await updateMouldCategory(plcData);

                // Remark signal changed
                if (remark && plcData.remark_signal && !string.IsNullOrEmpty(plcData.remark))
                    await updateProblem(plcData);

                // Reject signal triggered
                if (reject_signal && plcData.reject_signal)
                    await insertUpdateReject(plcData);

                // Measure QC changed
                if (measure_qc_changed && measure_qc)
                    await updateMeasureQC(plcData, measure_qc);

                // Utilities changed
                foreach (var util in util_changed)
                    await updateUtilities(plcData, util.utility_name, util.status);

                }
            catch (Exception ex)
            {
                Console.WriteLine($"[Machine {plcData.id_machine}] ERROR: {ex.Message}");
            }

            _lastMachineMaster[id_machine] = (plcData, productionDate, shift, measure_qc);
        }
        #endregion

        #region Machine Log Auto
        // On Shift Change
        public async Task shiftChange(dynamic plcData)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Shift Change");
            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            var (productionDate, shift) = GetProductionDate(time);

            var total_weight = master.reject_panelling + master.reject_lumpy + master.reject_black_dot + master.reject_burst + master.reject_startup + master.reject_preform + master.reject_purging + master.reject_others;
            var tableName = $"machine_log_{master.id_machine}";

            var sql = $@"
                -- Update Reject Table
                IF NOT EXISTS (SELECT 1 FROM reject WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
                BEGIN
                    INSERT INTO reject (id_machine, machine_name, id_type, mould, shift, production_date) 
                    VALUES (@id_machine, @machine_name, @id_type, @mould, @shift, @production_date);
                END
                ELSE
                BEGIN
                    UPDATE reject SET total_weight = @total_weight, reject_panelling = @reject_panelling, reject_lumpy = @reject_lumpy, reject_black_dot = @reject_black_dot, reject_burst = @reject_burst, reject_startup = @reject_startup, reject_preform = @reject_preform, reject_purging = @reject_purging, reject_others = @reject_others
                    WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND shift = @shift and production_date = @production_date
                END

                -- Update Machine Log Table
                IF EXISTS (SELECT 1 FROM [{tableName}] WHERE finish IS NULL)
                BEGIN
                    UPDATE [{tableName}] SET finish = @time WHERE finish IS NULL;
                END

                INSERT INTO [{tableName}] (machine_name, id_type, mould, start, category, problem, mould_category, shift, production_date) 
                VALUES (@machine_name, @id_type, @mould, @time, NULLIF(@stop_category, ''), NULLIF(@problem, ''), NULLIF(@mould_category, 0), @shift, @production_date);

                -- Update Utilities Table
                DECLARE @UpdatedRows TABLE (id_machine INT, machine_name NVARCHAR(255), utility_name NVARCHAR(255), category NVARCHAR(255));

                -- Process BARREL
                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'BARREL' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    SELECT id_machine, machine_name, utility_name, category
                    FROM utilities
                    WHERE id_machine = @id_machine AND utility_name = 'BARREL' AND finish IS NULL;
    
                    UPDATE utilities 
                    SET finish = @time 
                    WHERE id_machine = @id_machine AND utility_name = 'BARREL' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'BARREL')
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    VALUES (@id_machine, @machine_name, 'BARREL', @util_barrel);
                END

                -- Process HYDRAULIC MOTOR
                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    SELECT id_machine, machine_name, utility_name, category
                    FROM utilities
                    WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR' AND finish IS NULL;
    
                    UPDATE utilities 
                    SET finish = @time 
                    WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR')
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    VALUES (@id_machine, @machine_name, 'HYDRAULIC MOTOR', @util_hyd_motor);
                END

                -- Process DEHUMIDIFIER
                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    SELECT id_machine, machine_name, utility_name, category
                    FROM utilities
                    WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER' AND finish IS NULL;
    
                    UPDATE utilities 
                    SET finish = @time 
                    WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER')
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    VALUES (@id_machine, @machine_name, 'DEHUMIDIFIER', @util_dehumidifier);
                END

                -- Process CHILLER
                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'CHILLER' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    SELECT id_machine, machine_name, utility_name, category
                    FROM utilities
                    WHERE id_machine = @id_machine AND utility_name = 'CHILLER' AND finish IS NULL;
    
                    UPDATE utilities 
                    SET finish = @time 
                    WHERE id_machine = @id_machine AND utility_name = 'CHILLER' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'CHILLER')
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    VALUES (@id_machine, @machine_name, 'CHILLER', @util_chiller);
                END

                -- Process MATERIAL
                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'MATERIAL' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    SELECT id_machine, machine_name, utility_name, category
                    FROM utilities
                    WHERE id_machine = @id_machine AND utility_name = 'MATERIAL' AND finish IS NULL;
    
                    UPDATE utilities 
                    SET finish = @time 
                    WHERE id_machine = @id_machine AND utility_name = 'MATERIAL' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'MATERIAL')
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    VALUES (@id_machine, @machine_name, 'MATERIAL', @util_material);
                END

                -- Process DRY CYCLE
                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    SELECT id_machine, machine_name, utility_name, category
                    FROM utilities
                    WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE' AND finish IS NULL;
    
                    UPDATE utilities 
                    SET finish = @time 
                    WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE')
                BEGIN
                    INSERT INTO @UpdatedRows (id_machine, machine_name, utility_name, category)
                    VALUES (@id_machine, @machine_name, 'DRY CYCLE', @util_dry_cycle);
                END

                -- Insert utilities
                INSERT INTO utilities (id_machine, machine_name, utility_name, start, category, shift, production_date)
                SELECT @id_machine, @machine_name, utility_name, @time, category, @shift, @production_date
                FROM @UpdatedRows;

                -- Update attendance and staff_list Table
                IF NOT EXISTS (SELECT 1 FROM attendance WHERE production_date = @production_date AND shift = @shift)
                BEGIN
                    INSERT INTO attendance (staff_id, staff_name, staff_role, production_date, shift, status, machine_name)
                    SELECT staff_id, staff_name, staff_role, @production_date, @shift,
                        CASE 
                            WHEN @production_date BETWEEN start_date AND end_date AND work_shift = @shift 
                            THEN status 
                            ELSE 'INACTIVE' 
                        END AS status,
                        CASE 
                            WHEN @production_date BETWEEN start_date AND end_date AND work_shift = @shift
                            THEN machine_name 
                            ELSE NULL 
                        END AS machine_name
                    FROM staff_list;

                    UPDATE sl
                    SET 
                        sl.staff_role = a.staff_role,
                        sl.status = a.status,
                        sl.machine_name = a.machine_name
                    FROM staff_list sl
                    INNER JOIN attendance a ON sl.staff_id = a.staff_id
                    WHERE a.production_date = @production_date AND a.shift = @shift;
                END

                -- Update Report Table
                IF NOT EXISTS (SELECT 1 FROM report WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
                BEGIN
                    INSERT INTO report (id_machine, machine_name, time, shift, production_date, id_type, mould) 
                    VALUES (@id_machine, @machine_name, @time, @shift, @production_date, @id_type, @mould);
                END

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
                    GROUP BY 
                        MSplit.machine_name, 
                        sl.work_shift;

                UPDATE mm
                    SET mm.packer = ISNULL(pt.packer, '')
                    FROM machine_master mm
                    LEFT JOIN @PackerTable pt 
                        ON mm.machine_name = pt.machine_name
                       AND mm.shift = pt.work_shift;

                -- Update Machine Master Table
                UPDATE machine_master 
                    SET material = s.material, id_type = @id_type, mould = @mould, type = s.type, jo_no = 0, qty_order = 0, wip_opening = 0, wip_closing = 0, finish_good = 0, qty_accum = 0, qty_perct = s.qty_perct, sap_ct = s.sap_ct, part_weight = s.part_weight, gross_weight = s.gross_weight, shift = @shift
                    FROM machine_master m
                    JOIN sap s ON s.id_type = @id_type AND s.mould = @mould
                    WHERE m.id_machine = @id_machine;";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id_machine", master.id_machine);
            cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
            cmd.Parameters.AddWithValue("@id_type", master.id_type);
            cmd.Parameters.AddWithValue("@mould", master.mould);
            cmd.Parameters.AddWithValue("@shot", master.shot);
            cmd.Parameters.AddWithValue("@act_ct", master.act_ct);
            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);
            cmd.Parameters.AddWithValue("@time", time);
            cmd.Parameters.AddWithValue("@total_weight", total_weight);
            cmd.Parameters.AddWithValue("@util_barrel", plcData.util_barrel);
            cmd.Parameters.AddWithValue("@util_hyd_motor", plcData.util_hyd_motor);
            cmd.Parameters.AddWithValue("@util_dehumidifier", plcData.util_dehumidifier);
            cmd.Parameters.AddWithValue("@util_chiller", plcData.util_chiller);
            cmd.Parameters.AddWithValue("@util_material", plcData.util_material);
            cmd.Parameters.AddWithValue("@util_dry_cycle", plcData.util_dry_cycle);
            cmd.Parameters.AddWithValue("@reject_panelling", master.reject_panelling);
            cmd.Parameters.AddWithValue("@reject_lumpy", master.reject_lumpy);
            cmd.Parameters.AddWithValue("@reject_black_dot", master.reject_black_dot);
            cmd.Parameters.AddWithValue("@reject_burst", master.reject_burst);
            cmd.Parameters.AddWithValue("@reject_startup", master.reject_startup);
            cmd.Parameters.AddWithValue("@reject_preform", master.reject_preform);
            cmd.Parameters.AddWithValue("@reject_purging", master.reject_purging);
            cmd.Parameters.AddWithValue("@reject_others", master.reject_others);
            cmd.Parameters.AddWithValue("@stop_category", master.stop_category);
            cmd.Parameters.AddWithValue("@problem", master.remark);
            cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
            cmd.Parameters.AddWithValue("@packer", master.packer);
            await cmd.ExecuteNonQueryAsync();

            _plcService.UpdatePLCS(master);
        }

        // Insert new and Update finish Machine Log Stop
        public async Task insertMachineStop(dynamic plcData)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Insert Machine Stop");
            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            var (productionDate, shift) = GetProductionDate(time);
            var tableName = $"machine_log_{id_machine}";

            var sql = $@"
                UPDATE [{tableName}] SET finish = @time WHERE finish IS NULL

                INSERT INTO [{tableName}]
                (machine_name, id_type, mould, start, shift, production_date, status_start)
                VALUES (@machine_name, @id_type, @mould, @time, @shift, @production_date, @status_start)";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
            cmd.Parameters.AddWithValue("@id_type", master.id_type);
            cmd.Parameters.AddWithValue("@mould", master.mould);
            cmd.Parameters.AddWithValue("@time", master.time);
            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);
            cmd.Parameters.AddWithValue("@status_start", master.status_start);

            await cmd.ExecuteNonQueryAsync();
        }

        // Update Category
        public async Task updateCategory(dynamic plcData)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Update Category");
            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            var tableName = $"machine_log_{master.id_machine}";

            var sql = $@"
                UPDATE [{tableName}] SET category = @category WHERE category IS NULL OR finish IS NULL";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@category", master.stop_category);
            await cmd.ExecuteNonQueryAsync();
        }

        // Update Problem
        public async Task updateProblem(dynamic plcData)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Update Problem");

            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            if (string.IsNullOrWhiteSpace(master.remark))
                return;

            var tableName = $"machine_log_{master.id_machine}";

            var sql = $@"
                UPDATE [{tableName}] 
                SET 
                    problem = CASE 
                        WHEN @remark = '' THEN NULL 
                        ELSE @remark 
                    END,
                    mould_category = CASE 
                        WHEN category = 'MOULD CHANGE' AND @mould_category != '' THEN @mould_category 
                        ELSE NULL
                    END
                WHERE finish IS NULL";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@remark", master.remark);
            cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
            await cmd.ExecuteNonQueryAsync();
        }

        // Update Mould Category
        public async Task updateMouldCategory(dynamic plcData)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Update Mould Category");

            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            var tableName = $"machine_log_{master.id_machine}";
            var sql = $@"
                UPDATE [{tableName}] 
                SET 
                    problem = CASE 
                        WHEN @remark = '' THEN NULL 
                        ELSE @remark 
                    END,
                    mould_category = CASE 
                        WHEN category = 'MOULD CHANGE' AND @mould_category != '' THEN @mould_category 
                        ELSE NULL
                    END
                WHERE category = 'MOULD CHANGE' AND mould_category IS NULL";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@remark", master.remark);
            cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
            await cmd.ExecuteNonQueryAsync();
        }

        // Update Measure QC
        public async Task updateMeasureQC(dynamic plcData, bool measure_qc)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Update Measure QC");

            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            var sql = $@"
                UPDATE machine_master SET measure_qc = @measure_qc WHERE id_machine = @id_machine";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@measure_qc", measure_qc);
            cmd.Parameters.AddWithValue("@id_machine", id_machine);
            await cmd.ExecuteNonQueryAsync();

            _plcService.UpdateMeasureQC(master);
        }

        // Update Utilities
        public async Task updateUtilities(dynamic plcData, string utility_name, bool status)
        {
            Console.WriteLine($"[Machine {plcData.id_machine}] Update Utilities");

            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;
            var (productionDate, shift) = GetProductionDate(time);
            var master = await GetMachineMaster(id_machine, time, plcData);

            var sql = $@"
                UPDATE utilities 
                SET finish = @time 
                WHERE id_machine = @id_machine 
                    AND utility_name = @utility_name 
                    AND finish IS NULL;

                INSERT INTO utilities (id_machine, machine_name, utility_name, start, category, shift, production_date) 
                VALUES (@id_machine, @machine_name, @utility_name, @time, @category, @shift, @production_date);";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_machine", id_machine);
            cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
            cmd.Parameters.AddWithValue("@time", time);
            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);
            cmd.Parameters.AddWithValue("@utility_name", utility_name);
            cmd.Parameters.AddWithValue("@category", status);
            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Reject
        public async Task insertUpdateReject(dynamic plcData)
        {
            int id_machine = plcData.id_machine;
            DateTime time = plcData.time;

            var master = await GetMachineMaster(id_machine, time, plcData);

            var (productionDate, shift) = GetProductionDate(time);
            var total_weight = master.reject_panelling + master.reject_lumpy + master.reject_black_dot + master.reject_burst + master.reject_startup + master.reject_preform + master.reject_purging + master.reject_others;

            var sql = $@"
                IF NOT EXISTS ( SELECT 1 FROM reject WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND shift = @shift AND production_date = @production_date )
                BEGIN
                    INSERT INTO reject ( id_machine, machine_name, id_type, mould, shift, production_date, total_weight, reject_panelling, reject_lumpy, reject_black_dot, reject_burst, reject_startup, reject_preform, reject_purging, reject_others)
                    VALUES ( @id_machine, @machine_name, @id_type, @mould, @shift, @production_date, @total_weight, @reject_panelling, @reject_lumpy, @reject_black_dot, @reject_burst, @reject_startup, @reject_preform, @reject_purging, @reject_others)
                END
                ELSE
                BEGIN
                    UPDATE reject SET
                        total_weight = ROUND(@total_weight, 2),
                        reject_panelling = ROUND(@reject_panelling, 2),
                        reject_lumpy = ROUND(@reject_lumpy, 2),
                        reject_black_dot = ROUND(@reject_black_dot, 2),
                        reject_burst = ROUND(@reject_burst, 2),
                        reject_startup = ROUND(@reject_startup, 2),
                        reject_preform = ROUND(@reject_preform, 2),
                        reject_purging = ROUND(@reject_purging, 2),
                        reject_others = ROUND(@reject_others, 2)
                    WHERE id_machine = @id_machine
                        AND id_type = @id_type AND mould = @mould
                        AND shift = @shift AND production_date = @production_date
                END";

            using var conn = await CreateConnection();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_machine", master.id_machine);
            cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
            cmd.Parameters.AddWithValue("@id_type", master.id_type);
            cmd.Parameters.AddWithValue("@mould", master.mould);
            cmd.Parameters.AddWithValue("@total_weight", total_weight);
            cmd.Parameters.AddWithValue("@reject_panelling", master.reject_panelling);
            cmd.Parameters.AddWithValue("@reject_lumpy", master.reject_lumpy);
            cmd.Parameters.AddWithValue("@reject_black_dot", master.reject_black_dot);
            cmd.Parameters.AddWithValue("@reject_burst", master.reject_burst);
            cmd.Parameters.AddWithValue("@reject_startup", master.reject_startup);
            cmd.Parameters.AddWithValue("@reject_preform", master.reject_preform);
            cmd.Parameters.AddWithValue("@reject_purging", master.reject_purging);
            cmd.Parameters.AddWithValue("@reject_others", master.reject_others);
            cmd.Parameters.AddWithValue("@shift", shift);
            cmd.Parameters.AddWithValue("@production_date", productionDate);

            await cmd.ExecuteNonQueryAsync();
        }
        #endregion

        #region Helper
        private static (DateOnly productionDate, int shift) GetProductionDate(DateTime time)
        {
            var hour = time.Hour;

            DateTime productionDate = (hour >= 0 && hour < 6) ? time.AddDays(-1).Date : time.Date;

            int shift = (hour >= 6 && hour < 18) ? 1 : 2;

            return (DateOnly.FromDateTime(productionDate), shift);
        }

        public static class CategoryColorHelper
        {
            public static string GetColorByCategory(string? category)
            {
                return category switch
                {
                    "PRODUCTION RUNNING" => "#00ff00",
                    "PRODUCT BUYOFF" => "#808080",
                    "NO OPERATOR" or "NO SCHEDULE" or "MATERIAL DRYING" or "OTHERS PROD" => "#ffff00",
                    "QUALITY ISSUE" or "SAMPLE RUNNING" or "MOULD CHANGE" or "OTHERS TECH" => "#ff0000",
                    "SCHEDULED MAINTENANCE" or "MACHINE BREAKDOWN" or "OTHERS MAIN" => "#ffa500",
                    _ => "#808080"
                };
            }
        }
        #endregion
    }
}
