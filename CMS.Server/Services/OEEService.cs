using Microsoft.Data.SqlClient;
namespace CMS.Server.Services;

public class OEEService(PlcService plcService, string connectionString) : BaseService(connectionString, plcService)
{
    public async Task<IReadOnlyList<object>> CalculateOeeAsync(DateOnly startDate, DateOnly endDate, int shift)
    {
        var (today, currentShift) = GetProductionDate(DateTime.Now);
        bool isToday = startDate == endDate && startDate == today;

        string sql = isToday
            ? await BuildTodayOeeSqlAsync(today)
            : await BuildRangeOeeSqlAsync();

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@today", today);
        cmd.Parameters.AddWithValue("@start_date", startDate);
        cmd.Parameters.AddWithValue("@end_date", endDate);
        cmd.Parameters.AddWithValue("@shift", isToday ? currentShift : shift);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                machine_name = reader["machine_name"].ToString(),
                run_time = Convert.ToSingle(reader["run_time"]),
                down_time = Convert.ToSingle(reader["down_time"]),
                unallocated = Convert.ToSingle(reader["unallocated"]),
                material_used = Convert.ToSingle(reader["material_used"]),
                reject_weight = Convert.ToSingle(reader["reject_weight"]),
                availability = Convert.ToSingle(reader["availability"]),
                performance = Convert.ToSingle(reader["performance"]),
                quality = Convert.ToSingle(reader["quality"]),
                oee = Convert.ToSingle(reader["oee"]),
            });
        }
        return result;
    }

    private async Task<string> BuildTodayOeeSqlAsync(DateOnly today)
    {
        var logCte = await BuildMachineLogUnionAsync(
            "machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start");

        return $@"
            WITH CombinedLogs AS (
                {logCte}
            ),
            MachineAgg AS (
                SELECT id_machine, machine_name, id_type, mould,
                    SUM(shot) AS shot,
                    SUM(CASE WHEN category='PRODUCTION RUNNING' AND status_start=1
                             THEN DATEDIFF(SECOND,start,COALESCE(finish,GETDATE()))/3600.0 ELSE 0 END) AS run_time,
                    SUM(CASE WHEN category<>'PRODUCTION RUNNING'
                             THEN DATEDIFF(SECOND,start,COALESCE(finish,GETDATE()))/3600.0 ELSE 0 END) AS down_time,
                    AVG(NULLIF(act_ct,0)) AS act_ct
                FROM CombinedLogs
                WHERE production_date=@today AND shift=@shift
                GROUP BY id_machine, machine_name, id_type, mould
            ),
            WithSAP AS (
                SELECT m.*,
                    COALESCE(s.sap_ct,0) AS sap_ct,
                    COALESCE((m.shot * s.qty_perct * s.part_weight)/1000.0,0) AS material_used,
                    COALESCE(m.shot * s.sap_ct,0) AS total_sap_time,
                    COALESCE(m.shot * m.act_ct,0)  AS total_actual_time
                FROM MachineAgg m
                LEFT JOIN sap s ON s.id_type=m.id_type AND s.mould=m.mould
            ),
            WithReject AS (
                SELECT w.*,
                    COALESCE(r.reject_black_dot+r.reject_burst+r.reject_lumpy+r.reject_others+r.reject_panelling,0) AS reject_weight
                FROM WithSAP w
                LEFT JOIN reject r
                    ON r.id_machine=w.id_machine AND r.id_type=w.id_type AND r.mould=w.mould
                    AND r.production_date=@today AND r.shift=@shift
            ),
            MachineSummary AS (
                SELECT id_machine, machine_name,
                    SUM(run_time) AS run_time, SUM(down_time) AS down_time,
                    0 AS unallocated,
                    SUM(material_used) AS material_used, SUM(reject_weight) AS reject_weight,
                    SUM(total_sap_time) AS total_sap_time, SUM(total_actual_time) AS total_actual_time
                FROM WithReject GROUP BY id_machine, machine_name
            )
            SELECT id_machine, machine_name, run_time, down_time, unallocated, material_used, reject_weight,
                CASE WHEN (run_time+down_time)=0 THEN 0 ELSE (run_time*1.0/(run_time+down_time))*100 END AS availability,
                CASE WHEN total_actual_time=0    THEN 0 ELSE (total_sap_time*1.0/total_actual_time)*100 END AS performance,
                CASE WHEN material_used=0         THEN 0 ELSE ((material_used-reject_weight)*1.0/material_used)*100 END AS quality,
                CASE WHEN (run_time+down_time)=0 OR total_actual_time=0 OR material_used=0 THEN 0
                     ELSE (run_time*1.0/(run_time+down_time))*(total_sap_time*1.0/total_actual_time)*((material_used-reject_weight)*1.0/material_used)*100
                END AS oee
            FROM MachineSummary
            ORDER BY id_machine;";
    }

    private static Task<string> BuildRangeOeeSqlAsync() => Task.FromResult(@"
        WITH AllDates AS (
            SELECT DATEADD(DAY,n.n,@start_date) AS d
            FROM (SELECT TOP (DATEDIFF(DAY,@start_date,@end_date)+1)
                         ROW_NUMBER() OVER (ORDER BY (SELECT NULL))-1 AS n
                  FROM sys.all_objects) n
        ),
        ShiftDates AS (
            SELECT d.d AS production_date, s.shift
            FROM AllDates d CROSS JOIN (SELECT 1 AS shift UNION ALL SELECT 2) s
        ),
        EffectiveCalendar AS (
            SELECT sd.production_date, sd.shift,
                CASE WHEN EXISTS(SELECT 1 FROM calendar cx WHERE cx.production_date=sd.production_date AND cx.shift=sd.shift)
                     THEN COALESCE((SELECT SUM(c.planned_hours) FROM calendar c WHERE c.production_date=sd.production_date AND c.shift=sd.shift),0)
                     ELSE 12
                END AS planned_hours
            FROM ShiftDates sd
        ),
        ReportAgg AS (
            SELECT r.id_machine, r.machine_name, r.production_date, r.shift,
                COALESCE(SUM(r.production_running),0) AS run_time,
                COALESCE(SUM(COALESCE(r.change_full_set,0)+COALESCE(r.change_half_set,0)+COALESCE(r.change_parts,0)+COALESCE(r.maintenance_dt,0)+COALESCE(r.technician_dt,0)+COALESCE(r.production_dt,0)),0) AS down_time,
                COALESCE(SUM(r.unallocated),0) AS unallocated,
                SUM(COALESCE(r.material_used,0)*COALESCE(r.sap_ct,0)) AS total_sap_time,
                SUM(COALESCE(r.material_used,0)*COALESCE(r.act_ct,0)) AS total_actual_time,
                COALESCE(SUM(r.material_used),0) AS material_used,
                COALESCE(SUM(r.reject_prod+r.reject_startup),0) AS reject_weight
            FROM report r
            INNER JOIN EffectiveCalendar ec ON ec.production_date=r.production_date AND ec.shift=r.shift AND ec.planned_hours>0
            WHERE r.production_date BETWEEN @start_date AND @end_date
            GROUP BY r.id_machine, r.machine_name, r.production_date, r.shift
        ),
        MachineSummary AS (
            SELECT ra.id_machine, ra.machine_name,
                SUM(ra.run_time) AS run_time, SUM(ra.down_time) AS down_time,
                SUM(ra.unallocated) AS unallocated, SUM(ra.material_used) AS material_used,
                SUM(ra.reject_weight) AS reject_weight, SUM(ra.total_sap_time) AS total_sap_time,
                SUM(ra.total_actual_time) AS total_actual_time, SUM(ec2.planned_hours) AS available_hours
            FROM ReportAgg ra
            INNER JOIN EffectiveCalendar ec2 ON ec2.production_date=ra.production_date AND ec2.shift=ra.shift
            GROUP BY ra.id_machine, ra.machine_name
        )
        SELECT id_machine, machine_name, run_time, down_time, unallocated, material_used, reject_weight, available_hours,
            CASE WHEN NULLIF(available_hours,0) IS NULL THEN 0 ELSE (run_time*1.0/available_hours)*100 END AS availability,
            CASE WHEN total_actual_time=0 THEN 0 ELSE (total_sap_time*1.0/total_actual_time)*100 END AS performance,
            CASE WHEN material_used=0 THEN 0 WHEN ((material_used-reject_weight)*1.0/material_used)<0 THEN 0
                 ELSE ((material_used-reject_weight)*1.0/material_used)*100 END AS quality,
            CASE WHEN NULLIF(available_hours,0) IS NULL OR total_actual_time=0 OR material_used=0 THEN 0
                 ELSE (run_time*1.0/available_hours)*(total_sap_time*1.0/total_actual_time)*((material_used-reject_weight)*1.0/material_used)*100
            END AS oee
        FROM MachineSummary ORDER BY id_machine;");

    // ── Pareto helpers (Reject / Output / Downtime) ────────────────────────────

    public async Task<IReadOnlyList<object>> LoadRejectAsync(DateOnly start, DateOnly end)
    {
        const string sql = @"
            SELECT TOP 10 reject.id_type, reject.mould, sap.type,
                COALESCE(SUM(total_weight),0) AS total_reject
            FROM reject
            LEFT JOIN sap ON sap.id_type=reject.id_type AND sap.mould=reject.mould
            WHERE production_date BETWEEN @start_date AND @end_date AND reject.id_type <> 123456
            GROUP BY reject.id_type, reject.mould, sap.type
            HAVING COALESCE(SUM(total_weight),0) > 0
            ORDER BY COALESCE(SUM(total_weight),0) DESC";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@start_date", start);
        cmd.Parameters.AddWithValue("@end_date", end);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new { id_type = Convert.ToInt32(reader["id_type"]), type = reader["type"].ToString(), total_reject = Convert.ToSingle(reader["total_reject"]) });
        return result;
    }

    public async Task<IReadOnlyList<object>> LoadOutputAsync(DateOnly start, DateOnly end)
    {
        var (today, _) = GetProductionDate(DateTime.Now);
        bool isToday = start == today && end == today;

        string sql = isToday
            ? @"SELECT TOP 10 id_type, type,
                    COALESCE(CASE WHEN part_weight>0 THEN (shift_output/part_weight) ELSE 0 END,0) AS total_output
                FROM machine_master WHERE id_type<>123456
                ORDER BY CASE WHEN part_weight>0 THEN (shift_output/part_weight) ELSE 0 END DESC"
            : @"SELECT TOP 10 id_type, type,
                    COALESCE(SUM(CASE WHEN part_weight>0 THEN (shift_output/part_weight) ELSE 0 END),0) AS total_output
                FROM report
                WHERE production_date BETWEEN @start_date AND @end_date AND id_type<>123456
                GROUP BY id_type, type
                ORDER BY COALESCE(SUM(CASE WHEN part_weight>0 THEN (shift_output/part_weight) ELSE 0 END),0) DESC";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        if (!isToday) { cmd.Parameters.AddWithValue("@start_date", start); cmd.Parameters.AddWithValue("@end_date", end); }
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new { id_type = Convert.ToInt32(reader["id_type"]), type = reader["type"].ToString(), total_output = Convert.ToSingle(reader["total_output"]) });
        return result;
    }

    public async Task<object> LoadDowntimeAsync(DateOnly start_date, DateOnly end_date)
    {
        var logUnion = await BuildMachineLogUnionAsync(
            columns: "id_type, mould, category, start, finish, production_date",
            whereClause: "production_date BETWEEN @start_date AND @end_date");

        var sql = $@"
            WITH CombinedLogs AS (
                {logUnion}
            )
            SELECT TOP 10
                sap.id_type,
                sap.type,
                SUM(DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE()))) / 3600.0 AS hours
            FROM CombinedLogs ml
            LEFT JOIN sap
                ON sap.id_type = ml.id_type
               AND sap.mould = ml.mould
            WHERE ml.id_type <> 123456
              AND ml.category NOT IN ('PRODUCTION RUNNING', 'NO SCHEDULE', 'SCHEDULED MAINTENANCE')
            GROUP BY sap.id_type, sap.type
            ORDER BY hours DESC;";

        var result = new List<object>();

        await using var conn = await CreateConnectionAsync();
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

    // ── Machine Detail ─────────────────────────────────────────────────────────

    private static void ValidateMachineId(int id_machine)
    {
        if (id_machine < 1 || id_machine > 40)
            throw new ArgumentOutOfRangeException(nameof(id_machine), "id_machine must be 1–40.");
    }

    public async Task<IReadOnlyList<object>> LoadMachineProductOutputAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        const string sql = @"
            SELECT sap.type, report.id_type, report.mould,
                SUM(COALESCE(report.shot,0)) AS shot,
                MAX(COALESCE(report.qty_perct,0)) AS qty_perct,
                SUM(COALESCE(report.shift_output,0)) AS shift_output,
                MAX(COALESCE(report.part_weight,0)) AS part_weight,
                ROUND(AVG(COALESCE(report.act_ct,0)),2) AS act_ct,
                ROUND(AVG(COALESCE(report.sap_ct,0)),2) AS sap_ct,
                CASE WHEN AVG(COALESCE(report.act_ct,0))=0 THEN 0
                     ELSE ROUND((AVG(COALESCE(report.sap_ct,0))/AVG(COALESCE(report.act_ct,0)))*100,1)
                END AS efficiency
            FROM report
            INNER JOIN sap ON report.id_type=sap.id_type AND report.mould=sap.mould
            WHERE id_machine=@id_machine AND production_date BETWEEN @start_date AND @end_date AND report.id_type<>123456
            GROUP BY report.id_type, report.mould, sap.type
            ORDER BY shift_output DESC";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            type = reader["type"].ToString(),
            id_type = Convert.ToInt32(reader["id_type"]),
            mould = Convert.ToInt32(reader["mould"]),
            shot = Convert.ToInt32(reader["shot"]),
            qty_perct = Convert.ToInt32(reader["qty_perct"]),
            shift_output = Convert.ToInt32(reader["shift_output"]),
            part_weight = Convert.ToSingle(reader["part_weight"]),
            act_ct = Convert.ToSingle(reader["act_ct"]),
            sap_ct = Convert.ToSingle(reader["sap_ct"]),
            efficiency = Convert.ToSingle(reader["efficiency"]),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineDailyOutputAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        const string sql = @"
            SELECT production_date, SUM(COALESCE(shift_output,0)) AS shift_output
            FROM report
            WHERE id_machine=@id_machine AND production_date BETWEEN @start_date AND @end_date AND id_type<>123456
            GROUP BY production_date ORDER BY production_date";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            production_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["production_date"])).ToString("yyyy-MM-dd"),
            shift_output = Convert.ToInt32(reader["shift_output"]),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineDowntimeCategoryAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        var sql = $@"
            SELECT category, ROUND(SUM(DATEDIFF(SECOND,start,COALESCE(finish,GETDATE()))/3600.0),2) AS hours
            FROM machine_log_{id_machine}
            WHERE production_date BETWEEN @start_date AND @end_date AND NOT status_start=1 AND category IS NOT NULL
            GROUP BY category ORDER BY hours DESC";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            category = reader["category"].ToString(),
            hours = Convert.ToSingle(reader["hours"]),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineDowntimeEventsAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        var sql = $@"
            SELECT start, finish, category,
                ROUND(DATEDIFF(SECOND,start,COALESCE(finish,GETDATE()))/3600.0,2) AS duration,
                CASE shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift,
                COALESCE(problem,'') AS remark
            FROM machine_log_{id_machine}
            WHERE production_date BETWEEN @start_date AND @end_date AND NOT status_start=1
              AND category IS NOT NULL AND id_type<>123456
            ORDER BY start DESC";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            start = reader["start"].ToString(),
            finish = reader["finish"].ToString(),
            category = reader["category"].ToString(),
            duration = Convert.ToSingle(reader["duration"]),
            shift = reader["shift"].ToString(),
            remark = reader["remark"].ToString(),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineRejectAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        const string sql = @"
            SELECT production_date, CASE shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift,
                sap.type, reject.id_type, reject.mould,
                COALESCE(reject_panelling,0) AS reject_panelling, COALESCE(reject_lumpy,0) AS reject_lumpy,
                COALESCE(reject_black_dot,0) AS reject_black_dot, COALESCE(reject_burst,0) AS reject_burst,
                COALESCE(reject_startup,0)   AS reject_startup,   COALESCE(reject_preform,0) AS reject_preform,
                COALESCE(reject_purging,0)   AS reject_purging,   COALESCE(reject_others,0)  AS reject_others,
                COALESCE(total_weight,0)     AS total_weight
            FROM reject
            INNER JOIN sap ON reject.id_type=sap.id_type AND reject.mould=sap.mould
            WHERE id_machine=@id_machine AND production_date BETWEEN @start_date AND @end_date AND reject.id_type<>123456
            ORDER BY production_date, shift";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            production_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["production_date"])).ToString("yyyy-MM-dd"),
            shift = reader["shift"].ToString(),
            type = reader["type"].ToString(),
            id_type = Convert.ToInt32(reader["id_type"]),
            mould = Convert.ToInt32(reader["mould"]),
            reject_panelling = Convert.ToSingle(reader["reject_panelling"]),
            reject_lumpy = Convert.ToSingle(reader["reject_lumpy"]),
            reject_black_dot = Convert.ToSingle(reader["reject_black_dot"]),
            reject_burst = Convert.ToSingle(reader["reject_burst"]),
            reject_startup = Convert.ToSingle(reader["reject_startup"]),
            reject_preform = Convert.ToSingle(reader["reject_preform"]),
            reject_purging = Convert.ToSingle(reader["reject_purging"]),
            reject_others = Convert.ToSingle(reader["reject_others"]),
            total_weight = Convert.ToSingle(reader["total_weight"]),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineCycleTimeAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        const string sql = @"
            SELECT sap.type, report.id_type, report.mould,
                COALESCE(ROUND(AVG(NULLIF(report.act_ct,0)),2),0) AS act_ct,
                COALESCE(ROUND(AVG(NULLIF(sap.sap_ct,0)),2),0)   AS sap_ct
            FROM report
            INNER JOIN sap ON report.id_type=sap.id_type AND report.mould=sap.mould
            WHERE id_machine=@id_machine AND production_date BETWEEN @start_date AND @end_date AND report.id_type<>123456
            GROUP BY report.id_type, report.mould, sap.type ORDER BY type";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            type = reader["type"].ToString(),
            id_type = Convert.ToInt32(reader["id_type"]),
            mould = Convert.ToInt32(reader["mould"]),
            act_ct = Convert.ToSingle(reader["act_ct"]),
            sap_ct = Convert.ToSingle(reader["sap_ct"]),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineShiftPerformanceAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        const string sql = @"
            SELECT CASE shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift,
                SUM(COALESCE(shift_output,0)) AS shift_output,
                SUM(COALESCE(reject_total_pcs,0)) AS total_reject_pcs
            FROM report
            WHERE id_machine=@id_machine AND production_date BETWEEN @start_date AND @end_date AND id_type<>123456
            GROUP BY shift ORDER BY shift";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            shift = reader["shift"].ToString(),
            shift_output = Convert.ToInt32(reader["shift_output"]),
            total_reject_pcs = Convert.ToInt32(reader["total_reject_pcs"]),
        });
    }

    public async Task<IReadOnlyList<object>> LoadMachineUtilitiesAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);
        const string sql = @"
            SELECT utility_name, COALESCE(start,GETDATE()) AS start, COALESCE(finish,GETDATE()) AS finish,
                CASE category WHEN 1 THEN 'Running' ELSE 'Stop' END AS status,
                ROUND(DATEDIFF(SECOND,start,COALESCE(finish,GETDATE()))/3600.0,2) AS duration,
                CASE shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift
            FROM utilities
            WHERE id_machine=@id_machine AND production_date BETWEEN @start_date AND @end_date
            ORDER BY start DESC";

        return await ExecuteDetailQueryAsync(sql, id_machine, start, end, reader => new
        {
            utility_name = reader["utility_name"].ToString(),
            start = reader["start"].ToString(),
            finish = reader["finish"].ToString(),
            status = reader["status"].ToString(),
            duration = Convert.ToSingle(reader["duration"]),
            shift = reader["shift"].ToString(),
        });
    }

    public async Task<List<OEERawRow>> LoadOEERawForExport(DateOnly start_date, DateOnly end_date, int shift)
    {
        var time = DateTime.Now;
        var (productionDate, _) = GetProductionDate(time);

        string sql;

        if (start_date == end_date && start_date == productionDate)
        {
            sql = @"
                    WITH CombinedLogs AS (
                        SELECT 1  AS id_machine, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_1
                        UNION ALL SELECT 2,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_2
                        UNION ALL SELECT 3,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_3
                        UNION ALL SELECT 4,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_4
                        UNION ALL SELECT 5,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_5
                        UNION ALL SELECT 6,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_6
                        UNION ALL SELECT 7,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_7
                        UNION ALL SELECT 8,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_8
                        UNION ALL SELECT 9,  machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_9
                        UNION ALL SELECT 10, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_10
                        UNION ALL SELECT 11, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_11
                        UNION ALL SELECT 12, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_12
                        UNION ALL SELECT 13, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_13
                        UNION ALL SELECT 14, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_14
                        UNION ALL SELECT 15, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_15
                        UNION ALL SELECT 16, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_16
                        UNION ALL SELECT 17, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_17
                        UNION ALL SELECT 18, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_18
                        UNION ALL SELECT 19, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_19
                        UNION ALL SELECT 20, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_20
                        UNION ALL SELECT 21, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_21
                        UNION ALL SELECT 22, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_22
                        UNION ALL SELECT 23, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_23
                        UNION ALL SELECT 24, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_24
                        UNION ALL SELECT 25, machine_name, id_type, mould, start, finish, category, shot, act_ct, shift, production_date, status_start FROM machine_log_25
                    ),
                    MachineAvailable AS (
                        SELECT id_machine,
                               SUM(DATEDIFF(SECOND, start, COALESCE(finish, GETDATE()))) / 3600.0 AS available_hours
                        FROM CombinedLogs
                        WHERE production_date = @today AND shift = @shift
                        GROUP BY id_machine
                    ),
                    MachineAgg AS (
                        SELECT
                            cl.id_machine, cl.machine_name, cl.id_type, cl.mould,
                            SUM(cl.shot) AS shot,
                            SUM(CASE WHEN cl.category = 'PRODUCTION RUNNING' AND cl.status_start = 1
                                     THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                                     ELSE 0 END) AS run_time,
                            SUM(CASE WHEN cl.category <> 'PRODUCTION RUNNING' AND cl.category IS NOT NULL
                                     THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                                     ELSE 0 END) AS down_time,
                            SUM(CASE WHEN cl.category IS NULL
                                     THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                                     ELSE 0 END) AS unallocated,
                            SUM(CAST(cl.shot AS FLOAT) * COALESCE(NULLIF(cl.act_ct, 0), 0)) / 3600.0 AS total_actual_time
                        FROM CombinedLogs cl
                        WHERE cl.production_date = @today AND cl.shift = @shift
                        GROUP BY cl.id_machine, cl.machine_name, cl.id_type, cl.mould
                    ),
                    WithSAP AS (
                        SELECT
                            m.id_machine, m.machine_name, m.id_type, m.mould,
                            m.shot, m.run_time, m.down_time, m.unallocated,
                            s.type,
                            COALESCE((CAST(m.shot AS FLOAT) * COALESCE(s.qty_perct, 0) * COALESCE(s.part_weight, 0)) / 1000.0, 0) AS material_used,
                            COALESCE(s.sap_ct, 0)  AS sap_ct,
                            COALESCE(NULLIF(s.sap_ct, 0) * CAST(m.shot AS FLOAT) / 3600.0, 0) AS total_sap_time,
                            m.total_actual_time
                        FROM MachineAgg m
                        LEFT JOIN sap s ON s.id_type = m.id_type AND s.mould = m.mould
                    ),
                    WithReject AS (
                        SELECT
                            ws.id_machine, ws.machine_name, ws.id_type, ws.mould, ws.type,
                            ws.shot, ws.run_time, ws.down_time, ws.unallocated,
                            ws.material_used, ws.sap_ct,
                            ws.total_sap_time, ws.total_actual_time,
                            COALESCE(r.reject_black_dot + r.reject_burst + r.reject_lumpy
                                   + r.reject_others + r.reject_panelling, 0) AS reject_weight
                        FROM WithSAP ws
                        LEFT JOIN reject r
                            ON r.id_machine = ws.id_machine
                           AND r.id_type    = ws.id_type
                           AND r.mould      = ws.mould
                           AND r.production_date = @today
                           AND r.shift      = @shift
                    )
                    SELECT
                        w.id_machine,
                        w.machine_name,
                        COALESCE(w.id_type, 0)   AS id_type,
                        COALESCE(w.mould,   '')  AS mould,
                        COALESCE(w.type,    '')  AS type,
                        SUM(w.shot)              AS shot,
                        SUM(w.run_time)          AS run_time,
                        SUM(w.down_time)         AS down_time,
                        SUM(w.unallocated)       AS unallocated,
                        SUM(w.material_used)     AS material_used,
                        SUM(w.reject_weight)     AS reject_weight,
                        MAX(mt.available_hours)  AS available_hours,
                        CASE WHEN SUM(w.shot) = 0 THEN 0
                             ELSE SUM(w.total_sap_time) / SUM(w.shot) * 3600.0
                        END AS sap_ct,
                        CASE WHEN SUM(w.shot) = 0 THEN 0
                             ELSE SUM(w.total_actual_time) / SUM(w.shot) * 3600.0
                        END AS act_ct,
                        SUM(w.total_sap_time)    AS total_sap_time,
                        SUM(w.total_actual_time) AS total_actual_time
                    FROM WithReject w
                    LEFT JOIN MachineAvailable mt ON mt.id_machine = w.id_machine
                    GROUP BY w.id_machine, w.machine_name, w.id_type, w.mould, w.type
                    ORDER BY w.id_machine, w.id_type, w.mould;";
        }
        else
        {
            sql = @"
                    WITH AllDates AS (
                        SELECT DATEADD(DAY, n.n, @start_date) AS d
                        FROM (
                            SELECT TOP (DATEDIFF(DAY, @start_date, @end_date) + 1)
                                   ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
                            FROM sys.all_objects
                        ) n
                    ),
                    ShiftDates AS (
                        SELECT d.d AS production_date, s.shift
                        FROM AllDates d
                        CROSS JOIN (SELECT 1 AS shift UNION ALL SELECT 2) s
                    ),
                    EffectiveCalendar AS (
                        SELECT
                            sd.production_date, sd.shift,
                            CASE
                                WHEN EXISTS (SELECT 1 FROM calendar cx
                                             WHERE cx.production_date = sd.production_date
                                               AND cx.shift = sd.shift)
                                THEN COALESCE((SELECT SUM(c.planned_hours) FROM calendar c
                                               WHERE c.production_date = sd.production_date
                                                 AND c.shift = sd.shift), 0)
                                ELSE 12
                            END AS planned_hours
                        FROM ShiftDates sd
                    ),
                    ReportAgg AS (
                        SELECT
                            r.id_machine, r.machine_name, r.id_type, r.mould, r.production_date, r.shift,
                            MAX(s.type) AS type,
                            COALESCE(SUM(r.shot), 0) AS shot,
                            COALESCE(SUM(r.production_running), 0) AS run_time,
                            COALESCE(SUM(
                                COALESCE(r.change_full_set,  0) + COALESCE(r.change_half_set, 0) +
                                COALESCE(r.change_parts,     0) + COALESCE(r.maintenance_dt,  0) +
                                COALESCE(r.technician_dt,    0) + COALESCE(r.production_dt,   0)
                            ), 0) AS down_time,
                            COALESCE(SUM(r.unallocated), 0) AS unallocated,
                            COALESCE(SUM(r.material_used), 0) AS material_used,
                            COALESCE(SUM(r.reject_prod + r.reject_startup), 0) AS reject_weight,
                            SUM(CAST(COALESCE(r.shot, 0) AS FLOAT) * COALESCE(r.sap_ct, 0)) / 3600.0 AS total_sap_time,
                            SUM(CAST(COALESCE(r.shot, 0) AS FLOAT) * COALESCE(r.act_ct, 0)) / 3600.0 AS total_actual_time
                        FROM report r
                        INNER JOIN EffectiveCalendar ec
                            ON  ec.production_date = r.production_date
                            AND ec.shift           = r.shift
                            AND ec.planned_hours   > 0
                        LEFT JOIN sap s ON s.id_type = r.id_type AND s.mould = r.mould
                        WHERE r.production_date BETWEEN @start_date AND @end_date
                          AND r.id_machine <> 26
                        GROUP BY r.id_machine, r.machine_name, r.id_type, r.mould, r.production_date, r.shift
                    ),
                    MachineAvailable AS (
                        SELECT ra.id_machine, SUM(ec2.planned_hours) AS available_hours
                        FROM (SELECT DISTINCT id_machine, production_date, shift FROM ReportAgg) ra
                        INNER JOIN EffectiveCalendar ec2
                            ON ec2.production_date = ra.production_date
                           AND ec2.shift           = ra.shift
                        GROUP BY ra.id_machine
                    )
                    SELECT
                        ra.id_machine,
                        ra.machine_name,
                        COALESCE(ra.id_type, 0)  AS id_type,
                        COALESCE(ra.mould,   '')  AS mould,
                        COALESCE(ra.type,    '')  AS type,
                        SUM(ra.shot)             AS shot,
                        SUM(ra.run_time)         AS run_time,
                        SUM(ra.down_time)        AS down_time,
                        SUM(ra.unallocated)      AS unallocated,
                        SUM(ra.material_used)    AS material_used,
                        SUM(ra.reject_weight)    AS reject_weight,
                        COALESCE(MAX(ma.available_hours), 0) AS available_hours,
                        CASE WHEN SUM(ra.shot) = 0 THEN 0
                             ELSE SUM(ra.total_sap_time)    / SUM(ra.shot) * 3600.0
                        END AS sap_ct,
                        CASE WHEN SUM(ra.shot) = 0 THEN 0
                             ELSE SUM(ra.total_actual_time) / SUM(ra.shot) * 3600.0
                        END AS act_ct,
                        SUM(ra.total_sap_time)   AS total_sap_time,
                        SUM(ra.total_actual_time) AS total_actual_time
                    FROM ReportAgg ra
                    LEFT JOIN MachineAvailable ma ON ma.id_machine = ra.id_machine
                    GROUP BY ra.id_machine, ra.machine_name, ra.id_type, ra.mould, ra.type
                    ORDER BY ra.id_machine, ra.id_type, ra.mould;";
        }

        var result = new List<OEERawRow>();
        using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@today", productionDate);
        cmd.Parameters.AddWithValue("@start_date", start_date);
        cmd.Parameters.AddWithValue("@end_date", end_date);
        cmd.Parameters.AddWithValue("@shift", shift);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new OEERawRow(
                IdMachine: Convert.ToInt32(reader["id_machine"]),
                MachineName: Convert.ToString(reader["machine_name"]) ?? string.Empty,
                IdType: Convert.ToInt32(reader["id_type"]),
                Mould: Convert.ToString(reader["mould"]) ?? string.Empty,
                Type: Convert.ToString(reader["type"]) ?? string.Empty,
                Shot: Convert.ToDouble(reader["shot"]),
                RunTime: Convert.ToDouble(reader["run_time"]),
                DownTime: Convert.ToDouble(reader["down_time"]),
                Unallocated: Convert.ToDouble(reader["unallocated"]),
                MaterialUsed: Convert.ToDouble(reader["material_used"]),
                RejectWeight: Convert.ToDouble(reader["reject_weight"]),
                AvailableHours: Convert.ToDouble(reader["available_hours"]),
                SapCt: Convert.ToDouble(reader["sap_ct"]),
                ActCt: Convert.ToDouble(reader["act_ct"])
            ));
        }
        return result;
    }
    // ── Generic executor ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<object>> ExecuteDetailQueryAsync(
        string sql, int id_machine, DateOnly start, DateOnly end,
        Func<SqlDataReader, object> mapper)
    {
        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id_machine", id_machine);
        cmd.Parameters.AddWithValue("@start_date", start);
        cmd.Parameters.AddWithValue("@end_date", end);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(mapper(reader));
        return result;
    }
}