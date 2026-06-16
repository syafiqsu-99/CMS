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

            using var conn = await CreateConnection();

            var staticTables = new[]
            {
                "machine_master", "reject", "report", "sap", "staff_list",
                "utilities", "attendance", "calendar", "plc_passwords", "db_log"
            };

            foreach (var table in staticTables)
            {
                bool exists = await TableExistsAsync(conn, table);

                if (!exists)
                {
                    var ddl = GetCreateTableDDL(table);
                    if (string.IsNullOrWhiteSpace(ddl)) continue;

                    using var cmd = new SqlCommand(ddl, conn);
                    await cmd.ExecuteNonQueryAsync();
                    createdTables.Add(table);

                    if (table == "plc_passwords")
                        await SeedPlcPasswordsAsync(conn);
                }
                else
                {
                    await MigrateTableAsync(conn, table, GetCreateTableDDL(table));
                }
            }

            int minId = 0, maxId = 0;

            using (var rangeCmd = new SqlCommand(
                "SELECT MIN(id_machine), MAX(id_machine) FROM machine_master", conn))
            using (var reader = await rangeCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync() && !reader.IsDBNull(0) && !reader.IsDBNull(1))
                {
                    minId = reader.GetInt32(0);
                    maxId = reader.GetInt32(1);
                }
            }

            if (maxId >= minId)
            {
                string machineLogDdl = GetCreateTableDDL("machine_log_0");

                for (int i = minId; i <= maxId; i++)
                {
                    var tableName = $"machine_log_{i}";
                    bool exists = await TableExistsAsync(conn, tableName);

                    if (!exists)
                    {
                        var ddl = GetCreateTableDDL(tableName);
                        if (string.IsNullOrWhiteSpace(ddl)) continue;

                        using var cmd = new SqlCommand(ddl, conn);
                        await cmd.ExecuteNonQueryAsync();
                        createdTables.Add(tableName);
                    }
                    else
                    {
                        await MigrateTableAsync(conn, tableName, machineLogDdl);
                    }
                }
            }

            return new { createdTables };
        }

        private static async Task MigrateTableAsync(SqlConnection conn, string tableName, string ddl)
        {
            if (string.IsNullOrWhiteSpace(ddl)) return;

            var expectedColumns = ParseColumnDefinitions(ddl);

            foreach (var kvp in expectedColumns)
            {
                string columnName = kvp.Key;
                string cleanDef = kvp.Value.CleanDef;
                string fullDef = kvp.Value.FullDef;

                using var checkCmd = new SqlCommand(@"
                    SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @table AND COLUMN_NAME = @column", conn);

                checkCmd.Parameters.AddWithValue("@table", tableName);
                checkCmd.Parameters.AddWithValue("@column", columnName);

                using var reader = await checkCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    await reader.CloseAsync();
                    using var addCmd = new SqlCommand(
                        $"ALTER TABLE [{tableName}] ADD [{columnName}] {fullDef}", conn);
                    await addCmd.ExecuteNonQueryAsync();
                    continue;
                }

                string dbType = reader.GetString(0).ToUpper();
                int? dbMaxLength = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                string dbNullable = reader.GetString(2);
                await reader.CloseAsync();

                if (!ColumnTypeMatches(cleanDef, dbType, dbMaxLength, dbNullable))
                {
                    try
                    {
                        using var alterCmd = new SqlCommand(
                            $"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {cleanDef}", conn);
                        await alterCmd.ExecuteNonQueryAsync();
                    }
                    catch (SqlException ex)
                    {
                        Console.WriteLine($"[SCHEMA WARNING] Failed to alter '{tableName}.{columnName}': {ex.Message}");
                    }
                }
            }
        }

        private static Dictionary<string, (string CleanDef, string FullDef)> ParseColumnDefinitions(string ddl)
        {
            var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(ddl)) return result;

            int start = ddl.IndexOf('(');
            int end = ddl.LastIndexOf(')');
            if (start < 0 || end < 0) return result;

            string body = ddl.Substring(start + 1, end - start - 1);

            var pkColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in body.Split('\n'))
            {
                string line = rawLine.Trim().TrimEnd(',');
                if (line.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                {
                    int pStart = line.IndexOf('(');
                    int pEnd = line.IndexOf(')', pStart);
                    if (pStart >= 0 && pEnd > pStart)
                    {
                        var cols = line.Substring(pStart + 1, pEnd - pStart - 1).Split(',');
                        foreach (var c in cols)
                        {
                            pkColumns.Add(c.Trim().Trim('[', ']'));
                        }
                    }
                }
            }

            foreach (var rawLine in body.Split('\n'))
            {
                string line = rawLine.Trim().TrimEnd(',');
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.Contains(" AS ", StringComparison.OrdinalIgnoreCase)) continue;

                string colName, remainder;

                if (line.StartsWith("["))
                {
                    int close = line.IndexOf(']');
                    if (close < 0) continue;
                    colName = line.Substring(1, close - 1);
                    remainder = line.Substring(close + 1).Trim();
                }
                else
                {
                    int space = line.IndexOf(' ');
                    if (space < 0) continue;
                    colName = line.Substring(0, space);
                    remainder = line.Substring(space + 1).Trim();
                }

                string typeDef = remainder;
                string fullDef = remainder;

                bool isNotNull = typeDef.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 typeDef.IndexOf("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 typeDef.IndexOf("IDENTITY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 pkColumns.Contains(colName);

                int pkIdx = typeDef.IndexOf("PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
                if (pkIdx >= 0) typeDef = typeDef.Substring(0, pkIdx).Trim();

                int identityIdx = typeDef.IndexOf("IDENTITY", StringComparison.OrdinalIgnoreCase);
                if (identityIdx >= 0) typeDef = typeDef.Substring(0, identityIdx).Trim();

                int defaultIdx = typeDef.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase);
                if (defaultIdx >= 0) typeDef = typeDef.Substring(0, defaultIdx).Trim();

                int notNullIdx = typeDef.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase);
                if (notNullIdx >= 0) typeDef = typeDef.Substring(0, notNullIdx).Trim();

                int nullIdx = typeDef.IndexOf("NULL", StringComparison.OrdinalIgnoreCase);
                if (nullIdx >= 0) typeDef = typeDef.Substring(0, nullIdx).Trim();

                typeDef = typeDef.Trim();

                if (isNotNull)
                    typeDef += " NOT NULL";
                else
                    typeDef += " NULL";

                if (!string.IsNullOrWhiteSpace(colName) && !string.IsNullOrWhiteSpace(typeDef))
                    result[colName] = (typeDef, fullDef);
            }

            return result;
        }
        private static bool ColumnTypeMatches(string columnDef, string dbType, int? dbMaxLength, string dbNullable)
        {
            string def = columnDef.ToUpper().Trim();

            bool expectNullable = !def.Contains("NOT NULL");
            if (expectNullable != (dbNullable == "YES")) return false;

            if (def.StartsWith("NVARCHAR"))
            {
                if (dbType != "NVARCHAR") return false;
                if (def.Contains("MAX")) return dbMaxLength == -1;
                var match = System.Text.RegularExpressions.Regex.Match(def, @"NVARCHAR\((\d+)\)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int defLen))
                    return dbMaxLength == defLen;
            }
            else if (def.StartsWith("DATETIME2")) return dbType == "DATETIME2";
            else if (def.StartsWith("DATETIME")) return dbType == "DATETIME";
            else if (def.StartsWith("DATE")) return dbType == "DATE";
            else if (def.StartsWith("FLOAT")) return dbType == "FLOAT";
            else if (def.StartsWith("BIT")) return dbType == "BIT";
            else if (def.StartsWith("INT")) return dbType == "INT";

            return true;
        }

        private static async Task SeedPlcPasswordsAsync(SqlConnection conn)
        {
            using var countCmd = new SqlCommand("SELECT COUNT(*) FROM plc_passwords", conn);
            int count = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
            if (count > 0) return;

            const string sql = @"
                INSERT INTO plc_passwords (department, password)
                VALUES ('production', 0), ('technician', 0), ('maintenance', 0), ('qc', 0)";

            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<bool> TableExistsAsync(SqlConnection conn, string tableName)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @table", conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
        }

        private static string GetCreateTableDDL(string table)
        {
            if (table.StartsWith("machine_log_"))
            {
                return $@"
                CREATE TABLE [{table}](
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    machine_name NVARCHAR(MAX),
                    id_type INT,
                    mould INT,
                    start DATETIME NOT NULL,
                    finish DATETIME NULL,
                    shot INT DEFAULT 0,
                    category NVARCHAR(MAX) NULL,
                    problem NVARCHAR(MAX) NULL,
                    mould_category INT,
                    shift INT,
                    production_date DATE,
                    act_ct FLOAT DEFAULT 0,
                    status_start BIT
                )";
            }

            if (table == "machine_master")
                return @"
                CREATE TABLE machine_master(
                    id_machine INT IDENTITY(1,1) PRIMARY KEY,
                    shift INT,
                    machine_name NVARCHAR(MAX),
                    packer NVARCHAR(MAX),
                    material NVARCHAR(MAX),
                    id_type INT,
                    mould INT,
                    type NVARCHAR(MAX),
                    category NVARCHAR(MAX) NULL,
                    jo_no NVARCHAR(MAX),
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
                    visual_qc INT,
                    measure_qc INT
                )";

            if (table == "reject")
                return @"
                CREATE TABLE reject(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    id_machine INT,
                    machine_name NVARCHAR(MAX),
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
                    machine_name NVARCHAR(MAX),
                    packer NVARCHAR(MAX),
                    material NVARCHAR(MAX),
                    id_type INT,
                    mould INT,
                    type NVARCHAR(MAX),
                    jo_no NVARCHAR(MAX),
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
                    remark NVARCHAR(MAX),
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
                    type NVARCHAR(MAX),
                    qty_perct INT,
                    process NVARCHAR(MAX),
                    material NVARCHAR(MAX),
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
                    staff_name NVARCHAR(MAX),
                    staff_role NVARCHAR(MAX),
                    status NVARCHAR(MAX),
                    machine_name NVARCHAR(MAX),
                    start_date DATE,
                    end_date DATE,
                    work_shift INT
                )";

            if (table == "utilities")
                return @"
                CREATE TABLE utilities(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    id_machine INT,
                    machine_name NVARCHAR(MAX),
                    utility_name NVARCHAR(MAX),
                    start DATETIME,
                    finish DATETIME NULL,
                    category NVARCHAR(MAX),
                    shift INT,
                    production_date DATE
                )";

            if (table == "attendance")
                return @"
                CREATE TABLE attendance(
                    id INT IDENTITY(1,1) PRIMARY KEY,
                    staff_id INT,
                    staff_name NVARCHAR(MAX),
                    staff_role NVARCHAR(MAX),
                    status NVARCHAR(MAX),
                    machine_name NVARCHAR(MAX),
                    production_date DATE,
                    shift INT
                )";

            if (table == "calendar")
                return @"
                CREATE TABLE calendar(
                    production_date DATE NOT NULL,
                    shift INT NOT NULL,
                    day_type NVARCHAR(MAX) NOT NULL,
                    planned_hours FLOAT NOT NULL DEFAULT 12,
                    start DATETIME NOT NULL,
                    finish DATETIME NULL,
                    PRIMARY KEY (production_date, shift)
                )";

            if (table == "plc_passwords")
                return @"
                CREATE TABLE plc_passwords(
                    department  NVARCHAR(50) NOT NULL PRIMARY KEY,
                    password    INT          NOT NULL DEFAULT 0,
                    updated_at  DATETIME2    NOT NULL DEFAULT GETDATE()
                )";

            if (table == "db_log")
                return @"
                CREATE TABLE db_log (
                    id            INT IDENTITY(1,1) PRIMARY KEY,
                    id_machine    INT NULL,
                    time          DATETIME NOT NULL DEFAULT GETDATE(),
                    process       NVARCHAR(MAX) NOT NULL,
                    details       NVARCHAR(MAX) NULL,
                    error_message NVARCHAR(MAX) NULL
                )";

            return "";
        }
    }
}