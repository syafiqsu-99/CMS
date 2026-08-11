using Microsoft.Data.SqlClient;
namespace CMS.Server.Services;

public class OEEService(MainPlcService mainPlcService, string connectionString, ILogger<BaseService> logger) : BaseService(connectionString, mainPlcService, logger)
{
    public async Task<IReadOnlyList<object>> CalculateOeeAsync(DateOnly startDate, DateOnly endDate)
    {
        var (today, currentShift) = GetProductionDate(DateTime.Now);
        bool isToday = startDate == endDate && startDate == today;

        string sql = isToday
            ? await BuildTodayOeeSqlAsync(today, currentShift)
            : await BuildRangeOeeSqlAsync();

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@today", today);
        cmd.Parameters.AddWithValue("@start_date", startDate);
        cmd.Parameters.AddWithValue("@end_date", endDate);
        if (isToday)
        {
            cmd.Parameters.AddWithValue("@currentShift", currentShift);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                machine_name = reader["machine_name"].ToString(),
                run_time = Convert.ToSingle(reader["run_time"]),
                unplanned_dt = Convert.ToSingle(reader["unplanned_dt"]),
                planned_dt = Convert.ToSingle(reader["planned_dt"]),
                operating_time = Convert.ToSingle(reader["operating_time"]),
                total_actual_time = Convert.ToSingle(reader["total_actual_time"]),
                total_sap_time = Convert.ToSingle(reader["total_sap_time"]),
                material_used = Convert.ToSingle(reader["material_used"]),
                reject_weight = Convert.ToSingle(reader["reject_weight"]),
                change_full_set = Convert.ToSingle(reader["change_full_set"]),
                change_half_set = Convert.ToSingle(reader["change_half_set"]),
                change_parts = Convert.ToSingle(reader["change_parts"]),
                maintenance_dt = Convert.ToSingle(reader["maintenance_dt"]),
                technician_dt = Convert.ToSingle(reader["technician_dt"]),
                production_dt = Convert.ToSingle(reader["production_dt"]),
                buyoff_dt = Convert.ToSingle(reader["buyoff_dt"]),
                availability = Convert.ToSingle(reader["availability"]),
                performance = Convert.ToSingle(reader["performance"]),
                quality = Convert.ToSingle(reader["quality"]),
                oee = Convert.ToSingle(reader["oee"]),
            });
        }
        return result;
    }

    private async Task<string> BuildTodayOeeSqlAsync(DateOnly today, int currentShift)
    {
        var logCte = await BuildMachineLogUnionAsync("production_date = @today and shift = @currentShift");

        return $@"
            WITH CombinedLogs AS (
                {logCte}
            ),
            ShiftDayType AS (
                SELECT COALESCE(
                    (SELECT TOP 1 day_type FROM calendar
                     WHERE production_date = @today AND shift = @currentShift),
                    'NORMAL'
                ) AS day_type
            ),
            ShiftHasRun AS (
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM CombinedLogs WHERE category = 'PRODUCTION RUNNING'
                ) THEN 1 ELSE 0 END AS has_run
            ),
            MachineAgg AS (
                SELECT cl.id_machine, cl.machine_name, cl.id_type, cl.mould,
                    SUM(cl.shot) AS shot,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category = 'PRODUCTION RUNNING' AND cl.status_start = 1
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS run_time,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category IS NOT NULL
                             AND cl.category NOT IN ('PRODUCTION RUNNING', 'NO SCHEDULE', 'SCHEDULED MAINTENANCE')
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS unplanned_dt,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category IN ('NO SCHEDULE', 'SCHEDULED MAINTENANCE')
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS planned_dt,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category = 'MOULD CHANGE' AND cl.mould_category = 1
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS change_full_set,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category = 'MOULD CHANGE' AND cl.mould_category = 2
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS change_half_set,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category = 'MOULD CHANGE' AND cl.mould_category = 3
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS change_parts,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category IN ('MACHINE BREAKDOWN', 'OTHERS MAIN')
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS maintenance_dt,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category IN ('QUALITY ISSUE', 'SAMPLE RUNNING', 'OTHERS TECH')
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS technician_dt,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category IN ('NO OPERATOR', 'MATERIAL DRYING', 'OTHERS PROD')
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS production_dt,
                    SUM(CASE
                        WHEN sdt.day_type = 'OFFDAY' THEN 0
                        WHEN sdt.day_type = 'OVERTIME' AND shr.has_run = 0 THEN 0
                        WHEN cl.category IN ('PRODUCT BUYOFF')
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS buyoff_dt,
                    AVG(NULLIF(cl.act_ct, 0)) AS act_ct
                FROM CombinedLogs cl
                CROSS JOIN ShiftDayType sdt
                CROSS JOIN ShiftHasRun shr
                GROUP BY cl.id_machine, cl.machine_name, cl.id_type, cl.mould
            ),
            WithSAP AS (
                SELECT m.*,
                    COALESCE((m.shot * s.qty_perct * s.part_weight)/1000.0,0) AS material_used,
                    COALESCE(m.shot * s.sap_ct,0) / 3600.0 AS total_sap_time,
                    COALESCE(m.shot * m.act_ct,0) / 3600.0 AS total_actual_time
                FROM MachineAgg m
                LEFT JOIN sap s ON s.id_type=m.id_type AND s.mould=m.mould
            ),
            WithReject AS (
                SELECT w.*,
                    COALESCE(r.reject_black_dot+r.reject_burst+r.reject_lumpy+r.reject_others+r.reject_panelling,0) AS reject_weight
                FROM WithSAP w
                LEFT JOIN reject r
                    ON r.id_machine=w.id_machine AND r.id_type=w.id_type AND r.mould=w.mould
                    AND r.production_date=@today AND r.shift=@currentShift
            ),
            MachineSummary AS (
                SELECT
                    w.id_machine,
                    w.machine_name,
                    SUM(w.run_time)          AS run_time,
                    SUM(w.unplanned_dt)      AS unplanned_dt,
                    SUM(w.planned_dt)        AS planned_dt,
                    SUM(w.change_full_set)   AS change_full_set,
                    SUM(w.change_half_set)   AS change_half_set,
                    SUM(w.change_parts)      AS change_parts,
                    SUM(w.maintenance_dt)    AS maintenance_dt,
                    SUM(w.technician_dt)     AS technician_dt,
                    SUM(w.production_dt)     AS production_dt,
                    SUM(w.buyoff_dt)         AS buyoff_dt,
                    SUM(w.material_used)     AS material_used,
                    SUM(w.reject_weight)     AS reject_weight,
                    SUM(w.total_sap_time)    AS total_sap_time,
                    SUM(w.total_actual_time) AS total_actual_time,
                    SUM(w.run_time) + SUM(w.unplanned_dt) AS operating_time
                FROM WithReject w
                GROUP BY w.id_machine, w.machine_name
            )
            SELECT id_machine, machine_name, run_time, unplanned_dt, planned_dt, operating_time,
                change_full_set, change_half_set, change_parts, maintenance_dt, technician_dt, production_dt, buyoff_dt,
                material_used, reject_weight, total_actual_time, total_sap_time,
                CASE WHEN (run_time+unplanned_dt)=0 THEN 0 ELSE (run_time*1.0/(run_time+unplanned_dt))*100 END AS availability,
                CASE WHEN total_actual_time=0    THEN 0 ELSE (total_sap_time*1.0/total_actual_time)*100 END AS performance,
                CASE WHEN material_used=0        THEN 0
                     WHEN ((material_used-reject_weight)*1.0/material_used) < 0 THEN 0
                     ELSE ((material_used-reject_weight)*1.0/material_used)*100 END AS quality,
                CASE WHEN (run_time+unplanned_dt)=0 OR total_actual_time=0 OR material_used=0 OR (material_used-reject_weight) < 0 THEN 0
                     ELSE (run_time*1.0/(run_time+unplanned_dt))*(total_sap_time*1.0/total_actual_time)*((material_used-reject_weight)*1.0/material_used)*100
                END AS oee
            FROM MachineSummary
            WHERE id_machine <> 0
            ORDER BY id_machine;";
    }

    private async Task<string> BuildRangeOeeSqlAsync()
    {
        var logCte = await BuildMachineLogUnionAsync("production_date BETWEEN @start_date AND @end_date");

        return $@"
        WITH CombinedLogs AS (
            {logCte}
        ),
        AllDates AS (
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
                sd.production_date,
                sd.shift,
                COALESCE(
                    (SELECT TOP 1 cx.day_type
                     FROM calendar cx
                     WHERE cx.production_date = sd.production_date
                       AND cx.shift           = sd.shift),
                    'NORMAL'
                ) AS day_type
            FROM ShiftDates sd
        ),
        OvertimeRunCheck AS (
            SELECT cl.id_machine, cl.production_date, cl.shift,
                CASE WHEN SUM(CASE WHEN cl.category = 'PRODUCTION RUNNING' THEN 1 ELSE 0 END) > 0
                     THEN 1 ELSE 0
                END AS has_run
            FROM CombinedLogs cl
            INNER JOIN EffectiveCalendar ec
                ON  ec.production_date = cl.production_date
                AND ec.shift           = cl.shift
                AND ec.day_type        = 'OVERTIME'
            GROUP BY cl.id_machine, cl.production_date, cl.shift
        ),
        ReportAgg AS (
            SELECT
                r.id_machine, r.machine_name, r.id_type, r.mould,
                r.production_date, r.shift,
                COALESCE(SUM(r.shot), 0) AS shot,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE r.production_running
                END), 0) AS run_time,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.change_full_set, 0) + COALESCE(r.change_half_set, 0)
                       + COALESCE(r.change_parts,    0) + COALESCE(r.maintenance_dt,  0)
                       + COALESCE(r.technician_dt,   0) + COALESCE(r.production_dt,   0)
                       + COALESCE(r.buyoff_dt,       0)
                END), 0) AS unplanned_dt,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.planned_dt, 0)
                END), 0) AS planned_dt,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.change_full_set, 0)
                END), 0) AS change_full_set,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.change_half_set, 0)
                END), 0) AS change_half_set,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.change_parts, 0)
                END), 0) AS change_parts,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.maintenance_dt, 0)
                END), 0) AS maintenance_dt,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.technician_dt, 0)
                END), 0) AS technician_dt,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.production_dt, 0)
                END), 0) AS production_dt,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OFFDAY' THEN 0
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.buyoff_dt, 0)
                END), 0) AS buyoff_dt,
                COALESCE(SUM(r.material_used),  0) AS material_used,
                COALESCE(SUM(r.reject_prod + r.reject_startup), 0) AS reject_weight,
                SUM(COALESCE(r.shot, 0) * COALESCE(r.sap_ct, 0)) AS total_sap_time_raw,
                SUM(COALESCE(r.shot, 0) * COALESCE(r.act_ct, 0)) AS total_actual_time_raw
            FROM report r
            INNER JOIN EffectiveCalendar ec
                ON  ec.production_date = r.production_date
                AND ec.shift           = r.shift
                AND ec.day_type        <> 'OFFDAY'
            LEFT JOIN OvertimeRunCheck orc
                ON  orc.id_machine      = r.id_machine
                AND orc.production_date = r.production_date
                AND orc.shift           = r.shift
            WHERE r.production_date BETWEEN @start_date AND @end_date
              AND r.id_machine <> 0
            GROUP BY r.id_machine, r.machine_name, r.id_type, r.mould,
                     r.production_date, r.shift
        ),
        ProductSummary AS (
            SELECT
                ra.id_machine,
                ra.machine_name,
                ra.id_type,
                ra.mould,
                SUM(ra.shot)            AS shot,
                SUM(ra.run_time)        AS run_time,
                SUM(ra.unplanned_dt)    AS unplanned_dt,
                SUM(ra.planned_dt)      AS planned_dt,
                SUM(ra.change_full_set) AS change_full_set,
                SUM(ra.change_half_set) AS change_half_set,
                SUM(ra.change_parts)    AS change_parts,
                SUM(ra.maintenance_dt)  AS maintenance_dt,
                SUM(ra.technician_dt)   AS technician_dt,
                SUM(ra.production_dt)   AS production_dt,
                SUM(ra.buyoff_dt)       AS buyoff_dt,
                SUM(ra.material_used)   AS material_used,
                SUM(ra.reject_weight)   AS reject_weight,
                SUM(ra.total_sap_time_raw)    / 3600.0 AS total_sap_time,
                SUM(ra.total_actual_time_raw) / 3600.0 AS total_actual_time
            FROM ReportAgg ra
            GROUP BY ra.id_machine, ra.machine_name, ra.id_type, ra.mould
        ),
        MachineSummary AS (
            SELECT
                ps.id_machine,
                ps.machine_name,
                SUM(ps.run_time)          AS run_time,
                SUM(ps.unplanned_dt)      AS unplanned_dt,
                SUM(ps.planned_dt)        AS planned_dt,
                SUM(ps.change_full_set)   AS change_full_set,
                SUM(ps.change_half_set)   AS change_half_set,
                SUM(ps.change_parts)      AS change_parts,
                SUM(ps.maintenance_dt)    AS maintenance_dt,
                SUM(ps.technician_dt)     AS technician_dt,
                SUM(ps.production_dt)     AS production_dt,
                SUM(ps.buyoff_dt)         AS buyoff_dt,
                SUM(ps.material_used)     AS material_used,
                SUM(ps.reject_weight)     AS reject_weight,
                SUM(ps.total_sap_time)    AS total_sap_time,
                SUM(ps.total_actual_time) AS total_actual_time,
                SUM(ps.run_time) + SUM(ps.unplanned_dt) AS operating_time
            FROM ProductSummary ps
            GROUP BY ps.id_machine, ps.machine_name
        )
        SELECT
            id_machine, machine_name,
            run_time, unplanned_dt, planned_dt, operating_time,
            change_full_set, change_half_set, change_parts, maintenance_dt, technician_dt, production_dt, buyoff_dt,
            material_used, reject_weight, total_actual_time, total_sap_time,
            CASE WHEN NULLIF(operating_time, 0) IS NULL THEN 0
                 ELSE (run_time * 1.0 / operating_time) * 100
            END AS availability,
            CASE WHEN total_actual_time = 0 THEN 0
                 ELSE (total_sap_time * 1.0 / total_actual_time) * 100
            END AS performance,
            CASE WHEN material_used = 0 THEN 0
                 WHEN ((material_used - reject_weight) * 1.0 / material_used) < 0 THEN 0
                 ELSE ((material_used - reject_weight) * 1.0 / material_used) * 100
            END AS quality,
            CASE WHEN NULLIF(operating_time, 0) IS NULL
                   OR total_actual_time = 0
                   OR material_used = 0
                   OR (material_used - reject_weight) < 0 THEN 0
                 ELSE (run_time * 1.0 / operating_time) *
                      (total_sap_time * 1.0 / total_actual_time) *
                      ((material_used - reject_weight) * 1.0 / material_used) * 100
            END AS oee
        FROM MachineSummary
        ORDER BY id_machine;";
    }

    public async Task<IReadOnlyList<object>> CalculateMonthlyOeeAsync(int year)
    {
        var logCte = await BuildMachineLogUnionAsync("production_date BETWEEN @year_start AND @year_end");

        string sql = $@"
        WITH CombinedLogs AS (
            {logCte}
        ),
        AllDates AS (
            SELECT DATEADD(DAY, n.n, @year_start) AS d
            FROM (
                SELECT TOP (DATEDIFF(DAY, @year_start, @year_end) + 1)
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
                sd.production_date,
                sd.shift,
                COALESCE(
                    (SELECT TOP 1 cx.day_type
                     FROM calendar cx
                     WHERE cx.production_date = sd.production_date
                       AND cx.shift           = sd.shift),
                    'NORMAL'
                ) AS day_type
            FROM ShiftDates sd
        ),
        OvertimeRunCheck AS (
            SELECT cl.id_machine, cl.production_date, cl.shift,
                CASE WHEN SUM(CASE WHEN cl.category = 'PRODUCTION RUNNING' THEN 1 ELSE 0 END) > 0
                     THEN 1 ELSE 0
                END AS has_run
            FROM CombinedLogs cl
            INNER JOIN EffectiveCalendar ec
                ON  ec.production_date = cl.production_date
                AND ec.shift           = cl.shift
                AND ec.day_type        = 'OVERTIME'
            GROUP BY cl.id_machine, cl.production_date, cl.shift
        ),
        ReportAgg AS (
            SELECT
                MONTH(r.production_date) AS month_no,
                r.id_machine,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE r.production_running
                END), 0) AS run_time,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.change_full_set, 0) + COALESCE(r.change_half_set, 0)
                       + COALESCE(r.change_parts,    0) + COALESCE(r.maintenance_dt,  0)
                       + COALESCE(r.technician_dt,   0) + COALESCE(r.production_dt,   0)
                       + COALESCE(r.buyoff_dt,       0)
                END), 0) AS unplanned_dt,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE r.material_used
                END), 0) AS material_used,
                COALESCE(SUM(CASE
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE r.reject_prod + r.reject_startup
                END), 0) AS reject_weight,
                SUM(CASE
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.shot, 0) * COALESCE(r.sap_ct, 0)
                END) AS total_sap_time_raw,
                SUM(CASE
                    WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                    ELSE COALESCE(r.shot, 0) * COALESCE(r.act_ct, 0)
                END) AS total_actual_time_raw
            FROM report r
            INNER JOIN EffectiveCalendar ec
                ON  ec.production_date = r.production_date
                AND ec.shift           = r.shift
                AND ec.day_type        <> 'OFFDAY'
            LEFT JOIN OvertimeRunCheck orc
                ON  orc.id_machine      = r.id_machine
                AND orc.production_date = r.production_date
                AND orc.shift           = r.shift
            WHERE r.id_machine <> 0
              AND r.production_date BETWEEN @year_start AND @year_end
            GROUP BY MONTH(r.production_date), r.id_machine
        )
        SELECT
            month_no,
            id_machine,
            run_time,
            unplanned_dt,
            material_used,
            reject_weight,
            total_sap_time_raw,
            total_actual_time_raw
        FROM ReportAgg
        ORDER BY month_no, id_machine;";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@year_start", new DateOnly(year, 1, 1));
        cmd.Parameters.AddWithValue("@year_end", new DateOnly(year, 12, 31));

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                month_no = Convert.ToInt32(reader["month_no"]),
                id_machine = Convert.ToInt32(reader["id_machine"]),
                run_time = Convert.ToDouble(reader["run_time"]),
                unplanned_dt = Convert.ToDouble(reader["unplanned_dt"]),
                material_used = Convert.ToDouble(reader["material_used"]),
                reject_weight = Convert.ToDouble(reader["reject_weight"]),
                total_sap_time = Convert.ToDouble(reader["total_sap_time_raw"]),
                total_actual_time = Convert.ToDouble(reader["total_actual_time_raw"]),
            });
        }
        return result;
    }
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
        var logUnion = await BuildMachineLogUnionAsync("production_date BETWEEN @start_date AND @end_date");

        var sql = $@"
            WITH CombinedLogs AS (
                {logUnion}
            )
            SELECT TOP 10
                sap.id_type,
                sap.type,
                ROUND(SUM(DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE()))) / 3600.0, 2) AS hours
            FROM CombinedLogs ml
            INNER JOIN sap
                ON  sap.id_type = ml.id_type
                AND sap.mould   = ml.mould
            WHERE ml.id_type <> 123456
              AND ml.category NOT IN ('PRODUCTION RUNNING', 'NO SCHEDULE', 'SCHEDULED MAINTENANCE')
            GROUP BY sap.id_type, sap.type
            ORDER BY hours DESC;";

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@start_date", start_date);
        cmd.Parameters.AddWithValue("@end_date", end_date);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                id_type = reader["id_type"] == DBNull.Value ? 0 : Convert.ToInt32(reader["id_type"]),
                type = reader["type"] == DBNull.Value ? string.Empty : Convert.ToString(reader["type"]),
                hours = reader["hours"] == DBNull.Value ? 0.0 : Convert.ToDouble(reader["hours"]),
            });
        }

        return result;
    }

    // ── Machine Detail ─────────────────────────────────────────────────────────

    public async Task<object> LoadMachineDetailAsync(int id_machine, DateOnly start, DateOnly end)
    {
        ValidateMachineId(id_machine);

        const string calendarCtes = @"
            AllDates AS (
                SELECT DATEADD(DAY, n.n, @start_date) AS d
                FROM (
                    SELECT TOP (DATEDIFF(DAY, @start_date, @end_date) + 1)
                           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
                    FROM sys.all_objects
                ) n
            ),
            ShiftDates AS (
                SELECT d.d AS production_date, s.shift
                FROM AllDates d CROSS JOIN (SELECT 1 AS shift UNION ALL SELECT 2) s
            ),
            EffectiveCalendar AS (
                SELECT sd.production_date, sd.shift,
                    COALESCE(
                        (SELECT TOP 1 cx.day_type FROM calendar cx
                         WHERE cx.production_date = sd.production_date
                           AND cx.shift           = sd.shift),
                        'NORMAL'
                    ) AS day_type
                FROM ShiftDates sd
            ),
            OvertimeRunCheck AS (
                SELECT ml.production_date, ml.shift,
                    CASE WHEN SUM(CASE WHEN UPPER(ml.category) = 'PRODUCTION RUNNING' THEN 1 ELSE 0 END) > 0
                         THEN 1 ELSE 0
                    END AS has_run
                FROM machine_log_{0} ml
                INNER JOIN EffectiveCalendar ec
                    ON  ec.production_date = ml.production_date
                    AND ec.shift           = ml.shift
                    AND ec.day_type        = 'OVERTIME'
                GROUP BY ml.production_date, ml.shift
            ),
            -- Shifts that count for report-based metrics (OFFDAY excluded; OVERTIME only if has_run)
            ValidShifts AS (
                SELECT ec.production_date, ec.shift
                FROM EffectiveCalendar ec
                WHERE ec.day_type <> 'OFFDAY'
                  AND NOT (
                        ec.day_type = 'OVERTIME'
                        AND COALESCE((
                            SELECT orc.has_run FROM OvertimeRunCheck orc
                            WHERE orc.production_date = ec.production_date
                              AND orc.shift           = ec.shift
                        ), 0) = 0
                  )
        )";

        // ── 1. Product Output ──────────────────────────────────────────────────────
        var sqlProductOutput = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                s.type, r.id_type, r.mould,
                SUM(COALESCE(r.shot,         0))  AS shot,
                MAX(COALESCE(r.qty_perct,    0))  AS qty_perct,
                SUM(COALESCE(r.shift_output, 0))  AS shift_output,
                MAX(COALESCE(r.part_weight,  0))  AS part_weight,
                ROUND(AVG(NULLIF(r.act_ct,   0)), 2) AS act_ct,
                COALESCE(MAX(s.sap_ct), 0)           AS sap_ct,
                CASE WHEN AVG(NULLIF(r.act_ct, 0)) IS NULL OR AVG(NULLIF(r.act_ct, 0)) = 0 THEN 0
                     ELSE ROUND((COALESCE(MAX(s.sap_ct), 0) / AVG(NULLIF(r.act_ct, 0))) * 100, 1)
                END AS efficiency
            FROM report r
            INNER JOIN sap s ON s.id_type = r.id_type AND s.mould = r.mould
            INNER JOIN ValidShifts vs ON vs.production_date = r.production_date AND vs.shift = r.shift
            WHERE r.id_machine = @id_machine
              AND r.production_date BETWEEN @start_date AND @end_date
              AND r.id_type <> 123456
            GROUP BY r.id_type, r.mould, s.type
            ORDER BY shift_output DESC;";

        // ── 2. Daily Output ────────────────────────────────────────────────────────
        var sqlDailyOutput = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                r.production_date,
                SUM(COALESCE(r.shift_output, 0)) AS shift_output
            FROM report r
            INNER JOIN ValidShifts vs ON vs.production_date = r.production_date AND vs.shift = r.shift
            WHERE r.id_machine = @id_machine
              AND r.production_date BETWEEN @start_date AND @end_date
              AND r.id_type <> 123456
            GROUP BY r.production_date
            ORDER BY r.production_date;";

        // ── 3. Downtime Category ───────────────────────────────────────────────────
        var sqlDowntimeCategory = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                ml.category,
                ROUND(SUM(DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) / 3600.0), 2) AS hours
            FROM machine_log_{id_machine} ml
            INNER JOIN ValidShifts vs ON vs.production_date = ml.production_date AND vs.shift = ml.shift
            WHERE ml.production_date BETWEEN @start_date AND @end_date
              AND ml.status_start <> 1
              AND ml.category IS NOT NULL
            GROUP BY ml.category
            ORDER BY hours DESC;";

        // ── 4. Downtime Events ─────────────────────────────────────────────────────
        var sqlDowntimeEvents = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                ml.start, ml.finish, ml.category,
                ROUND(DATEDIFF(SECOND, ml.start, COALESCE(ml.finish, GETDATE())) / 3600.0, 2) AS duration,
                CASE ml.shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift,
                COALESCE(ml.problem, '') AS remark
            FROM machine_log_{id_machine} ml
            INNER JOIN ValidShifts vs ON vs.production_date = ml.production_date AND vs.shift = ml.shift
            WHERE ml.production_date BETWEEN @start_date AND @end_date
              AND ml.status_start <> 1
              AND ml.category IS NOT NULL
              AND ml.id_type <> 123456
            ORDER BY ml.start DESC;";

        // ── 5. Reject ──────────────────────────────────────────────────────────────
        var sqlReject = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                rej.production_date,
                CASE rej.shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift,
                s.type, rej.id_type, rej.mould,
                COALESCE(rej.reject_panelling,  0) AS reject_panelling,
                COALESCE(rej.reject_lumpy,      0) AS reject_lumpy,
                COALESCE(rej.reject_black_dot,  0) AS reject_black_dot,
                COALESCE(rej.reject_burst,      0) AS reject_burst,
                COALESCE(rej.reject_startup,    0) AS reject_startup,
                COALESCE(rej.reject_preform,    0) AS reject_preform,
                COALESCE(rej.reject_purging,    0) AS reject_purging,
                COALESCE(rej.reject_others,     0) AS reject_others,
                COALESCE(rej.total_weight,      0) AS total_weight
            FROM reject rej
            INNER JOIN sap s ON s.id_type = rej.id_type AND s.mould = rej.mould
            INNER JOIN ValidShifts vs ON vs.production_date = rej.production_date AND vs.shift = rej.shift
            WHERE rej.id_machine = @id_machine
              AND rej.production_date BETWEEN @start_date AND @end_date
              AND rej.id_type <> 123456
            ORDER BY rej.production_date, rej.shift;";

        // ── 6. Cycle Time ──────────────────────────────────────────────────────────
        var sqlCycleTime = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                s.type, r.id_type, r.mould,
                COALESCE(ROUND(AVG(NULLIF(r.act_ct, 0)), 2), 0) AS act_ct,
                COALESCE(MAX(s.sap_ct), 0)                       AS sap_ct
            FROM report r
            INNER JOIN sap s ON s.id_type = r.id_type AND s.mould = r.mould
            INNER JOIN ValidShifts vs ON vs.production_date = r.production_date AND vs.shift = r.shift
            WHERE r.id_machine = @id_machine
              AND r.production_date BETWEEN @start_date AND @end_date
              AND r.id_type <> 123456
            GROUP BY r.id_type, r.mould, s.type
            ORDER BY s.type;";

        // ── 7. Shift Performance ───────────────────────────────────────────────────
        var sqlShiftPerf = $@"
            WITH {string.Format(calendarCtes, id_machine)}
            SELECT
                CASE r.shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift,
                SUM(COALESCE(r.shift_output,     0)) AS shift_output,
                SUM(COALESCE(r.reject_total_pcs, 0)) AS total_reject_pcs
            FROM report r
            INNER JOIN ValidShifts vs ON vs.production_date = r.production_date AND vs.shift = r.shift
            WHERE r.id_machine = @id_machine
              AND r.production_date BETWEEN @start_date AND @end_date
              AND r.id_type <> 123456
            GROUP BY r.shift
            ORDER BY r.shift;";

        // ── 8. Utilities ──────────────────────────────────
        const string sqlUtilities = @"
            SELECT
                utility_name,
                COALESCE(start,  GETDATE()) AS start,
                COALESCE(finish, GETDATE()) AS finish,
                CASE category WHEN 1 THEN 'Running' ELSE 'Stop' END AS status,
                ROUND(DATEDIFF(SECOND, start, COALESCE(finish, GETDATE())) / 3600.0, 2) AS duration,
                CASE shift WHEN 1 THEN 'Morning' ELSE 'Night' END AS shift
            FROM utilities
            WHERE id_machine = @id_machine
              AND production_date BETWEEN @start_date AND @end_date
            ORDER BY start DESC;";

        await using var conn = await CreateConnectionAsync();

        async Task<List<T>> Query<T>(string sql, Func<SqlDataReader, T> map)
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_machine", id_machine);
            cmd.Parameters.AddWithValue("@start_date", start);
            cmd.Parameters.AddWithValue("@end_date", end);
            var rows = new List<T>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(map(reader));
            return rows;
        }

        var productOutput = await Query(sqlProductOutput, r => (object)new
        {
            type = r["type"].ToString(),
            id_type = Convert.ToInt32(r["id_type"]),
            mould = Convert.ToInt32(r["mould"]),
            shot = Convert.ToInt32(r["shot"]),
            qty_perct = Convert.ToInt32(r["qty_perct"]),
            shift_output = Convert.ToInt32(r["shift_output"]),
            part_weight = Convert.ToSingle(r["part_weight"]),
            act_ct = Convert.ToSingle(r["act_ct"]),
            sap_ct = Convert.ToSingle(r["sap_ct"]),
            efficiency = Convert.ToSingle(r["efficiency"]),
        });

        var dailyOutput = await Query(sqlDailyOutput, r => (object)new
        {
            production_date = DateOnly.FromDateTime(Convert.ToDateTime(r["production_date"])).ToString("yyyy-MM-dd"),
            shift_output = Convert.ToInt32(r["shift_output"]),
        });

        var downtimeCategory = await Query(sqlDowntimeCategory, r => (object)new
        {
            category = r["category"].ToString(),
            hours = Convert.ToSingle(r["hours"]),
        });

        var downtimeEvents = await Query(sqlDowntimeEvents, r => (object)new
        {
            start = r["start"].ToString(),
            finish = r["finish"].ToString(),
            category = r["category"].ToString(),
            duration = Convert.ToSingle(r["duration"]),
            shift = r["shift"].ToString(),
            remark = r["remark"].ToString(),
        });

        var reject = await Query(sqlReject, r => (object)new
        {
            production_date = DateOnly.FromDateTime(Convert.ToDateTime(r["production_date"])).ToString("yyyy-MM-dd"),
            shift = r["shift"].ToString(),
            type = r["type"].ToString(),
            id_type = Convert.ToInt32(r["id_type"]),
            mould = Convert.ToInt32(r["mould"]),
            reject_panelling = Convert.ToSingle(r["reject_panelling"]),
            reject_lumpy = Convert.ToSingle(r["reject_lumpy"]),
            reject_black_dot = Convert.ToSingle(r["reject_black_dot"]),
            reject_burst = Convert.ToSingle(r["reject_burst"]),
            reject_startup = Convert.ToSingle(r["reject_startup"]),
            reject_preform = Convert.ToSingle(r["reject_preform"]),
            reject_purging = Convert.ToSingle(r["reject_purging"]),
            reject_others = Convert.ToSingle(r["reject_others"]),
            total_weight = Convert.ToSingle(r["total_weight"]),
        });

        var cycleTime = await Query(sqlCycleTime, r => (object)new
        {
            type = r["type"].ToString(),
            id_type = Convert.ToInt32(r["id_type"]),
            mould = Convert.ToInt32(r["mould"]),
            act_ct = Convert.ToSingle(r["act_ct"]),
            sap_ct = Convert.ToSingle(r["sap_ct"]),
        });

        var shiftPerf = await Query(sqlShiftPerf, r => (object)new
        {
            shift = r["shift"].ToString(),
            shift_output = Convert.ToInt32(r["shift_output"]),
            total_reject_pcs = Convert.ToInt32(r["total_reject_pcs"]),
        });

        var utilities = await Query(sqlUtilities, r => (object)new
        {
            utility_name = r["utility_name"].ToString(),
            start = r["start"].ToString(),
            finish = r["finish"].ToString(),
            status = r["status"].ToString(),
            duration = Convert.ToSingle(r["duration"]),
            shift = r["shift"].ToString(),
        });

        return new
        {
            product_output = productOutput,
            daily_output = dailyOutput,
            downtime_category = downtimeCategory,
            downtime_events = downtimeEvents,
            reject = reject,
            cycle_time = cycleTime,
            shift_performance = shiftPerf,
            utilities = utilities,
        };
    }

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

    public async Task<List<OEERawRow>> LoadOEERawForExport(DateOnly start_date, DateOnly end_date)
    {
        var time = DateTime.Now;
        var (productionDate, currentShift) = GetProductionDate(time);
        bool isToday = start_date == end_date && start_date == productionDate;

        string sql;

        if (isToday)
        {
            var logCte = await BuildMachineLogUnionAsync("production_date = @today AND shift = @currentShift");
            sql = $@"
            WITH CombinedLogs AS (
                {logCte}
            ),
            ShiftDayType AS (
                SELECT COALESCE(
                    (SELECT TOP 1 day_type FROM calendar
                     WHERE production_date = @today AND shift = @currentShift),
                    'NORMAL'
                ) AS day_type
            ),
            ShiftHasRun AS (
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM CombinedLogs WHERE category = 'PRODUCTION RUNNING'
                ) THEN 1 ELSE 0 END AS has_run
            ),
            MachineAgg AS (
                SELECT
                    cl.id_machine, cl.machine_name, cl.id_type, cl.mould,
                    SUM(cl.shot) AS shot,
                    SUM(CASE
                        WHEN (SELECT day_type FROM ShiftDayType) = 'OFFDAY' THEN 0
                        WHEN (SELECT day_type FROM ShiftDayType) = 'OVERTIME'
                             AND (SELECT has_run FROM ShiftHasRun) = 0 THEN 0
                        WHEN cl.category = 'PRODUCTION RUNNING' AND cl.status_start = 1
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS run_time,
                    SUM(CASE
                        WHEN (SELECT day_type FROM ShiftDayType) = 'OFFDAY' THEN 0
                        WHEN (SELECT day_type FROM ShiftDayType) = 'OVERTIME'
                             AND (SELECT has_run FROM ShiftHasRun) = 0 THEN 0
                        WHEN cl.category NOT IN ('PRODUCTION RUNNING', 'NO SCHEDULE', 'SCHEDULED MAINTENANCE') AND cl.category IS NOT NULL
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS unplanned_dt,
                    SUM(CASE
                        WHEN (SELECT day_type FROM ShiftDayType) = 'OFFDAY' THEN 0
                        WHEN (SELECT day_type FROM ShiftDayType) = 'OVERTIME'
                             AND (SELECT has_run FROM ShiftHasRun) = 0 THEN 0
                        WHEN cl.category IN ('NO SCHEDULE', 'SCHEDULED MAINTENANCE') AND cl.category IS NOT NULL
                             THEN DATEDIFF(SECOND, cl.start, COALESCE(cl.finish, GETDATE())) / 3600.0
                        ELSE 0
                    END) AS planned_dt,
                    SUM(CAST(cl.shot AS FLOAT) * COALESCE(NULLIF(cl.act_ct, 0), 0)) / 3600.0 AS total_actual_time
                FROM CombinedLogs cl
                GROUP BY cl.id_machine, cl.machine_name, cl.id_type, cl.mould
            ),
            WithReject AS (
                SELECT
                    m.*,
                    COALESCE(s.type,   '')  AS type,
                    COALESCE(s.sap_ct, 0)   AS sap_ct,
                    COALESCE(s.material_used, 0) AS material_used,
                    CAST(m.shot AS FLOAT) * COALESCE(NULLIF(s.sap_ct, 0), 0) / 3600.0 AS total_sap_time,
                    COALESCE(r.reject_black_dot + r.reject_burst + r.reject_lumpy
                           + r.reject_others + r.reject_panelling, 0) AS reject_weight
                FROM MachineAgg m
                LEFT JOIN sap s
                    ON  s.id_type = m.id_type
                    AND s.mould   = m.mould
                LEFT JOIN reject r
                    ON  r.id_machine      = m.id_machine
                    AND r.id_type         = m.id_type
                    AND r.mould           = m.mould
                    AND r.production_date = @today
                    AND r.shift           = @currentShift
            )
            SELECT
                id_machine,
                machine_name,
                COALESCE(id_type, 0)  AS id_type,
                COALESCE(mould,   '')  AS mould,
                type,
                shot,
                run_time,
                unplanned_dt,
                planned_dt,
                material_used,
                reject_weight,
                sap_ct,
                CASE WHEN shot = 0 THEN 0
                     ELSE total_actual_time / shot * 3600.0
                END AS act_ct,
                total_sap_time,
                total_actual_time
            FROM WithReject
            ORDER BY id_machine, id_type, mould;";
        }
        else
        {
            var logCte = await BuildMachineLogUnionAsync("production_date BETWEEN @start_date AND @end_date");
            sql = $@"
            WITH CombinedLogs AS (
                {logCte}
            ),
            AllDates AS (
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
                    sd.production_date,
                    sd.shift,
                    COALESCE(
                        (SELECT TOP 1 cx.day_type
                         FROM calendar cx
                         WHERE cx.production_date = sd.production_date
                           AND cx.shift           = sd.shift),
                        'NORMAL'
                    ) AS day_type
                FROM ShiftDates sd
            ),
            OvertimeRunCheck AS (
                SELECT cl.id_machine, cl.production_date, cl.shift,
                    CASE WHEN SUM(CASE WHEN cl.category = 'PRODUCTION RUNNING' THEN 1 ELSE 0 END) > 0
                         THEN 1 ELSE 0
                    END AS has_run
                FROM CombinedLogs cl
                INNER JOIN EffectiveCalendar ec
                    ON  ec.production_date = cl.production_date
                    AND ec.shift           = cl.shift
                    AND ec.day_type        = 'OVERTIME'
                GROUP BY cl.id_machine, cl.production_date, cl.shift
            ),
            ReportAgg AS (
                SELECT
                    r.id_machine, r.machine_name, r.id_type, r.mould,
                    r.production_date, r.shift,
                    COALESCE(SUM(r.shot), 0) AS shot,
                    COALESCE(SUM(CASE
                        WHEN ec.day_type = 'OFFDAY' THEN 0
                        WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                        ELSE r.production_running
                    END), 0) AS run_time,
                    COALESCE(SUM(CASE
                        WHEN ec.day_type = 'OFFDAY' THEN 0
                        WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                        ELSE COALESCE(r.change_full_set, 0) + COALESCE(r.change_half_set, 0)
                           + COALESCE(r.change_parts,    0) + COALESCE(r.maintenance_dt,  0)
                           + COALESCE(r.technician_dt,   0) + COALESCE(r.production_dt,   0)
                           + COALESCE(r.buyoff_dt,       0)
                    END), 0) AS unplanned_dt,
                    COALESCE(SUM(CASE
                        WHEN ec.day_type = 'OFFDAY' THEN 0
                        WHEN ec.day_type = 'OVERTIME' AND COALESCE(orc.has_run, 0) = 0 THEN 0
                        ELSE COALESCE(r.planned_dt, 0)
                    END), 0) AS planned_dt,
                    COALESCE(SUM(r.material_used),  0) AS material_used,
                    COALESCE(SUM(r.reject_prod + r.reject_startup), 0) AS reject_weight,
                    SUM(COALESCE(r.shot, 0) * COALESCE(r.sap_ct, 0)) AS total_sap_time,
                    SUM(COALESCE(r.shot, 0) * COALESCE(r.act_ct, 0)) AS total_actual_time,
                    AVG(COALESCE(r.act_ct, 0)) AS act_ct,
                    AVG(COALESCE(r.sap_ct, 0)) AS sap_ct
                FROM report r
                INNER JOIN EffectiveCalendar ec
                    ON  ec.production_date = r.production_date
                    AND ec.shift           = r.shift
                    AND ec.day_type        <> 'OFFDAY'
                LEFT JOIN OvertimeRunCheck orc
                    ON  orc.id_machine      = r.id_machine
                    AND orc.production_date = r.production_date
                    AND orc.shift           = r.shift
                WHERE r.production_date BETWEEN @start_date AND @end_date
                GROUP BY r.id_machine, r.machine_name, r.id_type, r.mould,
                         r.production_date, r.shift
            ),
            ProductSummary AS (
                SELECT
                    ra.id_machine,
                    ra.machine_name,
                    ra.id_type,
                    ra.mould,
                    COALESCE(s.type,   '')  AS type,
                    SUM(ra.shot)            AS shot,
                    SUM(ra.run_time)        AS run_time,
                    SUM(ra.unplanned_dt)    AS unplanned_dt,
                    SUM(ra.planned_dt)      AS planned_dt,
                    SUM(ra.material_used)   AS material_used,
                    SUM(ra.reject_weight)   AS reject_weight,
                    AVG(ra.sap_ct)          AS sap_ct,
                    AVG(ra.act_ct)          AS act_ct,
                    SUM(ra.total_sap_time) / 3600.0 AS total_sap_time,
                    SUM(ra.total_actual_time) / 3600.0 AS total_actual_time
                FROM ReportAgg ra
                LEFT JOIN sap s
                    ON  s.id_type = ra.id_type
                    AND s.mould   = ra.mould
                GROUP BY ra.id_machine, ra.machine_name, ra.id_type, ra.mould, s.type, ra.sap_ct
            )
            SELECT
                id_machine,
                machine_name,
                COALESCE(id_type, 0)  AS id_type,
                COALESCE(mould,   '')  AS mould,
                type,
                shot,
                run_time,
                unplanned_dt,
                planned_dt,
                material_used,
                reject_weight,
                sap_ct,
                act_ct,
                total_sap_time,
                total_actual_time
            FROM ProductSummary
            ORDER BY id_machine, id_type, mould;";
        }

        var result = new List<OEERawRow>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@today", productionDate);
        cmd.Parameters.AddWithValue("@start_date", start_date);
        cmd.Parameters.AddWithValue("@end_date", end_date);
        if (isToday)
            cmd.Parameters.AddWithValue("@currentShift", currentShift);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new OEERawRow(
                id_machine: Convert.ToInt32(reader["id_machine"]),
                machine_name: Convert.ToString(reader["machine_name"]) ?? string.Empty,
                id_type: Convert.ToInt32(reader["id_type"]),
                mould: Convert.ToInt32(reader["mould"]),
                type: Convert.ToString(reader["type"]) ?? string.Empty,
                shot: Convert.ToDouble(reader["shot"]),
                run_time: Convert.ToDouble(reader["run_time"]),
                unplanned_dt: Convert.ToDouble(reader["unplanned_dt"]),
                planned_dt: Convert.ToDouble(reader["planned_dt"]),
                material_used: Convert.ToDouble(reader["material_used"]),
                reject_weight: Convert.ToDouble(reader["reject_weight"]),
                sap_ct: Convert.ToDouble(reader["sap_ct"]),
                act_ct: Convert.ToDouble(reader["act_ct"]),
                total_sap_time: Convert.ToDouble(reader["total_sap_time"]),
                total_actual_time: Convert.ToDouble(reader["total_actual_time"])
            ));
        }
        return result;
    }

    // ── Generic executor ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<object>> ExecuteDetailQueryAsync(string sql, int id_machine, DateOnly start, DateOnly end, Func<SqlDataReader, object> mapper)
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