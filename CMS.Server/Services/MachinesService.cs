using Microsoft.Data.SqlClient;
using static CMS.Server.Services.MachineLogService;

namespace CMS.Server.Services;

public class MachinesService(PlcService plcService, string connectionString) : BaseService(connectionString, plcService)
{

    public async Task<object> LoadMachineMaster()
    {
        var time = DateTime.Now;
        var (productionDate, shift) = GetProductionDate(time);

        var logUnion = await BuildMachineLogUnionAsync("production_date = @production_date and shift = @shift");

        var sql = $@"
            WITH all_logs AS (
                {logUnion}
            ),
            latest_logs AS (
                -- This assigns a row number to each log per machine, sorted by newest first
                SELECT *, ROW_NUMBER() OVER(PARTITION BY id_machine ORDER BY start DESC) as rn
                FROM all_logs
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
                        ) / NULLIF(mm.sap_ct, 0)) * COALESCE(mm.qty_perct, 0)
                    AS INT)
                END AS planned_output,
                COALESCE(r.total_weight, 0) AS reject_weight,
                CASE 
                    WHEN COALESCE(mm.part_weight, 0) = 0 THEN 0
                    ELSE CAST(COALESCE(r.total_weight, 0) / NULLIF(mm.part_weight, 0) AS INT)
                END AS reject_pcs,
                
                -- Used COALESCE to fallback to current time if the machine has NO logs yet
                COALESCE(ll.start, CAST(GETDATE() AS DATETIME)) AS start, 
                COALESCE(ll.finish, CAST(GETDATE() AS DATETIME)) AS finish,
                CASE 
                    WHEN (NULLIF(ll.category, '') IS NULL) 
                         AND mm.status_start = 1 
                         AND mm.status_off = 1 
                    THEN 'PRODUCTION RUNNING'
                    ELSE COALESCE(NULLIF(ll.category, ''), 'N/A')
                END AS category,
                COALESCE(ll.problem, '') AS problem,
                COALESCE(ll.mould_category, 0) AS mould_category
            FROM machine_master mm
            -- LEFT JOIN ensures the machine still shows up even if it has 0 logs
            -- rn = 1 ensures we ONLY get the Top 1 latest log
            LEFT JOIN latest_logs ll ON mm.id_machine = ll.id_machine AND ll.rn = 1
            LEFT JOIN reject r 
                ON r.id_machine = mm.id_machine 
                AND r.id_type = mm.id_type 
                AND r.mould = mm.mould
                AND r.production_date = @production_date
                AND r.shift = @shift";

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
                visual_qc = Convert.ToInt32(reader["visual_qc"]),
                measure_qc = Convert.ToInt32(reader["measure_qc"]),
                mould_category = Convert.ToInt32(reader["mould_category"]),
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

        using var conn = await CreateConnectionAsync();
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
}