using Microsoft.Data.SqlClient;

namespace CMS.Server.Services
{
    public class SchemaInitializerService
    {
        private readonly string _connectionString;
        private static bool _schemaChecked = false;
        private static readonly object _lock = new();

        public SchemaInitializerService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private async ValueTask<SqlConnection> CreateConnection()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public async Task<object> EnsureDatabaseSchemaAsync()
        {
            if (_schemaChecked)
                return new { message = "Schema already checked this runtime." };

            lock (_lock)
            {
                if (_schemaChecked)
                    return new { message = "Schema already checked this runtime." };
                _schemaChecked = true;
            }

            var createdTables = new List<string>();
            var tableNames = new List<string>();

            for (int i = 1; i <= 26; i++)
                tableNames.Add($"machine_log_{i}");

            tableNames.AddRange([
                "reject", "report", "sap", "staff_list", "utilities", "attendance", "machine_master", "calendar"
            ]);

            using var conn = await CreateConnection();

            foreach (var table in tableNames)
            {
                bool exists = await TableExistsAsync(conn, table);
                if (exists)
                    continue;

                var ddl = GetCreateTableDDL(table);
                if (string.IsNullOrWhiteSpace(ddl))
                    continue;

                using var cmd = new SqlCommand(ddl, conn);
                await cmd.ExecuteNonQueryAsync();
                createdTables.Add(table);
            }

            return new { createdTables };
        }

        private static async Task<bool> TableExistsAsync(SqlConnection conn, string tableName)
        {
            string sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @table";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            int count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return count > 0;
        }

