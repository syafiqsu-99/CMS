using CMS.server.Services;
using CMS.Server.Services.Base;
using Microsoft.Data.SqlClient;
using static CMS.server.Services.MachineLogService;

namespace CMS.Server.Services;

public class MachinesService(string connectionString, PlcService plcService)
    : BaseDataService(connectionString)
{
    private readonly PlcService _plc = plcService;

    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<object>> LoadMachineMasterAsync()
    {
        var (productionDate, shift) = GetProductionDate(DateTime.Now);

        // Dynamic UNION ALL built from machine_master rows — handles any machine count.
        const string sql = @"
            WITH latest_logs AS (
                SELECT ll.*
                FROM machine_master mm
                CROSS APPLY (
                    SELECT TOP 1 ml.machine_name, ml.start, ml.finish,
                                 ml.category, ml.problem, ml.mould_category
                    FROM (
                        SELECT machine_name, start, finish, category, problem, mould_category,
                               ROW_NUMBER() OVER (PARTITION BY id_machine ORDER BY start DESC) AS rn,
                               @id_machine_col AS id_machine
                        FROM machine_log_1 WHERE 1=0  -- placeholder; real query below
                    ) x WHERE rn = 1
                ) ll
            )
            SELECT
                mm.id_machine,
                mm.machine_name,
                COALESCE(mm.packer,'')          AS packer,
                COALESCE(mm.material,'')        AS material,
                COALESCE(mm.id_type, 123456)    AS id_type,
                COALESCE(mm.mould, 0)           AS mould,
                COALESCE(mm.type,'')            AS type,
                CAST(COALESCE(mm.status_start,0) AS BIT) AS status_start,
                CAST(COALESCE(mm.status_off,1)   AS BIT) AS status_off,
                COALESCE(mm.qty_perct,0)        AS qty_perct,
                COALESCE(mm.act_ct,0)           AS act_ct,
                COALESCE(mm.sap_ct,0)           AS sap_ct,
                COALESCE(mm.shot,0)             AS shot,
                COALESCE(mm.material,'')        AS material,
                COALESCE(mm.part_weight,0)      AS part_weight,
                COALESCE(mm.visual_qc,0)        AS visual_qc,
                COALESCE(mm.measure_qc,0)       AS measure_qc,
                COALESCE(mm.shift_output,0)     AS output,
                /* planned_output: seconds elapsed in shift / sap_ct * qty_perct */
                CASE
                    WHEN COALESCE(mm.id_type,123456) = 123456 AND COALESCE(mm.mould,0) = 0 THEN 0
                    WHEN COALESCE(mm.sap_ct,0) = 0 THEN 0
                    ELSE CAST(
                        (DATEDIFF(SECOND,
                            CASE
                                WHEN CAST(GETDATE() AS TIME) >= '06:00:00'
                                 AND CAST(GETDATE() AS TIME) <  '18:00:00'
                                THEN CAST(CAST(GETDATE() AS DATE) AS DATETIME) + CAST('06:00:00' AS DATETIME)
                                WHEN CAST(GETDATE() AS TIME) >= '18:00:00'
                                THEN CAST(CAST(GETDATE() AS DATE) AS DATETIME) + CAST('18:00:00' AS DATETIME)
                                ELSE CAST(CAST(DATEADD(DAY,-1,GETDATE()) AS DATE) AS DATETIME) + CAST('18:00:00' AS DATETIME)
                            END,
                            GETDATE())
                        / NULLIF(mm.sap_ct,0)) * COALESCE(mm.qty_perct,0)
                    AS INT)
                END AS planned_output,
                COALESCE(r.total_weight,0) AS reject_weight,
                CASE
                    WHEN COALESCE(mm.part_weight,0) = 0 THEN 0
                    ELSE CAST(COALESCE(r.total_weight,0) / NULLIF(mm.part_weight,0) AS INT)
                END AS reject_pcs,
                ll.start,
                COALESCE(ll.finish, CAST(GETDATE() AS DATETIME)) AS finish,
                CASE
                    WHEN NULLIF(ll.category,'') IS NULL
                         AND mm.status_start = 1
                         AND mm.status_off   = 1
                    THEN 'PRODUCTION RUNNING'
                    ELSE NULLIF(ll.category,'')
                END AS category,
                COALESCE(ll.problem,'')        AS problem,
                COALESCE(ll.mould_category,0)  AS mould_category
            FROM machine_master mm
            CROSS APPLY (
                SELECT TOP 1 ml.machine_name, ml.start, ml.finish,
                             ml.category, ml.problem, ml.mould_category
                FROM (
                    SELECT * FROM machine_log_1  WHERE id_machine_col = mm.id_machine
                    /* additional unions injected below */
                ) _logs
                ORDER BY start DESC
            ) ll
            LEFT JOIN reject r
                ON r.id_machine     = mm.id_machine
               AND r.id_type        = mm.id_type
               AND r.mould          = mm.mould
               AND r.production_date = @production_date
               AND r.shift           = @shift";

        // Build the dynamic UNION ALL from machine_master rows instead of hardcoding.
        var logUnion = await BuildLogUnionSqlAsync();
        var finalSql = BuildMasterSql(logUnion);

        var result = new List<object>();
        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(finalSql, conn);
        cmd.Parameters.AddWithValue("@shift", shift);
        cmd.Parameters.AddWithValue("@production_date", productionDate);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var category = reader["category"].ToString();
            result.Add(new
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                machine_name = reader["machine_name"].ToString(),
                packer = reader["packer"].ToString(),
                material = reader["material"].ToString(),
                id_type = Convert.ToInt32(reader["id_type"]),
                mould = Convert.ToInt32(reader["mould"]),
                type = reader["type"].ToString(),
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
                category,
                problem = reader["problem"].ToString(),
                visual_qc = Convert.ToBoolean(reader["visual_qc"]),
                measure_qc = Convert.ToBoolean(reader["measure_qc"]),
                mould_category = Convert.ToInt32(reader["mould_category"]),
                color = BaseDataService.GetColor(category),
            });
        }
        return result;
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Reads distinct id_machine values from machine_master so the UNION ALL
    /// is always aligned with the actual machine count — no hardcoding needed.
    /// </summary>
    private async Task<string> BuildLogUnionSqlAsync()
    {
        const string idsSql = "SELECT id_machine FROM machine_master ORDER BY id_machine";
        var ids = new List<int>();

        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(idsSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetInt32(0));

        if (ids.Count == 0) return "SELECT NULL AS id_machine, NULL AS machine_name, NULL AS start, NULL AS finish, NULL AS category, NULL AS problem, NULL AS mould_category WHERE 1=0";

        var parts = ids.Select(id =>
            $"SELECT TOP 1 {id} AS id_machine, machine_name, start, finish, category, problem, mould_category FROM machine_log_{id} ORDER BY start DESC");

        return string.Join("\n            UNION ALL\n            ", parts);
    }

    private static string BuildMasterSql(string logUnion) => $@"
        WITH latest_logs AS (
            {logUnion}
        )
        SELECT
            mm.id_machine,
            mm.machine_name,
            COALESCE(mm.packer,'')           AS packer,
            COALESCE(mm.material,'')         AS material,
            COALESCE(mm.id_type, 123456)     AS id_type,
            COALESCE(mm.mould, 0)            AS mould,
            COALESCE(mm.type,'')             AS type,
            CAST(COALESCE(mm.status_start,0) AS BIT) AS status_start,
            CAST(COALESCE(mm.status_off,1)   AS BIT) AS status_off,
            COALESCE(mm.qty_perct,0)         AS qty_perct,
            COALESCE(mm.act_ct,0)            AS act_ct,
            COALESCE(mm.sap_ct,0)            AS sap_ct,
            COALESCE(mm.shot,0)              AS shot,
            COALESCE(mm.part_weight,0)       AS part_weight,
            COALESCE(mm.visual_qc,0)         AS visual_qc,
            COALESCE(mm.measure_qc,0)        AS measure_qc,
            COALESCE(mm.shift_output,0)      AS output,
            CASE
                WHEN COALESCE(mm.id_type,123456) = 123456 AND COALESCE(mm.mould,0) = 0 THEN 0
                WHEN COALESCE(mm.sap_ct,0) = 0 THEN 0
                ELSE CAST(
                    (DATEDIFF(SECOND,
                        CASE
                            WHEN CAST(GETDATE() AS TIME) >= '06:00:00'
                             AND CAST(GETDATE() AS TIME) <  '18:00:00'
                            THEN CAST(CAST(GETDATE() AS DATE) AS DATETIME) + CAST('06:00:00' AS DATETIME)
                            WHEN CAST(GETDATE() AS TIME) >= '18:00:00'
                            THEN CAST(CAST(GETDATE() AS DATE) AS DATETIME) + CAST('18:00:00' AS DATETIME)
                            ELSE CAST(CAST(DATEADD(DAY,-1,GETDATE()) AS DATE) AS DATETIME) + CAST('18:00:00' AS DATETIME)
                        END,
                        GETDATE())
                    / NULLIF(mm.sap_ct,0)) * COALESCE(mm.qty_perct,0)
                AS INT)
            END AS planned_output,
            COALESCE(r.total_weight,0) AS reject_weight,
            CASE
                WHEN COALESCE(mm.part_weight,0) = 0 THEN 0
                ELSE CAST(COALESCE(r.total_weight,0) / NULLIF(mm.part_weight,0) AS INT)
            END AS reject_pcs,
            ll.start,
            COALESCE(ll.finish, CAST(GETDATE() AS DATETIME)) AS finish,
            CASE
                WHEN NULLIF(ll.category,'') IS NULL
                     AND mm.status_start = 1
                     AND mm.status_off   = 1
                THEN 'PRODUCTION RUNNING'
                ELSE NULLIF(ll.category,'')
            END AS category,
            COALESCE(ll.problem,'')       AS problem,
            COALESCE(ll.mould_category,0) AS mould_category
        FROM machine_master mm
        LEFT JOIN latest_logs ll ON ll.id_machine = mm.id_machine
        LEFT JOIN reject r
            ON r.id_machine      = mm.id_machine
           AND r.id_type         = mm.id_type
           AND r.mould           = mm.mould
           AND r.production_date = @production_date
           AND r.shift           = @shift";
}