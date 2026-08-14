using ClosedXML.Excel;
using Microsoft.Data.SqlClient;

namespace CMS.Server.Services;

public class ReportExportService
{
    private readonly string _connectionString;
    private readonly SettingService _settingService;
    private readonly ILogger<ReportExportService> _logger;
    private readonly bool _isDevelopment;

    private const string SheetReport = "Daily Report";
    private const string SheetMonth = "Monthly Report";
    private const string SheetReference = "Reference Card";
    private const int HeaderRow = 1;
    private const int DataStartRow = 2;
    private static readonly TimeSpan FileLockRetryDelay = TimeSpan.FromSeconds(3);

    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    private static readonly ReportColumn[] Columns =
    {
        new("machine_name",       false, 12),
        new("production_date",    false, 12),
        new("shift",              false, 7),
        new("packer",             false, 18),
        new("material",           false, 12),
        new("id_type",            false, 10),
        new("mould",              false, 10),
        new("type",               false, 34),
        new("jo_no",              false, 16),
        new("qty_perct",          false, 8),
        new("gross_weight",       false, 12),
        new("part_weight",        false, 12),
        new("shot",               false, 10),
        new("qty_order",          false, 12),
        new("wip_opening",        false, 12),
        new("wip_closing",        false, 12),
        new("shift_output",       true,  12),
        new("finish_good",        false, 14),
        new("inward",             true,  14),
        new("qty_accum",          false, 14),
        new("qty_balance",        true,  12),
        new("material_used",      true,  14),
        new("runner",             true,  12),
        new("reject_startup",     false, 13),
        new("reject_startup_per", true,  13),
        new("reject_prod",        false, 13),
        new("reject_prod_per",    true,  13),
        new("act_ct",             false, 12),
        new("production_running", false, 12),
        new("sap_ct",             false, 12),
        new("change_full_set",    false, 14),
        new("change_half_set",    false, 14),
        new("change_parts",       false, 14),
        new("maintenance_dt",     false, 14),
        new("technician_dt",      false, 14),
        new("production_dt",      false, 14),
        new("buyoff_dt",          false, 12),
        new("planned_dt",         false, 12),
        new("avail_hour",         true,  12),
        new("remark",             false, 28),
        new("part_scrap",         false, 12),
        new("reject_labelling",   false, 14),
        new("reject_purging",     false, 12),
        new("reject_preform",     false, 12),
        new("reject_total_pcs",   false, 12),
    };

    private static int LastColumn => Columns.Length;

    public ReportExportService(string connectionString, SettingService settingService, ILogger<ReportExportService> logger, bool isDevelopment = false)
    {
        _connectionString = connectionString;
        _settingService = settingService;
        _logger = logger;
        _isDevelopment = isDevelopment;
    }

    private async ValueTask<SqlConnection> CreateConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<object> TestFolderPathAsync(string? overridePath = null)
    {
        var rootPath = string.IsNullOrWhiteSpace(overridePath)
            ? await _settingService.GetSettingAsync("report_folder_path")
            : overridePath.Trim();

        if (string.IsNullOrWhiteSpace(rootPath))
            return new { ok = false, message = "No folder path is configured." };

        if (!Directory.Exists(rootPath))
            return new { ok = false, message = $"Folder not found or not reachable: {rootPath}" };

        try
        {
            var probe = Path.Combine(rootPath, $".cms_write_test_{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probe, "ok");
            File.Delete(probe);
            return new { ok = true, message = $"Path is reachable and writable: {rootPath}" };
        }
        catch (UnauthorizedAccessException)
        {
            return new { ok = false, message = $"Path exists but is not writable by the service account: {rootPath}" };
        }
        catch (Exception ex)
        {
            return new { ok = false, message = $"Path check failed: {ex.Message}" };
        }
    }

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

        var dailyPath = Path.Combine(monthFolder, $"{productionDate:dd.MM.yyyy}.xlsx");
        var monthlyPath = Path.Combine(monthFolder, $"{MonthNames[productionDate.Month - 1]} {productionDate.Year} - .xlsx");

        var (morningRows, nightRows, sapRows) = await LoadAllAsync(productionDate);

        await WriteDailyWorkbookWithRetryAsync(dailyPath, morningRows, nightRows, sapRows, ct);

        var shiftRows = shift == 1 ? morningRows : nightRows;
        await AppendMonthlyWithRetryAsync(monthlyPath, productionDate, shift, shiftRows, sapRows, ct);

        _logger.LogInformation("[ReportExport] Wrote {Daily} and updated {Monthly} (shift {Shift}).", dailyPath, monthlyPath, shift);
        return dailyPath;
    }

