using CMS.Server.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;

namespace CMS.Server.Services;

public class BaseService
{
    protected readonly string _connectionString;
    protected readonly MainPlcService _plcService;

    private readonly ConcurrentDictionary<int, (dynamic plcData, DateOnly productionDate, int shift, int lastWrittenMeasureQc)> _lastMachineMaster = new();

    public BaseService(string connectionString, MainPlcService plcService)
    {
        _connectionString = connectionString;
        _plcService = plcService;
    }

    // ── Connection factory ────────────────────────────────────────────────────

    protected async ValueTask<SqlConnection> CreateConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    #region DynamicMachineLogHelpers

    public async Task<IReadOnlyList<(int id, string name, string ip)>> GetMachineIdsAsync()
    {
        const string sql = "SELECT id_machine, machine_name FROM machine_master ORDER BY id_machine";
        var machines = new List<(int, string, string)>();

        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            string name = reader.GetString(1);
            string ip = $"172.17.86.{220 + id}";
            machines.Add((id, name, ip));
        }

        return machines;
    }

    protected async Task<string> BuildMachineLogUnionAsync(string whereClause = "")
    {
        var ids = await GetMachineIdsAsync();
        if (ids.Count == 0)
            return "SELECT NULL AS id_machine WHERE 1=0";

        var where = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

        var parts = ids.Select(m =>
            $"SELECT {m.id} AS id_machine, machine_name, id_type, mould, COALESCE(start, GETDATE()) AS start, COALESCE(finish, GETDATE()) as finish, COALESCE(UPPER(category), 'N/A') as category, problem,  COALESCE(mould_category, 0) AS mould_category, shot, act_ct, shift, production_date, status_start FROM machine_log_{m.id}{where}");

        return string.Join("\n UNION ALL\n ", parts);
    }

    #endregion

    // ── Shift / date helpers ──────────────────────────────────────────────────

    protected static (DateOnly productionDate, int shift) GetProductionDate(DateTime time)
    {
        var hour = time.Hour;
        DateTime date = (hour >= 0 && hour < 6) ? time.AddDays(-1).Date : time.Date;
        int shift = (hour >= 6 && hour < 18) ? 1 : 2;
        return (DateOnly.FromDateTime(date), shift);
    }

    // ── Category colour mapping ───────────────────────────────────────────────

    public static string GetColor(string? category) => category switch
    {
        "PRODUCTION RUNNING"                                                    => "#00ff00",
        "PRODUCT BUYOFF"                                                        => "#808080",
        "NO OPERATOR" or "NO SCHEDULE" or "MATERIAL DRYING" or "OTHERS PROD"    => "#ffff00",
        "QUALITY ISSUE" or "SAMPLE RUNNING" or "MOULD CHANGE" or "OTHERS TECH"  => "#ff0000",
        "SCHEDULED MAINTENANCE" or "MACHINE BREAKDOWN" or "OTHERS MAIN"         => "#ffa500",
        _                                                                       => "#808080"
    };

    public async Task<object> LoadTimeline()
    {
        var time = DateTime.Now;
        var (productionDate, shift) = GetProductionDate(time);

        var logUnion = await BuildMachineLogUnionAsync("production_date = @production_date AND shift = @shift");

        var sql = $@"
                    WITH timeline AS(
                        {logUnion}
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
                        COALESCE((mm.shot * mm.qty_perct), 0) AS output,
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

        using var conn = await CreateConnectionAsync();
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
                color = GetColor(Convert.ToString(reader["category"])),
            });
        }

        return result;
    }

    #region Machine Master Auto
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

        using var conn = await CreateConnectionAsync();
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
            master.measure_qc = Convert.ToInt32(reader["measure_qc"]);
            master.visual_qc = plcData.visual_qc ?? 0;
            master.time = time;
            master.shot = plcData.shot ?? 0;
            master.shot_accum = plcData.shot_accum ?? 0;
            master.act_ct = plcData.act_ct ?? 0f;
            master.mould_category_no = plcData.mould_category_no ?? 0;
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

        var master = await GetMachineMaster(id_machine, time, plcData);

        var prev = _lastMachineMaster.GetValueOrDefault(id_machine);
        var (productionDate, shift) = GetProductionDate(time);

        await using var conn = await CreateConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        if (prev.plcData == null)
        {
            try
            {
                await shiftChange(master, conn, (SqlTransaction)tx);
                await tx.CommitAsync();
                _lastMachineMaster[id_machine] = (plcData, productionDate, shift, master.measure_qc);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            return;
        }

        bool shift_change           = prev.productionDate != productionDate || prev.shift != shift;
        bool category               = prev.plcData.stop_category != plcData.stop_category;
        bool mould_category_no      = prev.plcData.mould_category_no != plcData.mould_category_no;
        bool mould_change_started   = prev.plcData.stop_category != plcData.stop_category && plcData.stop_category == "MOULD CHANGE";
        bool remark_signal          = !prev.plcData.remark_signal && plcData.remark_signal;
        bool reject_signal          = !prev.plcData.reject_signal && plcData.reject_signal;
        bool qc_signal              = !prev.plcData.qc_signal && plcData.qc_signal;
        bool done                   = !plcData.done && plcData.done;
        bool machine_started        = !prev.plcData.status_start && plcData.status_start;
        bool machine_stopped        = prev.plcData.status_start && !plcData.status_start;
        bool production_running     = !prev.plcData.production_running && plcData.production_running;
        bool qc_reset               = !prev.plcData.qc_reset_signal && plcData.qc_reset_signal;

        var util_changed = new List<(string utility_name, bool status)>();
        if (prev.plcData.util_barrel != plcData.util_barrel) util_changed.Add(("BARREL", plcData.util_barrel));
        if (prev.plcData.util_hyd_motor != plcData.util_hyd_motor) util_changed.Add(("HYDRAULIC MOTOR", plcData.util_hyd_motor));
        if (prev.plcData.util_dehumidifier != plcData.util_dehumidifier) util_changed.Add(("DEHUMIDIFIER", plcData.util_dehumidifier));
        if (prev.plcData.util_chiller != plcData.util_chiller) util_changed.Add(("CHILLER", plcData.util_chiller));
        if (prev.plcData.util_material != plcData.util_material) util_changed.Add(("MATERIAL", plcData.util_material));
        if (prev.plcData.util_dry_cycle != plcData.util_dry_cycle) util_changed.Add(("DRY CYCLE", plcData.util_dry_cycle));

        try
        {
            const string updateSql = @"
                UPDATE machine_master
                SET
                    act_ct       = @act_ct,
                    status_start = @status_start,
                    status_off   = @status_off,
                    shot         = @shot_accum,
                    visual_qc    = @visual_qc
                WHERE id_machine = @id_machine;

                SELECT measure_qc FROM machine_master WHERE id_machine = @id_machine";

            await using var cmd = new SqlCommand(updateSql, conn);
            cmd.Transaction = (SqlTransaction)tx;
            cmd.Parameters.AddWithValue("@id_machine", id_machine);
            cmd.Parameters.AddWithValue("@act_ct", master.act_ct);
            cmd.Parameters.AddWithValue("@status_start", master.status_start);
            cmd.Parameters.AddWithValue("@status_off", master.status_off);
            cmd.Parameters.AddWithValue("@shot_accum", master.shot_accum);
            cmd.Parameters.AddWithValue("@visual_qc", master.visual_qc);

            var result = await cmd.ExecuteScalarAsync();
            int measure_qc = result != DBNull.Value && result != null ? Convert.ToInt32(result) : 0;

            // ── Event-driven actions ───────────────────────────────────────────

            if (measure_qc != prev.lastWrittenMeasureQc)
                _plcService.WriteQcMeasure(id_machine, measure_qc);

            if (production_running)
                await insertMachineRun(master, conn, (SqlTransaction)tx);

            if (done || machine_started || machine_stopped)
                await insertMachineStop(master, conn, (SqlTransaction)tx);

            if (shift_change)
                await shiftChange(master, conn, (SqlTransaction)tx);

            if (category && !string.IsNullOrEmpty(master.stop_category))
                await updateCategory(master, conn, (SqlTransaction)tx);

            if (master.mould_category_no != 0 && (mould_category_no || mould_change_started))
                await updateMouldCategory(master, conn, (SqlTransaction)tx);

            if (remark_signal)
                await updateProblem(master, conn, (SqlTransaction)tx);

            if (reject_signal)
                await insertUpdateReject(master, conn, (SqlTransaction)tx);

            if (qc_reset)
                await resetQC(master, conn, (SqlTransaction)tx);

            foreach (var util in util_changed)
                await updateUtilities(master, util.utility_name, util.status, conn, (SqlTransaction)tx);

            await tx.CommitAsync();

            _lastMachineMaster[id_machine] = (plcData, productionDate, shift, measure_qc);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
    #endregion

    #region Machine Log Auto
    // On Shift Change
    public async Task shiftChange(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Shift Change");

        var (productionDate, shift) = GetProductionDate(master.time);

        var total_weight = master.reject_panelling + master.reject_lumpy + master.reject_black_dot + master.reject_burst + master.reject_startup + master.reject_preform + master.reject_purging + master.reject_others;
        var tableName = $"machine_log_{master.id_machine}";

        var sql = $@"
                IF NOT EXISTS (
                    SELECT 1 FROM calendar
                    WHERE production_date = @production_date AND shift = @shift
                )
                BEGIN
                    INSERT INTO calendar (production_date, shift, day_type, planned_hours, start, finish)
                    VALUES (
                        @production_date,
                        @shift,
                        'NORMAL',
                        12,
                        CASE @shift
                            WHEN 1 THEN CAST(CAST(@production_date AS DATETIME) + CAST('06:00:00' AS DATETIME) AS DATETIME)
                            ELSE        CAST(CAST(@production_date AS DATETIME) + CAST('18:00:00' AS DATETIME) AS DATETIME)
                        END,
                        CASE @shift
                            WHEN 1 THEN CAST(CAST(@production_date AS DATETIME) + CAST('18:00:00' AS DATETIME) AS DATETIME)
                            ELSE        CAST(CAST(DATEADD(DAY, 1, @production_date) AS DATETIME) + CAST('06:00:00' AS DATETIME) AS DATETIME)
                        END
                    );
                END

                -- Update Reject Table
                IF NOT EXISTS (SELECT 1 FROM reject WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift)
                BEGIN
                    INSERT INTO reject (id_machine, machine_name, id_type, mould, shift, production_date) 
                    VALUES (@id_machine, @machine_name, @id_type, @mould, @shift, @production_date);
                END
                ELSE
                BEGIN
                    UPDATE reject SET
                        total_weight      = @total_weight,
                        reject_panelling  = @reject_panelling,
                        reject_lumpy      = @reject_lumpy,
                        reject_black_dot  = @reject_black_dot,
                        reject_burst      = @reject_burst,
                        reject_startup    = @reject_startup,
                        reject_preform    = @reject_preform,
                        reject_purging    = @reject_purging,
                        reject_others     = @reject_others
                    WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND shift = @shift AND production_date = @production_date
                END

                -- Update Machine Log Table
                IF EXISTS (SELECT 1 FROM [{tableName}] WHERE finish IS NULL)
                BEGIN
                    DECLARE @prev_shot_sum INT;
                    SELECT @prev_shot_sum = ISNULL(SUM(shot), 0)
                    FROM [{tableName}]
                    WHERE production_date = @production_date
                      AND shift           = @shift
                      AND id_type         = @id_type
                      AND mould           = @mould
                      AND finish          IS NOT NULL;

                    DECLARE @final_shot INT = @shot_accum - @prev_shot_sum;
                    IF @final_shot < 0 SET @final_shot = 0;

                    UPDATE [{tableName}]
                    SET
                        finish  = @time,
                        shot    = @final_shot,
                        act_ct  = CASE
                                      WHEN @final_shot = 0 THEN 0
                                      ELSE DATEDIFF(SECOND, start, @time) / CAST(@final_shot AS FLOAT)
                                  END
                    WHERE finish IS NULL;
                END

                INSERT INTO [{tableName}] (machine_name, id_type, mould, start, shot, category, problem, mould_category, shift, production_date, status_start) 
                VALUES (
                    @machine_name, @id_type, @mould, @time, 0, NULLIF(@category, ''), NULLIF(@problem, ''), 
                    CASE
                        WHEN NULLIF(@category, '') = 'MOULD CHANGE'
                        THEN @mould_category
                        ELSE 0
                    END,
                    @shift, @production_date, @status_start
                );

                -- Update Utilities Table
                DECLARE @UpdatedRows TABLE (id_machine INT, machine_name NVARCHAR(255), utility_name NVARCHAR(255), category NVARCHAR(255));

                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'BARREL' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows SELECT id_machine, machine_name, utility_name, category FROM utilities WHERE id_machine = @id_machine AND utility_name = 'BARREL' AND finish IS NULL;
                    UPDATE utilities SET finish = @time WHERE id_machine = @id_machine AND utility_name = 'BARREL' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'BARREL')
                BEGIN
                    INSERT INTO @UpdatedRows VALUES (@id_machine, @machine_name, 'BARREL', @util_barrel);
                END

                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows SELECT id_machine, machine_name, utility_name, category FROM utilities WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR' AND finish IS NULL;
                    UPDATE utilities SET finish = @time WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'HYDRAULIC MOTOR')
                BEGIN
                    INSERT INTO @UpdatedRows VALUES (@id_machine, @machine_name, 'HYDRAULIC MOTOR', @util_hyd_motor);
                END

                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows SELECT id_machine, machine_name, utility_name, category FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER' AND finish IS NULL;
                    UPDATE utilities SET finish = @time WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DEHUMIDIFIER')
                BEGIN
                    INSERT INTO @UpdatedRows VALUES (@id_machine, @machine_name, 'DEHUMIDIFIER', @util_dehumidifier);
                END

                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'CHILLER' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows SELECT id_machine, machine_name, utility_name, category FROM utilities WHERE id_machine = @id_machine AND utility_name = 'CHILLER' AND finish IS NULL;
                    UPDATE utilities SET finish = @time WHERE id_machine = @id_machine AND utility_name = 'CHILLER' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'CHILLER')
                BEGIN
                    INSERT INTO @UpdatedRows VALUES (@id_machine, @machine_name, 'CHILLER', @util_chiller);
                END

                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'MATERIAL' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows SELECT id_machine, machine_name, utility_name, category FROM utilities WHERE id_machine = @id_machine AND utility_name = 'MATERIAL' AND finish IS NULL;
                    UPDATE utilities SET finish = @time WHERE id_machine = @id_machine AND utility_name = 'MATERIAL' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'MATERIAL')
                BEGIN
                    INSERT INTO @UpdatedRows VALUES (@id_machine, @machine_name, 'MATERIAL', @util_material);
                END

                IF EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE' AND finish IS NULL)
                BEGIN
                    INSERT INTO @UpdatedRows SELECT id_machine, machine_name, utility_name, category FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE' AND finish IS NULL;
                    UPDATE utilities SET finish = @time WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE' AND finish IS NULL;
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM utilities WHERE id_machine = @id_machine AND utility_name = 'DRY CYCLE')
                BEGIN
                    INSERT INTO @UpdatedRows VALUES (@id_machine, @machine_name, 'DRY CYCLE', @util_dry_cycle);
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
                    INSERT INTO report (id_machine, machine_name, time, shift, production_date, id_type, mould, type, material, qty_perct, gross_weight, part_weight, sap_ct) 
                    SELECT 
                        @id_machine, @machine_name, @time, @shift, @production_date, @id_type, @mould, s.type, s.material, s.qty_perct, s.gross_weight, s.part_weight, s.sap_ct
                    FROM sap s
                    WHERE s.id_type = @id_type 
                      AND s.mould = @mould;
                END
                ELSE
                BEGIN
                    UPDATE report SET time = @time
                    WHERE id_machine = @id_machine AND id_type = @id_type AND mould = @mould AND production_date = @production_date AND shift = @shift
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
                    SET mm.packer = COALESCE(pt.packer, '')
                    FROM machine_master mm
                    LEFT JOIN @PackerTable pt 
                        ON mm.machine_name = pt.machine_name
                       AND mm.shift = pt.work_shift;

                -- Update Machine Master for new shift
                UPDATE machine_master
                    SET material    = s.material,
                        id_type     = @id_type,
                        mould       = @mould,
                        type        = s.type,
                        jo_no       = 0,
                        qty_order   = 0,
                        wip_opening = 0,
                        wip_closing = 0,
                        finish_good = 0,
                        qty_accum   = 0,
                        qty_perct   = s.qty_perct,
                        sap_ct      = s.sap_ct,
                        part_weight = s.part_weight,
                        gross_weight= s.gross_weight,
                        shift       = @shift
                    FROM machine_master m
                    JOIN sap s ON s.id_type = @id_type AND s.mould = @mould
                    WHERE m.id_machine = @id_machine;";

        await using var cmd = new SqlCommand(sql, conn, tx);

        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@id_machine", master.id_machine);
        cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
        cmd.Parameters.AddWithValue("@id_type", master.id_type);
        cmd.Parameters.AddWithValue("@mould", master.mould);
        cmd.Parameters.AddWithValue("@shot_accum", master.shot_accum);
        cmd.Parameters.AddWithValue("@status_start", master.status_start);
        cmd.Parameters.AddWithValue("@shift", shift);
        cmd.Parameters.AddWithValue("@production_date", productionDate);
        cmd.Parameters.AddWithValue("@time", master.time);
        cmd.Parameters.AddWithValue("@total_weight", total_weight);
        cmd.Parameters.AddWithValue("@util_barrel", master.util_barrel);
        cmd.Parameters.AddWithValue("@util_hyd_motor", master.util_hyd_motor);
        cmd.Parameters.AddWithValue("@util_dehumidifier", master.util_dehumidifier);
        cmd.Parameters.AddWithValue("@util_chiller", master.util_chiller);
        cmd.Parameters.AddWithValue("@util_material", master.util_material);
        cmd.Parameters.AddWithValue("@util_dry_cycle", master.util_dry_cycle);
        cmd.Parameters.AddWithValue("@reject_panelling", master.reject_panelling);
        cmd.Parameters.AddWithValue("@reject_lumpy", master.reject_lumpy);
        cmd.Parameters.AddWithValue("@reject_black_dot", master.reject_black_dot);
        cmd.Parameters.AddWithValue("@reject_burst", master.reject_burst);
        cmd.Parameters.AddWithValue("@reject_startup", master.reject_startup);
        cmd.Parameters.AddWithValue("@reject_preform", master.reject_preform);
        cmd.Parameters.AddWithValue("@reject_purging", master.reject_purging);
        cmd.Parameters.AddWithValue("@reject_others", master.reject_others);
        cmd.Parameters.AddWithValue("@category", master.stop_category);
        cmd.Parameters.AddWithValue("@problem", master.remark);
        cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
        cmd.Parameters.AddWithValue("@packer", master.packer);
        await cmd.ExecuteNonQueryAsync();

        _plcService.UpdatePLCS(master);
    }

    // Insert new and Update finish Machine Log Stop
    public async Task insertMachineRun(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Insert Machine Run");

        var (productionDate, shift) = GetProductionDate(master.time);
        var tableName = $"machine_log_{master.id_machine}";

        var sql = $@"
                UPDATE [{tableName}]
                SET category = 'PRODUCTION RUNNING'
                WHERE category IS NULL AND status_start = 1

                UPDATE [{tableName}]
                SET finish = @time
                WHERE finish IS NULL

                INSERT INTO [{tableName}] (machine_name, id_type, mould, start, shot, category, mould_category, shift, production_date, status_start)
                VALUES (@machine_name, @id_type, @mould, @time, 0, 'PRODUCTION RUNNING', 0, @shift, @production_date, @status_start);";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
        cmd.Parameters.AddWithValue("@id_type", master.id_type);
        cmd.Parameters.AddWithValue("@mould", master.mould);
        cmd.Parameters.AddWithValue("@time", master.time);
        cmd.Parameters.AddWithValue("@shift", shift);
        cmd.Parameters.AddWithValue("@production_date", productionDate);
        cmd.Parameters.AddWithValue("@status_start", master.status_start);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task insertMachineStop(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Insert Machine Stop");

        var (productionDate, shift) = GetProductionDate(master.time);
        var tableName = $"machine_log_{master.id_machine}";

        var sql = $@"
                IF EXISTS (SELECT 1 FROM [{tableName}] WHERE finish IS NULL)
                BEGIN
                    DECLARE @prev_shot_sum INT;
                    SELECT @prev_shot_sum = ISNULL(SUM(shot), 0)
                    FROM [{tableName}]
                    WHERE production_date = @production_date
                      AND shift           = @shift
                      AND id_type         = @id_type
                      AND mould           = @mould
                      AND finish          IS NOT NULL;

                    DECLARE @final_shot INT = @shot_accum - @prev_shot_sum;
                    IF @final_shot < 0 SET @final_shot = 0;

                    UPDATE [{tableName}]
                    SET
                        finish  = @time,
                        shot    = @final_shot,
                        act_ct  = CASE
                                      WHEN @final_shot = 0 THEN 0
                                      ELSE DATEDIFF(SECOND, start, @time) / CAST(@final_shot AS FLOAT)
                                  END
                    WHERE finish IS NULL;
                END

                INSERT INTO [{tableName}] (machine_name, id_type, mould, start, shot, mould_category, shift, production_date, status_start)
                VALUES (@machine_name, @id_type, @mould, @time, 0, 0, @shift, @production_date, @status_start);";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
        cmd.Parameters.AddWithValue("@id_type", master.id_type);
        cmd.Parameters.AddWithValue("@mould", master.mould);
        cmd.Parameters.AddWithValue("@shot_accum", master.shot_accum);
        cmd.Parameters.AddWithValue("@time", master.time);
        cmd.Parameters.AddWithValue("@shift", shift);
        cmd.Parameters.AddWithValue("@production_date", productionDate);
        cmd.Parameters.AddWithValue("@status_start", master.status_start);

        await cmd.ExecuteNonQueryAsync();
    }

    // Update Category
    public async Task updateCategory(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Update Category = {master.stop_category}");

        var tableName = $"machine_log_{master.id_machine}";

        var sql = $@"
                UPDATE [{tableName}]
                SET category = @category
                WHERE finish IS NULL OR category IS NULL;";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@category", master.stop_category);
        await cmd.ExecuteNonQueryAsync();
    }

    // Update Problem
    public async Task updateProblem(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Update Problem = {master.remark}");

        if (string.IsNullOrWhiteSpace(master.remark))
            return;

        var tableName = $"machine_log_{master.id_machine}";

        var sql = $@"
                UPDATE [{tableName}] 
                SET 
                    problem = NULLIF(@problem, ''),
                    mould_category = 
                        CASE
                            WHEN category = 'MOULD CHANGE' AND @mould_category <> 0
                            THEN @mould_category
                            ELSE 0
                        END
                WHERE finish IS NULL";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@problem", master.remark);
        cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
        await cmd.ExecuteNonQueryAsync();
    }

    // Update Mould Category
    public async Task updateMouldCategory(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Update Mould Category = {master.mould_category_no}");

        var tableName = $"machine_log_{master.id_machine}";
        var sql = $@"
                UPDATE [{tableName}] 
                SET 
                    problem = NULLIF(@problem, ''),
                    mould_category = 
                        CASE
                            WHEN category = 'MOULD CHANGE' AND @mould_category <> 0
                            THEN @mould_category
                            ELSE 0
                    END
                WHERE (category = 'MOULD CHANGE' AND mould_category = 0) OR (category = 'MOULD CHANGE' AND finish IS NULL)";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@problem", master.remark);
        cmd.Parameters.AddWithValue("@mould_category", master.mould_category_no);
        await cmd.ExecuteNonQueryAsync();
    }

    // Update Utilities
    public async Task updateUtilities(machine_master master, string utility_name, bool status, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Update Utilities");

        var (productionDate, shift) = GetProductionDate(master.time);

        var sql = $@"
                UPDATE utilities 
                SET finish = @time 
                WHERE id_machine = @id_machine 
                    AND utility_name = @utility_name 
                    AND finish IS NULL;

                INSERT INTO utilities (id_machine, machine_name, utility_name, start, category, shift, production_date) 
                VALUES (@id_machine, @machine_name, @utility_name, @time, @category, @shift, @production_date);";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@id_machine", master.id_machine);
        cmd.Parameters.AddWithValue("@machine_name", master.machine_name);
        cmd.Parameters.AddWithValue("@time", master.time);
        cmd.Parameters.AddWithValue("@shift", shift);
        cmd.Parameters.AddWithValue("@production_date", productionDate);
        cmd.Parameters.AddWithValue("@utility_name", utility_name);
        cmd.Parameters.AddWithValue("@category", status);
        await cmd.ExecuteNonQueryAsync();
    }

    // Reset Measure QC and Visual QC
    public async Task resetQC(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] QC Reset");

        const string sql = @"
            UPDATE machine_master
            SET visual_qc = 0, measure_qc = 0
            WHERE id_machine = @id_machine";

        await using var cmd = new SqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@id_machine", master.id_machine);
        await cmd.ExecuteNonQueryAsync();

        _plcService.ResetQcCounters(master.id_machine);
    }
    #endregion

    #region Reject
    public async Task insertUpdateReject(machine_master master, SqlConnection conn, SqlTransaction tx)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Machine {master.id_machine}] Update Reject");

        var (productionDate, shift) = GetProductionDate(master.time);
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

        await using var cmd = new SqlCommand(sql, conn, tx);
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
}