using ClosedXML.Excel;
using Microsoft.Data.SqlClient;

namespace CMS.Server.Services;

public class ReportExportService
{
    private readonly string _connectionString;
    private readonly SettingService _settingService;
    private readonly ILogger<ReportExportService> _logger;

    private const string SheetReport = "Daily Report";
    private const string SheetReference = "Reference Card";
    private const int HeaderTopRow = 4;
    private const int HeaderSubRow = 5;
    private const int DataStartRow = 6;
    private const int LastColumn = 41;
    private static readonly TimeSpan FileLockRetryDelay = TimeSpan.FromSeconds(3);

    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public ReportExportService(string connectionString, SettingService settingService, ILogger<ReportExportService> logger)
    {
        _connectionString = connectionString;
        _settingService = settingService;
        _logger = logger;
    }

    private async ValueTask<SqlConnection> CreateConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    // Scheduled path: honours the auto-save toggle, writes to the configured folder.
    public async Task ExportShiftAsync(DateOnly productionDate, int shift, CancellationToken ct = default)
    {
        var enabledRaw = await _settingService.GetSettingAsync("report_auto_save_enabled") ?? "false";
        if (!string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[ReportExport] Auto-save disabled; skipping {Date} shift {Shift}.", productionDate, shift);
            return;
        }

        await ExportShiftToFolderAsync(productionDate, shift, ct);
    }

    // Manual "save to folder" path: writes to the configured folder, returns the file path.
    public async Task<string> ExportShiftToFolderAsync(DateOnly productionDate, int shift, CancellationToken ct = default)
    {
        var rootPath = await _settingService.GetSettingAsync("report_folder_path");
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            _logger.LogWarning("[ReportExport] No folder path set; skipping {Date} shift {Shift}.", productionDate, shift);
            throw new InvalidOperationException("Report folder path is not configured.");
        }

        var monthFolder = Path.Combine(rootPath, $"{productionDate.Month}. {MonthNames[productionDate.Month - 1]}");
        Directory.CreateDirectory(monthFolder);

        var filePath = Path.Combine(monthFolder, $"{productionDate:dd.MM.yyyy}.xlsx");

        var (morningRows, nightRows, sapRows) = await LoadAllAsync(productionDate);

        await WriteWorkbookToFileWithRetryAsync(filePath, morningRows, nightRows, sapRows, ct);