    // Manual "download to PC" path: builds the daily workbook in memory.
    public async Task<(byte[] bytes, string fileName)> BuildWorkbookBytesAsync(DateOnly productionDate, int shift, CancellationToken ct = default)
    {
        var (morningRows, nightRows, sapRows) = await LoadAllAsync(productionDate);

        using var workbook = new XLWorkbook();
        BuildDailySheet(workbook, morningRows, nightRows);
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

    // ── Daily file ────────────────────────────────────────────────────────────

    private async Task WriteDailyWorkbookWithRetryAsync(
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
                BuildDailySheet(workbook, morningRows, nightRows);
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

    private void BuildDailySheet(XLWorkbook workbook, List<ReportRow> morningRows, List<ReportRow> nightRows)
    {
        var ws = workbook.Worksheets.Add(SheetReport);

        WriteHeaderRow(ws);

        int row = DataStartRow;
        row = WriteBlock(ws, morningRows, row);
        WriteYellowSeparator(ws, row);
        row++;
        row = WriteBlock(ws, nightRows, row);

        ApplyFixedWidths(ws);
        ws.SheetView.FreezeRows(HeaderRow);
    }

    // ── Monthly file (append, dedupe by production_date+shift+id_machine) ───────

    private async Task AppendMonthlyWithRetryAsync(
        string filePath,
        DateOnly productionDate,
        int shift,
        List<ReportRow> shiftRows,
        List<SapRow> sapRows,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var existing = ReadMonthlyRows(filePath);

                // Remove any existing rows for the same (production_date, shift, id_machine)
                // so a re-run or an edit replaces rather than duplicates.
                var incomingKeys = shiftRows
                    .Select(r => (r.production_date, shift, r.id_machine))
                    .ToHashSet();

                var merged = existing
                    .Where(r => !incomingKeys.Contains((r.production_date, r.shift, r.id_machine)))
                    .Concat(shiftRows.Select(r => new MonthlyRow(r, shift)))
                    .OrderBy(r => r.production_date)
                    .ThenBy(r => r.shift)
                    .ThenBy(r => r.id_machine)
                    .ToList();

                using var workbook = new XLWorkbook();
                BuildMonthlySheet(workbook, merged);
                BuildReferenceSheet(workbook, sapRows);
                workbook.SaveAs(filePath);
                return;
            }
            catch (IOException ex)
            {
                _logger.LogWarning("[ReportExport] Monthly file locked ({File}): {Message}. Retrying in {Delay}s.",
                    filePath, ex.Message, FileLockRetryDelay.TotalSeconds);
                await Task.Delay(FileLockRetryDelay, ct);
            }
        }
    }

    // Reads back the id_machine, production_date and shift plus the raw cell values
    // of an existing monthly file so previously-written shifts are preserved on append.
    private List<MonthlyRow> ReadMonthlyRows(string filePath)
    {
        var result = new List<MonthlyRow>();
        if (!File.Exists(filePath)) return result;

        using var workbook = new XLWorkbook(filePath);
        if (!workbook.Worksheets.TryGetWorksheet(SheetMonth, out var ws)) return result;

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (int r = DataStartRow; r <= lastRow; r++)
        {
            var nameCell = ws.Cell(r, ColumnIndex("machine_name"));
            if (nameCell.IsEmpty()) continue;

            var pd = ws.Cell(r, ColumnIndex("production_date")).GetValue<DateTime>();
            var sh = (int)ws.Cell(r, ColumnIndex("shift")).GetValue<double>();

            var values = new Dictionary<string, IXLCell>();
            foreach (var col in Columns)
                values[col.Key] = ws.Cell(r, ColumnIndex(col.Key));

            result.Add(new MonthlyRow(
                DateOnly.FromDateTime(pd),
                sh,
                idMachine: FindIdMachineHidden(ws, r),
                cells: values));
        }

        return result;
    }

    private static int HiddenIdColumn => LastColumn + 1;

    private static int FindIdMachineHidden(IXLWorksheet ws, int row)
    {
        var cell = ws.Cell(row, HiddenIdColumn);
        return cell.IsEmpty() ? 0 : (int)cell.GetValue<double>();
    }

    private void BuildMonthlySheet(XLWorkbook workbook, List<MonthlyRow> rows)
    {
        var ws = workbook.Worksheets.Add(SheetMonth);

        WriteHeaderRow(ws);

        int row = DataStartRow;
        foreach (var mr in rows)
        {
            if (mr.Source is not null)
                WriteReportRow(ws, row, mr.Source, mr.shift);
            else
                CopyMonthlyRow(ws, row, mr);

            ws.Cell(row, HiddenIdColumn).Value = mr.id_machine;
            row++;
        }

        ApplyFixedWidths(ws);
        ws.Column(HiddenIdColumn).Hide();
        ws.SheetView.FreezeRows(HeaderRow);
    }