        private static string GetCreateTableDDL(string table)
        {
            if (table.StartsWith("machine_log_"))
            {
                return $@"
                CREATE TABLE [{table}](
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    machine_name NVARCHAR(100),
                    id_type INT,
                    mould INT,
                    start DATETIME NOT NULL,
                    finish DATETIME NULL,
                    shot INT DEFAULT 0,
                    act_ct FLOAT DEFAULT 0,
                    category NVARCHAR(100) NULL,
                    problem NVARCHAR(255) NULL,
                    mould_category INT,
                    shift INT,
                    production_date DATE
                )";
            }

            if (table == "machine_master")
                return @"
                CREATE TABLE machine_master(
                        id_machine INT IDENTITY(1,1) PRIMARY KEY,
                        shift INT,
                        machine_name NVARCHAR(100),
                        packer NVARCHAR(255),
                        material NVARCHAR(100),
                        id_type INT,
                        mould INT,
                        type NVARCHAR(255),
                        jo_no NVARCHAR(255),
                        qty_perct INT,
                        gross_weight FLOAT,
                        part_weight FLOAT,
                        shot INT,
                        qty_order INT,
                        wip_opening INT,
                        wip_closing INT,
                        shift_output AS ([shot]*[qty_perct]) PERSISTED,
                        finish_good INT,
                        inward AS (COALESCE((NULLIF([part_weight],(0))*NULLIF([finish_good],(0)))/(1000),(0))) PERSISTED,
                        qty_accum INT,
                        qty_balance AS ([qty_order]-[qty_accum]) PERSISTED,
                        material_used AS (COALESCE((NULLIF([part_weight],(0))*NULLIF([shot]*[qty_perct],(0)))/(1000),(0))) PERSISTED,
                        part_scrap FLOAT,
                        runner AS (COALESCE((NULLIF([gross_weight]-[part_weight],(0))*NULLIF([shot]*[qty_perct],(0)))/(1000),(0))) PERSISTED,
                        act_ct FLOAT,
                        sap_ct FLOAT,
                        status_start BIT,
                        status_off BIT,
                        visual_qc BIT,
                        measure_qc BIT
                    )";

            if (table == "reject")
                return @"
                CREATE TABLE reject(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    id_machine INT,
                    machine_name NVARCHAR(100),
                    id_type INT,
                    mould INT,
                    total_weight FLOAT,
                    reject_panelling FLOAT,
                    reject_lumpy FLOAT,
                    reject_black_dot FLOAT,
                    reject_burst FLOAT,
                    reject_startup FLOAT,
                    reject_preform FLOAT,
                    reject_purging FLOAT,
                    reject_others FLOAT,
                    shift INT,
                    production_date DATE
                )";

            if (table == "report")
                return @"
                CREATE TABLE report(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    id_machine INT,
                    time DATETIME,
                    production_date DATE,
                    shift INT,
                    machine_name NVARCHAR(100),
                    packer NVARCHAR(255),
                    material NVARCHAR(100),
                    id_type INT,
                    mould INT,
                    type NVARCHAR(255),
                    jo_no NVARCHAR(255),
                    qty_perct INT,
                    gross_weight FLOAT,
                    part_weight FLOAT,
                    shot INT,
                    qty_order INT,
                    wip_opening INT,
                    wip_closing INT,
                    shift_output AS ([shot]*[qty_perct]) PERSISTED,
                    finish_good INT,
                    inward AS (COALESCE((NULLIF([part_weight],(0))*NULLIF([finish_good],(0)))/(1000),(0))) PERSISTED,
                    qty_accum INT,
                    qty_balance AS ([qty_order]-[qty_accum]) PERSISTED,
                    material_used AS (COALESCE((NULLIF([part_weight],(0))*NULLIF([shot]*[qty_perct],(0)))/(1000),(0))) PERSISTED,
                    runner AS (COALESCE((NULLIF([gross_weight]-[part_weight],(0))*NULLIF([shot]*[qty_perct],(0)))/(1000),(0))) PERSISTED,
                    reject_startup FLOAT,
                    reject_startup_per AS (([reject_startup]/NULLIF(COALESCE((NULLIF([part_weight],(0))*NULLIF([shot]*[qty_perct],(0)))/(1000),(0)),(0)))*(100.0)) PERSISTED,
                    reject_prod FLOAT,
                    reject_prod_per AS (([reject_prod]/NULLIF(COALESCE((NULLIF([part_weight],(0))*NULLIF([shot]*[qty_perct],(0)))/(1000),(0)),(0)))*(100.0)) PERSISTED,
                    act_ct FLOAT,
                    production_running FLOAT,
                    sap_ct FLOAT,
                    change_full_set FLOAT,
                    change_half_set FLOAT,
                    change_parts FLOAT,
                    maintenance_dt FLOAT,
                    technician_dt FLOAT,
                    production_dt FLOAT,
                    unallocated FLOAT,
                    remark NVARCHAR(255),
                    part_scrap FLOAT,
                    reject_labelling FLOAT,
                    reject_preform FLOAT,
                    reject_purging FLOAT,
                    reject_total_pcs INT
                )";

            if (table == "sap")
                return @"
                CREATE TABLE sap(
                    id INT IDENTITY(1,1),
                    id_type INT,
                    mould INT,
                    type NVARCHAR(255),
                    qty_perct INT,
                    process NVARCHAR(100),
                    material NVARCHAR(100),
                    part_weight FLOAT,
                    tolerance FLOAT,
                    gross_weight FLOAT,
                    sap_ct FLOAT
                    PRIMARY KEY (id_type, mould)
                )";

            if (table == "staff_list")
                return @"
                CREATE TABLE staff_list(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    staff_id INT,
                    staff_name NVARCHAR(255),
                    staff_role NVARCHAR(50),
                    status NVARCHAR(20),
                    machine_name NVARCHAR(100),
                    start_date DATE,
                    end_date DATE,
                    work_shift INT
                )";

            if (table == "utilities")
                return @"
                CREATE TABLE utilities(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    id_machine INT,
                    machine_name NVARCHAR(100),
                    utility_name NVARCHAR(50),
                    start DATETIME,
                    finish DATETIME NULL,
                    category NVARCHAR(50),
                    shift INT,
                    production_date DATE
                )";

            if (table == "attendance")
                return @"
                CREATE TABLE attendance(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    staff_id INT,
                    staff_name NVARCHAR(255),
                    staff_role NVARCHAR(50),
                    status NVARCHAR(50),
                    machine_name NVARCHAR(100),
                    production_date DATE,
                    shift INT
                )";

            if (table == "calendar")
                return @"
                CREATE TABLE calendar (
                    production_date DATE NOT NULL,
                    shift INT NOT NULL,
                    day_type NVARCHAR(20) NOT NULL,
                    planned_hours FLOAT NOT NULL DEFAULT 12,
                    start DAETIME NOT NULL,
                    finish DAETIME NULL,
                    PRIMARY KEY (production_date, shift)
                );";

            return "";
        }
    }
}
