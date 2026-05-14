using ClosedXML.Excel;
using CMS.Server.Models;

namespace CMS.Server.Services
{
    public class ExcelGenerationService
    {
        public byte[] GenerateExcelReport(List<Report> data, DateOnly productionDate, int shift)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Daily Production Report");

                AddReportHeader(worksheet, productionDate, shift);

                var headers = GetHeaderDefinitions();
                ApplyHeaders(worksheet, headers, 3);

                PopulateDataRows(worksheet, data, 4);

                ApplyFinalFormatting(worksheet);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private void AddReportHeader(IXLWorksheet worksheet, DateOnly date, int shift)
        {
            var titleCell = worksheet.Cell(1, 1);
            titleCell.Value = $"Daily Production Report - {date} - Shift {shift}";
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 14;
            worksheet.Range(1, 1, 1, 10).Merge();
        }

        private List<HeaderDefinition> GetHeaderDefinitions()
        {
            return new List<HeaderDefinition>
        {
            new(1, "M/C No", "#8EA9DB", 12),
            new(2, "Shift", "#8EA9DB", 10),
            new(3, "Packer Name", "#8EA9DB", 20),
            new(4, "Material", "#8EA9DB", 12),
            new(5, "SAP", "#8EA9DB", 12),
            new(6, "Mould", "#8EA9DB", 12),
            new(7, "Type", "#8EA9DB", 40),
            new(8, "JO No. (Prod. Order No.)", "#8EA9DB", 18),
            new(9, "Cav", "#8EA9DB", 10),
            new(10, "Gross Weight (gm)", "#8EA9DB", 16),
            new(11, "Net Weight (gm)", "#8EA9DB", 16),
            new(12, "Shot", "#8EA9DB", 10),
            new(13, "Qty Order (pcs)", "#8EA9DB", 15),
            new(14, "WIP Opening (pcs)", "#8EA9DB", 16),
            new(15, "WIP Closing (pcs)", "#8EA9DB", 16),
            new(16, "Shift Output", "#B1A0C7", 15),
            new(17, "Finish Good (Inward - pcs) To Warehouse", "#8EA9DB", 18),
            new(18, "Inward to Warehouse (kg)", "#B1A0C7", 18),
            new(19, "Accumulate Qty Build (pcs)", "#8EA9DB", 20),
            new(20, "Balance Qty (pcs)", "#B1A0C7", 16),
            new(21, "Material Used (kg)", "#B1A0C7", 16),
            new(22, "Runner (kg)", "#B1A0C7", 12),
            new(23, "Start Up (kg)", "#8EA9DB", 13),
            new(24, "Start Up (%)", "#B1A0C7", 13),
            new(25, "Prod. Reject (kg)", "#8EA9DB", 15),
            new(26, "Prod. Reject (%)", "#B1A0C7", 15),
            new(27, "Actual CT (s)", "#8EA9DB", 13),
            new(28, "Run Hours", "#8EA9DB", 12),
            new(29, "SAP Target CT (s)", "#8EA9DB", 16),
            new(30, "Mould Set Up Time (hrs) Full Set", "#FFFF66", 20),
            new(31, "Mould Set Up Time (hrs) Half Set", "#FFFF66", 20),
            new(32, "Mould Set Up Time (hrs) Blow Mould", "#FFFF66", 22),
            new(33, "M/C DT (hrs) Maintenance", "#FFFF66", 18),
            new(34, "M/C DT (hrs) Technician", "#FFFF66", 18),
            new(35, "Idle DT Prod/ QC/ Other", "#FFFF66", 18),
            new(36, "Remark", "#FFFF66", 30),
            new(37, "Part Scrap", "#FFFF66", 12),
            new(38, "Purging", "#FFFF66", 12),
            new(39, "Preform", "#FFFF66", 12),
            new(40, "Prod Reject (pcs)", "#B1A0C7", 16)
        };
        }

        private void ApplyHeaders(IXLWorksheet worksheet, List<HeaderDefinition> headers, int row)
        {
            foreach (var header in headers)
            {
                var cell = worksheet.Cell(row, header.Column);
                cell.Value = header.Title;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(header.BackgroundColor);
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.WrapText = true;
                worksheet.Column(header.Column).Width = header.Width;
            }
        }