    private void CopyMonthlyRow(IXLWorksheet ws, int row, MonthlyRow mr)
    {
        foreach (var (i, col) in Columns.Select((c, i) => (i, c)))
        {
            var target = ws.Cell(row, i + 1);
            if (mr.Cells is not null && mr.Cells.TryGetValue(col.Key, out var src))
                target.Value = src.Value;
        }
    }

    // ── Shared writers ─────────────────────────────────────────────────────────

    private void WriteHeaderRow(IXLWorksheet ws)
    {
        for (int i = 0; i < Columns.Length; i++)
        {
            var cell = ws.Cell(HeaderRow, i + 1);
            cell.Value = Columns[i].Key;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Fill.SetBackgroundColor(
                Columns[i].Computed ? XLColor.FromHtml("#B1A0C7") : XLColor.FromHtml("#8EA9DB"));
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
    }

    private int WriteBlock(IXLWorksheet ws, List<ReportRow> rows, int startRow)
    {
        int row = startRow;
        foreach (var r in rows)
        {
            WriteReportRow(ws, row, r, r.shift);
            row++;
        }
        return row;
    }

    private void WriteReportRow(IXLWorksheet ws, int row, ReportRow r, int shift)
    {
        int c = 1;
        ws.Cell(row, c++).Value = r.machine_name;
        ws.Cell(row, c++).Value = r.production_date.ToDateTime(TimeOnly.MinValue);
        ws.Cell(row, c - 1).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Cell(row, c++).Value = shift;
        ws.Cell(row, c++).Value = r.packer;
        ws.Cell(row, c++).Value = r.material;
        ws.Cell(row, c++).Value = r.id_type;
        ws.Cell(row, c++).Value = r.mould;
        ws.Cell(row, c++).Value = r.type;
        ws.Cell(row, c++).Value = r.jo_no;
        ws.Cell(row, c++).Value = r.qty_perct;
        ws.Cell(row, c++).Value = r.gross_weight;
        ws.Cell(row, c++).Value = r.part_weight;
        ws.Cell(row, c++).Value = r.shot;
        ws.Cell(row, c++).Value = r.qty_order;
        ws.Cell(row, c++).Value = r.wip_opening;
        ws.Cell(row, c++).Value = r.wip_closing;
        ws.Cell(row, c++).Value = r.shift_output;
        ws.Cell(row, c++).Value = r.finish_good;
        ws.Cell(row, c++).Value = r.inward;
        ws.Cell(row, c++).Value = r.qty_accum;
        ws.Cell(row, c++).Value = r.qty_balance;
        ws.Cell(row, c++).Value = r.material_used;
        ws.Cell(row, c++).Value = r.runner;
        ws.Cell(row, c++).Value = r.reject_startup;
        ws.Cell(row, c++).Value = r.reject_startup_per;
        ws.Cell(row, c++).Value = r.reject_prod;
        ws.Cell(row, c++).Value = r.reject_prod_per;
        ws.Cell(row, c++).Value = r.act_ct;
        ws.Cell(row, c++).Value = r.production_running;
        ws.Cell(row, c++).Value = r.sap_ct;
        ws.Cell(row, c++).Value = r.change_full_set;
        ws.Cell(row, c++).Value = r.change_half_set;
        ws.Cell(row, c++).Value = r.change_parts;
        ws.Cell(row, c++).Value = r.maintenance_dt;
        ws.Cell(row, c++).Value = r.technician_dt;
        ws.Cell(row, c++).Value = r.production_dt;
        ws.Cell(row, c++).Value = r.buyoff_dt;
        ws.Cell(row, c++).Value = r.planned_dt;
        ws.Cell(row, c++).Value = r.avail_hour;
        ws.Cell(row, c++).Value = r.remark;
        ws.Cell(row, c++).Value = r.part_scrap;
        ws.Cell(row, c++).Value = r.reject_labelling;
        ws.Cell(row, c++).Value = r.reject_purging;
        ws.Cell(row, c++).Value = r.reject_preform;
        ws.Cell(row, c++).Value = r.reject_total_pcs;

        for (int col = 1; col <= LastColumn; col++)
            ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private void WriteYellowSeparator(IXLWorksheet ws, int row)
    {
        var range = ws.Range(row, 1, row, LastColumn);
        range.Style.Fill.SetBackgroundColor(XLColor.Yellow);
    }

    // Fixed per-column widths from the schema. Headers no longer follow the data,
    // so short data under a long header name never squashes the column.
    private void ApplyFixedWidths(IXLWorksheet ws)
    {
        for (int i = 0; i < Columns.Length; i++)
            ws.Column(i + 1).Width = Columns[i].Width;
    }

    private static int ColumnIndex(string key)
    {
        for (int i = 0; i < Columns.Length; i++)
            if (Columns[i].Key == key) return i + 1;
        return 0;
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
            ws.Cell(row, c++).Value = s.id_type;
            ws.Cell(row, c++).Value = s.mould;
            ws.Cell(row, c++).Value = s.type;
            ws.Cell(row, c++).Value = s.qty_perct;
            ws.Cell(row, c++).Value = s.process;
            ws.Cell(row, c++).Value = s.material;
            ws.Cell(row, c++).Value = s.part_weight;
            ws.Cell(row, c++).Value = s.tolerance;
            ws.Cell(row, c++).Value = s.gross_weight;
            ws.Cell(row, c++).Value = s.sap_ct;
            row++;
        }

        ws.Columns().AdjustToContents(1, row);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private async Task<List<ReportRow>> LoadReportRowsAsync(DateOnly productionDate, int shift)
    {
        // Test machine (id_machine = 0) is excluded from saved reports in production,
        // but kept visible in development. Mirrors BaseService.TestMachineFilter.
        var testFilter = _isDevelopment ? "" : " AND id_machine <> 0";

        var sql = $@"
            SELECT
                COALESCE(id_machine, 0)          AS id_machine,
                production_date,
                COALESCE(shift, 0)               AS shift,
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
                COALESCE(production_dt, 0.0)     AS production_dt,
                COALESCE(buyoff_dt, 0.0)         AS buyoff_dt,
                COALESCE(planned_dt, 0.0)        AS planned_dt,
                COALESCE(avail_hour, 0.0)        AS avail_hour,
                COALESCE(remark, '')             AS remark,
                COALESCE(part_scrap, 0.0)        AS part_scrap,
                COALESCE(reject_labelling, 0.0)  AS reject_labelling,
                COALESCE(reject_purging, 0.0)    AS reject_purging,
                COALESCE(reject_preform, 0.0)    AS reject_preform,
                COALESCE(reject_total_pcs, 0)    AS reject_total_pcs
            FROM report
            WHERE production_date = @production_date AND shift = @shift{testFilter}
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
                shift = Convert.ToInt32(reader["shift"]),
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
                production_dt = Convert.ToDouble(reader["production_dt"]),
                buyoff_dt = Convert.ToDouble(reader["buyoff_dt"]),
                planned_dt = Convert.ToDouble(reader["planned_dt"]),
                avail_hour = Convert.ToDouble(reader["avail_hour"]),
                remark = Convert.ToString(reader["remark"]) ?? string.Empty,
                part_scrap = Convert.ToDouble(reader["part_scrap"]),
                reject_labelling = Convert.ToDouble(reader["reject_labelling"]),
                reject_purging = Convert.ToDouble(reader["reject_purging"]),
                reject_preform = Convert.ToDouble(reader["reject_preform"]),
                reject_total_pcs = Convert.ToInt32(reader["reject_total_pcs"])
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

    private sealed record ReportColumn(string Key, bool Computed, double Width);

    // Wraps either a freshly-loaded report row (Source) or a row copied back from
    // an existing monthly file (Cells). production_date/shift/id_machine drive sort+dedupe.
    private sealed class MonthlyRow
    {
        public DateOnly production_date { get; }
        public int shift { get; }
        public int id_machine { get; }
        public ReportRow? Source { get; }
        public Dictionary<string, IXLCell>? Cells { get; }

        public MonthlyRow(ReportRow source, int shift)
        {
            Source = source;
            production_date = source.production_date;
            this.shift = shift;
            id_machine = source.id_machine;
        }

        public MonthlyRow(DateOnly production_date, int shift, int idMachine, Dictionary<string, IXLCell> cells)
        {
            this.production_date = production_date;
            this.shift = shift;
            id_machine = idMachine;
            Cells = cells;
        }
    }

    private sealed class ReportRow
    {
        public int id_machine { get; set; }
        public DateOnly production_date { get; set; }
        public int shift { get; set; }
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
        public double production_dt { get; set; }
        public double buyoff_dt { get; set; }
        public double planned_dt { get; set; }
        public double avail_hour { get; set; }
        public string remark { get; set; } = string.Empty;
        public double part_scrap { get; set; }
        public double reject_labelling { get; set; }
        public double reject_purging { get; set; }
        public double reject_preform { get; set; }
        public int reject_total_pcs { get; set; }
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