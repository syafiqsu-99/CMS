using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Services
{
    public class SAPService
    {
        private readonly string _connectionString;

        public SAPService(string connectionString)
        {
            _connectionString = connectionString;
        }
        private async ValueTask<SqlConnection> CreateConnection()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
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

                UPDATE r
                SET 
                    r.material     = s.material,
                    r.type         = s.type,
                    r.qty_perct    = s.qty_perct,
                    r.sap_ct       = s.sap_ct,
                    r.part_weight  = s.part_weight,
                    r.gross_weight = s.gross_weight
                FROM report AS r
                JOIN sap AS s 
                    ON s.id_type = @id_type 
                    AND s.mould   = @mould
                WHERE r.id_type = s.id_type
                    AND r.mould   = s.mould;
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
    }
}