        private void PopulateDataRows(IXLWorksheet worksheet, List<Report> data, int startRow)
        {
            int currentRow = startRow;

            foreach (var item in data)
            {
                worksheet.Cell(currentRow, 1).Value = item.machine_name;
                worksheet.Cell(currentRow, 2).Value = item.shift   ;
                worksheet.Cell(currentRow, 3).Value = item.packer;
                worksheet.Cell(currentRow, 4).Value = item.material;
                worksheet.Cell(currentRow, 5).Value = item.id_type;
                worksheet.Cell(currentRow, 6).Value = item.mould;
                worksheet.Cell(currentRow, 7).Value = item.type;
                worksheet.Cell(currentRow, 8).Value = item.jo_no;
                worksheet.Cell(currentRow, 9).Value = item.qty_perct;
                worksheet.Cell(currentRow, 10).Value = item.gross_weight;
                worksheet.Cell(currentRow, 11).Value = item.part_weight;
                worksheet.Cell(currentRow, 12).Value = item.shot_accum;
                worksheet.Cell(currentRow, 13).Value = item.qty_order;
                worksheet.Cell(currentRow, 14).Value = item.wip_opening;
                worksheet.Cell(currentRow, 15).Value = item.wip_opening;
                worksheet.Cell(currentRow, 17).Value = item.finish_good;
                worksheet.Cell(currentRow, 19).Value = item.qty_accum;
                worksheet.Cell(currentRow, 23).Value = item.reject_startup;
                worksheet.Cell(currentRow, 25).Value = item.reject_prod;
                worksheet.Cell(currentRow, 27).Value = item.act_ct;
                worksheet.Cell(currentRow, 28).Value = item.production_running;
                worksheet.Cell(currentRow, 29).Value = item.sap_ct;

                worksheet.Cell(currentRow, 30).Value = item.change_full_set;
                worksheet.Cell(currentRow, 31).Value = item.change_half_set;
                worksheet.Cell(currentRow, 32).Value = item.change_parts;
                worksheet.Cell(currentRow, 33).Value = item.maintenance_dt;
                worksheet.Cell(currentRow, 34).Value = item.technician_dt;
                worksheet.Cell(currentRow, 35).Value = item.production_dt;
                worksheet.Cell(currentRow, 36).Value = item.remark;
                worksheet.Cell(currentRow, 37).Value = item.part_scrap;
                worksheet.Cell(currentRow, 38).Value = item.reject_purging;
                worksheet.Cell(currentRow, 39).Value = item.reject_preform;

                ApplyFormulas(worksheet, currentRow);

                for (int col = 30; col <= 39; col++)
                {
                    worksheet.Cell(currentRow, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFF99");
                }

                for (int col = 1; col <= 40; col++)
                {
                    worksheet.Cell(currentRow, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                currentRow++;
            }
        }

        private void ApplyFormulas(IXLWorksheet worksheet, int row)
        {
            // Shift Output = Shot (L) * Cav (I)
            worksheet.Cell(row, 16).FormulaA1 = $"=L{row}*I{row}";

            // Inward to Warehouse (kg) = (Net Weight (K) * Finish Good (Q)) / 1000
            worksheet.Cell(row, 18).FormulaA1 = $"=(K{row}*Q{row})/1000";
            worksheet.Cell(row, 18).Style.NumberFormat.Format = "0.00";

            // Balance Qty (pcs) = Qty Order (M) - Accumulate Qty (S)
            worksheet.Cell(row, 20).FormulaA1 = $"=M{row}-S{row}";

            // Material Used (kg) = (Net Weight (K) * Shift Output (P)) / 1000
            worksheet.Cell(row, 21).FormulaA1 = $"=(K{row}*P{row})/1000";
            worksheet.Cell(row, 21).Style.NumberFormat.Format = "0.00";

            // Runner (kg) = ((Gross Weight (J) - Net Weight (K)) * Shift Output (P)) / 1000
            worksheet.Cell(row, 22).FormulaA1 = $"=((J{row}-K{row})*P{row})/1000";
            worksheet.Cell(row, 22).Style.NumberFormat.Format = "0.00";

            // Start Up (%) = (Start Up (W) / Material Used (U)) * 100
            worksheet.Cell(row, 24).FormulaA1 = $"=IF(U{row}=0,0,(W{row}/U{row})*100)";
            worksheet.Cell(row, 24).Style.NumberFormat.Format = "0.00";

            // Prod. Reject (%) = (Prod Reject (Y) / Material Used (U)) * 100
            worksheet.Cell(row, 26).FormulaA1 = $"=IF(U{row}=0,0,(Y{row}/U{row})*100)";
            worksheet.Cell(row, 26).Style.NumberFormat.Format = "0.00";

            // Prod Reject (pcs) = (Start Up + Prod Reject + Purging + Preform) / Part Weight
            worksheet.Cell(row, 40).FormulaA1 = $"=IF(K{row}=0,0,(W{row}+Y{row}+AL{row}+AM{row})/K{row})";
            worksheet.Cell(row, 40).Style.NumberFormat.Format = "0.00";
        }

        private void ApplyFinalFormatting(IXLWorksheet worksheet)
        {
            // Freeze header row (row 3)
            worksheet.SheetView.FreezeRows(3);

            // Auto-adjust row heights
            worksheet.Rows().AdjustToContents();

            // Set row height for header
            worksheet.Row(3).Height = 30;
        }

        // Helper class for header definitions
        private record HeaderDefinition(int Column, string Title, string BackgroundColor, double Width);

        private static readonly string[] RAW_COLUMNS =
        [
            "Machine Name",          // 1  (A)
            "Id Type",               // 2  (B)
            "Mould",                 // 3  (C)
            "Type",                  // 4  (D)
            "Shot",                  // 5  (E)
            "Run Time (hrs)",        // 6  (F)
            "Down Time (hrs)",       // 7  (G)
            "Available Hours (hrs)", // 8  (H)  ← formula: =F+G
            "Material Used (kg)",    // 9  (I)
            "Reject Weight (kg)",    // 10 (J)
            "SAP CT (s)",            // 11 (K)
            "Act CT (s)",            // 12 (L)
            "Total SAP Time (hrs)",  // 13 (M)  ← formula: =(E*K)/3600
            "Total Act Time (hrs)",  // 14 (N)  ← formula: =(E*L)/3600
        ];

        private static readonly string[] SUMMARY_COLUMNS =
        [
            "Machine Name",          // 1  (A)
            "Run Time (hrs)",        // 2  (B)
            "Down Time (hrs)",       // 3  (C)
            "Material Used (kg)",    // 4  (D)
            "Reject Weight (kg)",    // 5  (E)
            "Available Hours (hrs)", // 6  (F)  ← SUMIFS on Raw col H
            "Total SAP Time (hrs)",  // 7  (G)  ← SUMIFS on Raw col M
            "Total Act Time (hrs)",  // 8  (H)  ← SUMIFS on Raw col N
            "Availability (%)",      // 9  (I)
            "Performance (%)",       // 10 (J)
            "Quality (%)",           // 11 (K)
            "OEE (%)",               // 12 (L)
        ];

        private static readonly Dictionary<int, string> SUMMARY_HEADER_NOTES = new()
        {
            [6] = "Available Hours (hrs)\n= Run Time + Down Time\nRun/Down Time already zeroed:\n- OFFDAY: both = 0\n- OVERTIME (no production): both = 0\n- OVERTIME (with production): full values kept",
            [7] = "Total SAP Time (hrs)\n= (SAP CT × Shot) / 3600",
            [8] = "Total Act Time (hrs)\n= (Act CT × Shot) / 3600",
            [9] = "Availability (%)\n= Run Time / Available Hours × 100",
            [10] = "Performance (%)\n= Total SAP Time / Total Act Time × 100",
            [11] = "Quality (%)\n= (Material Used − Reject Weight) / Material Used × 100",
            [12] = "OEE (%)\n= Availability% × Performance% × Quality% / 10000",
        };

        private static readonly string[] COL_LETTERS =
            ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q"];

        public byte[] GenerateOEEReport(IEnumerable<OEERawRow> rows, DateOnly startDate, DateOnly endDate)
        {
            using var workbook = new XLWorkbook();

            var rawDataSheet = workbook.Worksheets.Add("Raw Data");
            var summarySheet = workbook.Worksheets.Add("OEE Summary");

            var rowList = rows.ToList();

            BuildOEERawSheet(rawDataSheet, rowList, startDate, endDate);
            BuildOEESummarySheet(summarySheet, rowList, startDate, endDate);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void BuildOEERawSheet(IXLWorksheet ws, List<OEERawRow> rows, DateOnly startDate, DateOnly endDate)
        {
            const int TITLE_ROW = 1;
            const int HEADER_ROW = 3;
            int totalCols = RAW_COLUMNS.Length; // 14

            var title = ws.Cell(TITLE_ROW, 1);
            title.Value = $"Raw Data  |  {startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}";
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            ws.Range(TITLE_ROW, 1, TITLE_ROW, totalCols).Merge();

            for (int c = 1; c <= totalCols; c++)
            {
                var cell = ws.Cell(HEADER_ROW, c);
                cell.Value = RAW_COLUMNS[c - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            }
            ws.Row(HEADER_ROW).Height = 35;

            int currentRow = HEADER_ROW + 1;
            foreach (var r in rows)
            {
                ws.Cell(currentRow, 1).Value = r.MachineName;
                ws.Cell(currentRow, 2).Value = r.IdType;
                ws.Cell(currentRow, 3).Value = r.Mould;
                ws.Cell(currentRow, 4).Value = r.Type;
                ws.Cell(currentRow, 5).Value = r.Shot;
                ws.Cell(currentRow, 6).Value = r.RunTime;
                ws.Cell(currentRow, 7).Value = r.DownTime;

                // Available Hours (H) = Run Time + Down Time
                ws.Cell(currentRow, 8).FormulaA1 = $"=F{currentRow}+G{currentRow}";

                ws.Cell(currentRow, 9).Value = r.MaterialUsed;
                ws.Cell(currentRow, 10).Value = r.RejectWeight;
                ws.Cell(currentRow, 11).Value = r.SapCt;
                ws.Cell(currentRow, 12).Value = r.ActCt;

                // Total SAP Time (M) = (Shot × SAP CT) / 3600
                ws.Cell(currentRow, 13).FormulaA1 = $"=(E{currentRow}*K{currentRow})/3600";
                // Total Act Time (N) = (Shot × Act CT) / 3600
                ws.Cell(currentRow, 14).FormulaA1 = $"=(E{currentRow}*L{currentRow})/3600";

                for (int c = 5; c <= totalCols; c++)
                    ws.Cell(currentRow, c).Style.NumberFormat.Format = "0.00";

                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(currentRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
            }

            ws.Columns(1, totalCols).AdjustToContents();
            ws.SheetView.FreezeRows(HEADER_ROW);
            ws.SheetView.FreezeColumns(1);
        }

        private static void BuildOEESummarySheet(IXLWorksheet ws, List<OEERawRow> rows, DateOnly startDate, DateOnly endDate)
        {
            const int TITLE_ROW = 1;
            const int HEADER_ROW = 3;
            int totalCols = SUMMARY_COLUMNS.Length; // 12

            var title = ws.Cell(TITLE_ROW, 1);
            title.Value = $"OEE Summary  |  {startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}";
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            ws.Range(TITLE_ROW, 1, TITLE_ROW, totalCols).Merge();

            for (int c = 1; c <= totalCols; c++)
            {
                var cell = ws.Cell(HEADER_ROW, c);
                cell.Value = SUMMARY_COLUMNS[c - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Fill.BackgroundColor = c switch
                {
                    12 => XLColor.FromHtml("#C00000"),
                    >= 7 => XLColor.FromHtml("#ED7D31"),
                    _ => XLColor.FromHtml("#4472C4"),
                };
            }
            ws.Row(HEADER_ROW).Height = 42;

            foreach (var (colIndex, noteText) in SUMMARY_HEADER_NOTES)
            {
                var comment = ws.Cell(HEADER_ROW, colIndex).CreateComment();
                comment.AddText(noteText);
                comment.Style.Size.SetWidth(35);
                comment.Style.Size.SetHeight(110);
                comment.Style.Alignment.SetAutomaticSize(false);
            }

            var machines = rows
                .GroupBy(r => new { r.IdMachine, r.MachineName })
                .OrderBy(g => g.Key.IdMachine)
                .ToList();

            int currentRow = HEADER_ROW + 1;
            foreach (var machineGroup in machines)
            {
                ws.Cell(currentRow, 1).Value = machineGroup.Key.MachineName;

                ws.Cell(currentRow, 2).FormulaA1 = $"=SUMIFS('Raw Data'!F:F,'Raw Data'!A:A,A{currentRow})";  // Run Time
                ws.Cell(currentRow, 3).FormulaA1 = $"=SUMIFS('Raw Data'!G:G,'Raw Data'!A:A,A{currentRow})";  // Down Time
                ws.Cell(currentRow, 4).FormulaA1 = $"=SUMIFS('Raw Data'!I:I,'Raw Data'!A:A,A{currentRow})";  // Material Used
                ws.Cell(currentRow, 5).FormulaA1 = $"=SUMIFS('Raw Data'!J:J,'Raw Data'!A:A,A{currentRow})";  // Reject Weight
                ws.Cell(currentRow, 6).FormulaA1 = $"=SUMIFS('Raw Data'!H:H,'Raw Data'!A:A,A{currentRow})";  // Available Hours
                ws.Cell(currentRow, 7).FormulaA1 = $"=SUMIFS('Raw Data'!M:M,'Raw Data'!A:A,A{currentRow})";  // Total SAP Time
                ws.Cell(currentRow, 8).FormulaA1 = $"=SUMIFS('Raw Data'!N:N,'Raw Data'!A:A,A{currentRow})";  // Total Act Time

                // Availability (I) = Run Time / Available Hours × 100
                ws.Cell(currentRow, 9).FormulaA1 = $"=IF(F{currentRow}>0,B{currentRow}/F{currentRow}*100,0)";
                // Performance (J) = Total SAP Time / Total Act Time × 100
                ws.Cell(currentRow, 10).FormulaA1 = $"=IF(H{currentRow}=0,0,G{currentRow}/H{currentRow}*100)";
                // Quality (K) = (Material Used - Reject) / Material Used × 100
                ws.Cell(currentRow, 11).FormulaA1 = $"=IF(D{currentRow}=0,0,MAX(0,(D{currentRow}-E{currentRow})/D{currentRow}*100))";
                // OEE (L)
                ws.Cell(currentRow, 12).FormulaA1 = $"=IF(OR(I{currentRow}=0,J{currentRow}=0,K{currentRow}=0),0,I{currentRow}/100*J{currentRow}/100*K{currentRow}/100*100)";

                for (int c = 2; c <= totalCols; c++)
                    ws.Cell(currentRow, c).Style.NumberFormat.Format = "0.00";

                ws.Cell(currentRow, 12).Style.Font.Bold = true;

                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(currentRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
            }

            int lastDataRow = Math.Max(HEADER_ROW + 1, currentRow - 1);

            // ── Totals / Averages ──────────────────────────────────────────────────────
            int totalRow = currentRow + 1;
            ws.Cell(totalRow, 1).Value = "TOTAL / AVG";

            foreach (int c in new[] { 2, 3, 4, 5, 6, 7, 8 })
            {
                string letter = COL_LETTERS[c - 1];
                ws.Cell(totalRow, c).FormulaA1 = $"=SUM({letter}4:{letter}{lastDataRow})";
            }
            foreach (int c in new[] { 9, 10, 11, 12 })
            {
                string letter = COL_LETTERS[c - 1];
                ws.Cell(totalRow, c).FormulaA1 = $"=AVERAGE({letter}4:{letter}{lastDataRow})";
            }

            for (int c = 1; c <= totalCols; c++)
            {
                ws.Cell(totalRow, c).Style.NumberFormat.Format = "0.00";
                ApplyTotalStyle(ws.Cell(totalRow, c));
            }

            // ── Target row ─────────────────────────────────────────────────────────────
            int targetRow = totalRow + 1;
            ws.Cell(targetRow, 1).Value = "TARGET";

            foreach (var (col, val) in new (int, double)[] { (9, 70.0), (10, 95.0), (11, 97.0), (12, 65.0) })
                ws.Cell(targetRow, col).Value = val;

            for (int c = 1; c <= totalCols; c++)
            {
                var cell = ws.Cell(targetRow, c);
                cell.Style.NumberFormat.Format = "0.00";
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC000");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            ws.Column(1).Width = 20;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 14;
            ws.Column(4).Width = 16;
            ws.Column(5).Width = 14;
            ws.Column(6).Width = 18;
            ws.Column(7).Width = 18;
            ws.Column(8).Width = 18;
            ws.Column(9).Width = 15;
            ws.Column(10).Width = 14;
            ws.Column(11).Width = 12;
            ws.Column(12).Width = 12;

            ws.SheetView.FreezeRows(HEADER_ROW);
            ws.SheetView.FreezeColumns(1);
        }

        private static void ApplyTotalStyle(IXLCell cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
    }

    public sealed record OEERawRow(
        int IdMachine,
        string MachineName,
        int IdType,
        string Mould,
        string Type,
        double Shot,
        double RunTime,
        double DownTime,
        double MaterialUsed,
        double RejectWeight,
        double SapCt,
        double ActCt,
        double TotalSapTime,
        double TotalActTime
    );
}