        _logger.LogInformation("[ReportExport] Wrote {File} (shift {Shift}).", filePath, shift);
        return filePath;
    }

    // Manual "download to PC" path: builds the workbook in memory and returns the bytes.
    public async Task<(byte[] bytes, string fileName)> BuildWorkbookBytesAsync(DateOnly productionDate, int shift, CancellationToken ct = default)
    {
        var (morningRows, nightRows, sapRows) = await LoadAllAsync(productionDate);

        using var workbook = new XLWorkbook();
        BuildReportSheet(workbook, morningRows, nightRows);
        BuildReferenceSheet(workbook, sapRows);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"{productionDate:dd.MM.yyyy}.xlsx";
        return (stream.ToArray(), fileName);
    }

    private async Task<(List<ReportRow> morning, List<ReportRow> night, List<SapRow> sap)> LoadAllAsync(DateOnly productionDate)
    {
        var morningRows = await LoadReportRowsAsync(productionDate, 1);
        var nightRows = await LoadReportRowsAsync(productionDate, 2);
        var sapRows = await LoadSapRowsAsync();
        return (morningRows, nightRows, sapRows);
    }

    private async Task WriteWorkbookToFileWithRetryAsync(
        string filePath,
        List<ReportRow> morningRows,
        List<ReportRow> nightRows,
        List<SapRow> sapRows,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var workbook = new XLWorkbook();

                BuildReportSheet(workbook, morningRows, nightRows);
                BuildReferenceSheet(workbook, sapRows);

                workbook.SaveAs(filePath);
                return;
            }
            catch (IOException ex)
            {
                _logger.LogWarning("[ReportExport] File locked ({File}): {Message}. Retrying in {Delay}s.",
                    filePath, ex.Message, FileLockRetryDelay.TotalSeconds);
                await Task.Delay(FileLockRetryDelay, ct);
            }
        }
    }

    // ── Daily Report sheet ────────────────────────────────────────────────────

    private void BuildReportSheet(XLWorkbook workbook, List<ReportRow> morningRows, List<ReportRow> nightRows)
    {
        var ws = workbook.Worksheets.Add(SheetReport);

        ApplyGroupedHeader(ws);

        int row = DataStartRow;
        row = WriteBlock(ws, morningRows, row, "M");
        WriteYellowSeparator(ws, row);
        row++;
        row = WriteBlock(ws, nightRows, row, "N");

        ws.SheetView.FreezeRows(HeaderSubRow);
        ws.Columns().AdjustToContents(DataStartRow, row);
    }

    private int WriteBlock(IXLWorksheet ws, List<ReportRow> rows, int startRow, string shiftSuffix)
    {
        int row = startRow;
        foreach (var r in rows)
        {
            int c = 1;
            ws.Cell(row, c++).Value = $"{r.production_date:dd}{shiftSuffix}"; // A Date + shift suffix
            ws.Cell(row, c++).Value = r.machine_name;    // B  M/C No
            ws.Cell(row, c++).Value = r.packer;          // C  Packer Name
            ws.Cell(row, c++).Value = r.material;        // D  Material Type
            ws.Cell(row, c++).Value = r.id_type;         // E  Sap No.
            ws.Cell(row, c++).Value = r.mould;           // F  Mould No.
            ws.Cell(row, c++).Value = r.type;            // G  Product Code
            ws.Cell(row, c++).Value = r.jo_no;           // H  JO No.
            ws.Cell(row, c++).Value = r.qty_perct;       // I  Cav
            ws.Cell(row, c++).Value = r.gross_weight;    // J  Gross Weight
            ws.Cell(row, c++).Value = r.part_weight;     // K  Net Weight
            ws.Cell(row, c++).Value = r.shot;            // L  Shot
            ws.Cell(row, c++).Value = r.qty_order;       // M  Qty Order
            ws.Cell(row, c++).Value = r.wip_opening;     // N  WIP Opening
            ws.Cell(row, c++).Value = r.wip_closing;     // O  WIP Closing
            ws.Cell(row, c++).Value = r.shift_output;    // P  Shift Output
            ws.Cell(row, c++).Value = r.finish_good;     // Q  Finish Good
            ws.Cell(row, c++).Value = r.inward;          // R  Inward to W/H
            ws.Cell(row, c++).Value = r.qty_accum;       // S  Accumulate Qty Build
            ws.Cell(row, c++).Value = r.qty_balance;     // T  Balance Qty
            ws.Cell(row, c++).Value = r.material_used;   // U  Material Used
            ws.Cell(row, c++).Value = r.runner;          // V  Runner
            ws.Cell(row, c++).Value = r.reject_startup;      // W  Startup Reject kg
            ws.Cell(row, c++).Value = r.reject_startup_per;  // X  Startup Reject %
            ws.Cell(row, c++).Value = r.reject_prod;         // Y  Prod Reject kg
            ws.Cell(row, c++).Value = r.reject_prod_per;     // Z  Prod Reject %
            ws.Cell(row, c++).Value = r.act_ct;              // AA Actual CT
            ws.Cell(row, c++).Value = r.production_running;  // AB Run Hours
            ws.Cell(row, c++).Value = r.sap_ct;              // AC SAP CT
            ws.Cell(row, c++).Value = r.change_full_set;     // AD FS
            ws.Cell(row, c++).Value = r.change_half_set;     // AE HS
            ws.Cell(row, c++).Value = r.change_parts;        // AF BM
            ws.Cell(row, c++).Value = r.maintenance_dt;      // AG MTC
            ws.Cell(row, c++).Value = r.technician_dt;       // AH TECH
            ws.Cell(row, c++).Value = r.buyoff_dt;           // AI Buyoff
            ws.Cell(row, c++).Value = r.production_dt;       // AJ Idle Prod/QC/Others
            ws.Cell(row, c++).Value = r.remark;              // AK Remark
            ws.Cell(row, c++).Value = r.part_scrap;          // AL Part Scrap
            ws.Cell(row, c++).Value = r.reject_labelling;    // AM Labelling Reject
            ws.Cell(row, c++).Value = r.reject_purging;      // AN Purging
            ws.Cell(row, c++).Value = r.reject_preform;      // AO Preform
            row++;
        }
        return row;
    }

    private void WriteYellowSeparator(IXLWorksheet ws, int row)
    {
        var range = ws.Range(row, 1, row, LastColumn);
        range.Style.Fill.SetBackgroundColor(XLColor.Yellow);
    }

    private void ApplyGroupedHeader(IXLWorksheet ws)
    {
        // Single-column headers spanning both header rows
        var single = new (int col, string title)[]
        {
            (1, "Date"), (2, "M/C No"), (3, "Packer Name"), (4, "Material Type"),
            (5, "Sap No."), (6, "Mould No."), (7, "Product Code"), (8, "JO No. (Prod. Order No.)"),
            (9, "Cav"), (10, "Gross Weight (gm)"), (11, "Net Weight (gm)"), (12, "Shot"),
            (13, "Qty Order (pcs)"), (14, "WIP Opening (pcs)"), (15, "WIP Closing (pcs)"),
            (16, "Shift Output"), (17, "Finish Good (Inward - pcs)"), (18, "Inward to W/H (kg)"),
            (19, "Accumulate Qty Build (pcs)"), (20, "Balance Qty (pcs)"), (21, "Material Used (kg)"),
            (22, "Runner (kg)"), (23, "Startup Reject (kg)"), (24, "Startup Reject (%)"),
            (25, "Prod. Reject (kg)"), (26, "Prod. Reject (%)"), (27, "Actual CT (s)"),
            (28, "Run Hours"), (29, "SAP CT (s)"), (37, "Remark"), (38, "Part Scrap"),
            (39, "Labelling Reject"), (40, "Purging"), (41, "Preform")
        };

        foreach (var (col, title) in single)
        {
            ws.Range(HeaderTopRow, col, HeaderSubRow, col).Merge();
            var cell = ws.Cell(HeaderTopRow, col);
            cell.Value = title;
            StyleHeaderCell(cell);
        }

        // Grouped headers: top label + sub-labels
        WriteGroup(ws, 30, 32, "Mould Set Up Time (hrs)", new[] { "FS", "HS", "BM" });
        WriteGroup(ws, 33, 34, "M/C DT (hrs)", new[] { "MTC", "TECH" });
        WriteGroup(ws, 35, 35, "Buyoff (hrs)", new[] { "Buyoff" });
        WriteGroup(ws, 36, 36, "Idle DT (hrs)", new[] { "Prod / QC / Others" });

        ws.Row(HeaderTopRow).Style.Font.Bold = true;
        ws.Row(HeaderSubRow).Style.Font.Bold = true;
    }

    private void WriteGroup(IXLWorksheet ws, int startCol, int endCol, string groupTitle, string[] subTitles)
    {
        if (endCol > startCol)
            ws.Range(HeaderTopRow, startCol, HeaderTopRow, endCol).Merge();

        var top = ws.Cell(HeaderTopRow, startCol);
        top.Value = groupTitle;
        StyleHeaderCell(top);

        for (int i = 0; i < subTitles.Length; i++)
        {
            var sub = ws.Cell(HeaderSubRow, startCol + i);
            sub.Value = subTitles[i];
            StyleHeaderCell(sub);
        }
    }

    private void StyleHeaderCell(IXLCell cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Alignment.WrapText = true;
        cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#8EA9DB"));
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    // ── Reference Card sheet ──────────────────────────────────────────────────

    private void BuildReferenceSheet(XLWorkbook workbook, List<SapRow> sapRows)
    {
        var ws = workbook.Worksheets.Add(SheetReference);

        var headers = new[]
        {
            "Material", "Mould", "Material Description", "SAP Routing", "Process",
            "Material (Resin)", "Part Weight, g/pc", "QAQC w/ tolerance",
            "Gross Weight, g/pc", "Cycle Time, s/shot"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#B1A0C7"));
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int row = 2;
        foreach (var s in sapRows)
        {
            int c = 1;
            ws.Cell(row, c++).Value = s.id_type;       // Material
            ws.Cell(row, c++).Value = s.mould;         // Mould
            ws.Cell(row, c++).Value = s.type;          // Material Description
            ws.Cell(row, c++).Value = s.qty_perct;     // SAP Routing
            ws.Cell(row, c++).Value = s.process;       // Process
            ws.Cell(row, c++).Value = s.material;      // Material (Resin)
            ws.Cell(row, c++).Value = s.part_weight;   // Part Weight
            ws.Cell(row, c++).Value = s.tolerance;     // QAQC w/ tolerance
            ws.Cell(row, c++).Value = s.gross_weight;  // Gross Weight
            ws.Cell(row, c++).Value = s.sap_ct;        // Cycle Time
            row++;
        }

        ws.Columns().AdjustToContents(1, row);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task<List<ReportRow>> LoadReportRowsAsync(DateOnly productionDate, int shift)
    {
        const string sql = @"
            SELECT
                COALESCE(id_machine, 0)          AS id_machine,
                production_date,
                COALESCE(machine_name, '')       AS machine_name,
                COALESCE(packer, '')             AS packer,
                COALESCE(material, '')           AS material,
                COALESCE(id_type, 0)             AS id_type,
                COALESCE(mould, 0)               AS mould,
                COALESCE(type, '')               AS type,
                COALESCE(jo_no, '')              AS jo_no,
                COALESCE(qty_perct, 0)           AS qty_perct,
                COALESCE(gross_weight, 0.0)      AS gross_weight,
                COALESCE(part_weight, 0.0)       AS part_weight,
                COALESCE(shot, 0)                AS shot,
                COALESCE(qty_order, 0)           AS qty_order,
                COALESCE(wip_opening, 0)         AS wip_opening,
                COALESCE(wip_closing, 0)         AS wip_closing,
                COALESCE(shift_output, 0)        AS shift_output,
                COALESCE(finish_good, 0)         AS finish_good,
                COALESCE(inward, 0.0)            AS inward,
                COALESCE(qty_accum, 0)           AS qty_accum,
                COALESCE(qty_balance, 0)         AS qty_balance,
                COALESCE(material_used, 0.0)     AS material_used,
                COALESCE(runner, 0.0)            AS runner,
                COALESCE(reject_startup, 0.0)    AS reject_startup,
                COALESCE(reject_startup_per, 0.0) AS reject_startup_per,
                COALESCE(reject_prod, 0.0)       AS reject_prod,
                COALESCE(reject_prod_per, 0.0)   AS reject_prod_per,
                COALESCE(act_ct, 0.0)            AS act_ct,
                COALESCE(production_running, 0.0) AS production_running,
                COALESCE(sap_ct, 0.0)            AS sap_ct,
                COALESCE(change_full_set, 0.0)   AS change_full_set,
                COALESCE(change_half_set, 0.0)   AS change_half_set,
                COALESCE(change_parts, 0.0)      AS change_parts,
                COALESCE(maintenance_dt, 0.0)    AS maintenance_dt,
                COALESCE(technician_dt, 0.0)     AS technician_dt,
                COALESCE(buyoff_dt, 0.0)         AS buyoff_dt,
                COALESCE(production_dt, 0.0)     AS production_dt,
                COALESCE(remark, '')             AS remark,
                COALESCE(part_scrap, 0.0)        AS part_scrap,
                COALESCE(reject_labelling, 0.0)  AS reject_labelling,
                COALESCE(reject_purging, 0.0)    AS reject_purging,
                COALESCE(reject_preform, 0.0)    AS reject_preform
            FROM report
            WHERE production_date = @production_date AND shift = @shift
            ORDER BY id_machine";

        var rows = new List<ReportRow>();

        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@production_date", productionDate);
        cmd.Parameters.AddWithValue("@shift", shift);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new ReportRow
            {
                id_machine = Convert.ToInt32(reader["id_machine"]),
                production_date = DateOnly.FromDateTime(Convert.ToDateTime(reader["production_date"])),
                machine_name = Convert.ToString(reader["machine_name"]) ?? string.Empty,
                packer = Convert.ToString(reader["packer"]) ?? string.Empty,
                material = Convert.ToString(reader["material"]) ?? string.Empty,
                id_type = Convert.ToInt32(reader["id_type"]),
                mould = Convert.ToInt32(reader["mould"]),
                type = Convert.ToString(reader["type"]) ?? string.Empty,
                jo_no = Convert.ToString(reader["jo_no"]) ?? string.Empty,
                qty_perct = Convert.ToInt32(reader["qty_perct"]),
                gross_weight = Convert.ToDouble(reader["gross_weight"]),
                part_weight = Convert.ToDouble(reader["part_weight"]),
                shot = Convert.ToInt32(reader["shot"]),
                qty_order = Convert.ToInt32(reader["qty_order"]),
                wip_opening = Convert.ToInt32(reader["wip_opening"]),
                wip_closing = Convert.ToInt32(reader["wip_closing"]),
                shift_output = Convert.ToInt32(reader["shift_output"]),
                finish_good = Convert.ToInt32(reader["finish_good"]),
                inward = Convert.ToDouble(reader["inward"]),
                qty_accum = Convert.ToInt32(reader["qty_accum"]),
                qty_balance = Convert.ToInt32(reader["qty_balance"]),
                material_used = Convert.ToDouble(reader["material_used"]),
                runner = Convert.ToDouble(reader["runner"]),
                reject_startup = Convert.ToDouble(reader["reject_startup"]),
                reject_startup_per = Convert.ToDouble(reader["reject_startup_per"]),
                reject_prod = Convert.ToDouble(reader["reject_prod"]),
                reject_prod_per = Convert.ToDouble(reader["reject_prod_per"]),
                act_ct = Convert.ToDouble(reader["act_ct"]),
                production_running = Convert.ToDouble(reader["production_running"]),
                sap_ct = Convert.ToDouble(reader["sap_ct"]),
                change_full_set = Convert.ToDouble(reader["change_full_set"]),
                change_half_set = Convert.ToDouble(reader["change_half_set"]),
                change_parts = Convert.ToDouble(reader["change_parts"]),
                maintenance_dt = Convert.ToDouble(reader["maintenance_dt"]),
                technician_dt = Convert.ToDouble(reader["technician_dt"]),
                buyoff_dt = Convert.ToDouble(reader["buyoff_dt"]),
                production_dt = Convert.ToDouble(reader["production_dt"]),
                remark = Convert.ToString(reader["remark"]) ?? string.Empty,
                part_scrap = Convert.ToDouble(reader["part_scrap"]),
                reject_labelling = Convert.ToDouble(reader["reject_labelling"]),
                reject_purging = Convert.ToDouble(reader["reject_purging"]),
                reject_preform = Convert.ToDouble(reader["reject_preform"])
            });
        }

        return rows;
    }

    private async Task<List<SapRow>> LoadSapRowsAsync()
    {
        const string sql = @"
            SELECT
                COALESCE(id_type, 0)        AS id_type,
                COALESCE(mould, 0)          AS mould,
                COALESCE(type, '')          AS type,
                COALESCE(qty_perct, 0)      AS qty_perct,
                COALESCE(process, '')       AS process,
                COALESCE(material, '')      AS material,
                COALESCE(part_weight, 0.0)  AS part_weight,
                COALESCE(tolerance, 0.0)    AS tolerance,
                COALESCE(gross_weight, 0.0) AS gross_weight,
                COALESCE(sap_ct, 0.0)       AS sap_ct
            FROM sap
            ORDER BY id_type, mould";

        var rows = new List<SapRow>();

        await using var conn = await CreateConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new SapRow
            {
                id_type = Convert.ToInt32(reader["id_type"]),
                mould = Convert.ToInt32(reader["mould"]),
                type = Convert.ToString(reader["type"]) ?? string.Empty,
                qty_perct = Convert.ToInt32(reader["qty_perct"]),
                process = Convert.ToString(reader["process"]) ?? string.Empty,
                material = Convert.ToString(reader["material"]) ?? string.Empty,
                part_weight = Convert.ToDouble(reader["part_weight"]),
                tolerance = Convert.ToDouble(reader["tolerance"]),
                gross_weight = Convert.ToDouble(reader["gross_weight"]),
                sap_ct = Convert.ToDouble(reader["sap_ct"])
            });
        }

        return rows;
    }

    private sealed class ReportRow
    {
        public int id_machine { get; set; }
        public DateOnly production_date { get; set; }
        public string machine_name { get; set; } = string.Empty;
        public string packer { get; set; } = string.Empty;
        public string material { get; set; } = string.Empty;
        public int id_type { get; set; }
        public int mould { get; set; }
        public string type { get; set; } = string.Empty;
        public string jo_no { get; set; } = string.Empty;
        public int qty_perct { get; set; }
        public double gross_weight { get; set; }
        public double part_weight { get; set; }
        public int shot { get; set; }
        public int qty_order { get; set; }
        public int wip_opening { get; set; }
        public int wip_closing { get; set; }
        public int shift_output { get; set; }
        public int finish_good { get; set; }
        public double inward { get; set; }
        public int qty_accum { get; set; }
        public int qty_balance { get; set; }
        public double material_used { get; set; }
        public double runner { get; set; }
        public double reject_startup { get; set; }
        public double reject_startup_per { get; set; }
        public double reject_prod { get; set; }
        public double reject_prod_per { get; set; }
        public double act_ct { get; set; }
        public double production_running { get; set; }
        public double sap_ct { get; set; }
        public double change_full_set { get; set; }
        public double change_half_set { get; set; }
        public double change_parts { get; set; }
        public double maintenance_dt { get; set; }
        public double technician_dt { get; set; }
        public double buyoff_dt { get; set; }
        public double production_dt { get; set; }
        public string remark { get; set; } = string.Empty;
        public double part_scrap { get; set; }
        public double reject_labelling { get; set; }
        public double reject_purging { get; set; }
        public double reject_preform { get; set; }
    }

    private sealed class SapRow
    {
        public int id_type { get; set; }
        public int mould { get; set; }
        public string type { get; set; } = string.Empty;
        public int qty_perct { get; set; }
        public string process { get; set; } = string.Empty;
        public string material { get; set; } = string.Empty;
        public double part_weight { get; set; }
        public double tolerance { get; set; }
        public double gross_weight { get; set; }
        public double sap_ct { get; set; }
    }
}