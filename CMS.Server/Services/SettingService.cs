using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services;

public class SettingService(PlcService plcService, string connectionString) : BaseService(connectionString, plcService)
{
    // ── PLC ────────────────────────────────────────────────────────────────────

    public object ChangeDepartmentPasswords(Dictionary<string, int> passwords)
    {
        plcService.ChangePassword(passwords);
        return new { success = true, updated = passwords.Keys };
    }

    public object ReadSubPlcSignals(int machineId)
        => plcService.ReadSubPlcSignals(machineId);

    public Dictionary<string, object?> ReadAllPlcSignals()
    {
        var result = new Dictionary<string, object?>();
        Parallel.For(1, 27, machineId =>
        {
            try
            {
                var data = plcService.ReadSubPlcSignals(machineId);
                lock (result) result[machineId.ToString()] = data;
            }
            catch
            {
                lock (result) result[machineId.ToString()] = null;
            }
        });
        return result;
    }

    // ── SAP — full list (used internally by import/export helpers) ─────────────

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

    // ── SAP — server-side paginated + filtered (used by the data table) ────────

    /// <summary>
    /// Returns a paged slice of the SAP table plus the total matching row count.
    /// <paramref name="search"/> is matched against id_type, mould, type, process, and material.
    /// Pass <paramref name="search"/> as null / empty to skip filtering.
    /// </summary>
    public async Task<(List<object> Items, int TotalCount)> LoadSAPPaged(
        int page,
        int pageSize,
        string? search)
    {
        // Clamp inputs — never trust the caller blindly
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        int offset = (page - 1) * pageSize;

        // Build an optional WHERE clause. LIKE with a sanitised parameter is safe.
        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        string whereClause = hasSearch
            ? @"WHERE CAST(id_type AS NVARCHAR) LIKE @search
                   OR CAST(mould   AS NVARCHAR) LIKE @search
                   OR type     LIKE @search
                   OR process  LIKE @search
                   OR material LIKE @search"
            : "";

        // Single round-trip: count + page in one batch
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

        // First result set → total count
        if (await reader.ReadAsync())
            total = reader.GetInt32(0);

        // Second result set → page rows
        await reader.NextResultAsync();
        while (await reader.ReadAsync())
            items.Add(MapSapRow(reader));

        return (items, total);
    }

    // ── SAP CRUD ───────────────────────────────────────────────────────────────

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

    // ── Private helpers ────────────────────────────────────────────────────────

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