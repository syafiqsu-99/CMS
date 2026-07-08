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
            new(36, "Buyoff DT (hrs)", "#FFFF66", 16),
            new(37, "Planned DT (hrs)", "#FFFF66", 16),
            new(38, "Remark", "#FFFF66", 30),
            new(39, "Part Scrap", "#FFFF66", 12),
            new(40, "Purging", "#FFFF66", 12),
            new(41, "Preform", "#FFFF66", 12),
            new(42, "Prod Reject (pcs)", "#B1A0C7", 16)
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
                worksheet.Cell(currentRow, 2).Value = item.shift;
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
                worksheet.Cell(currentRow, 36).Value = item.buyoff_dt;
                worksheet.Cell(currentRow, 37).Value = item.planned_dt;
                worksheet.Cell(currentRow, 38).Value = item.remark;
                worksheet.Cell(currentRow, 39).Value = item.part_scrap;
                worksheet.Cell(currentRow, 40).Value = item.reject_purging;
                worksheet.Cell(currentRow, 41).Value = item.reject_preform;

                ApplyFormulas(worksheet, currentRow);

                for (int col = 30; col <= 41; col++)
                {
                    worksheet.Cell(currentRow, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFFF99");
                }

                for (int col = 1; col <= 42; col++)
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
            worksheet.Cell(row, 42).FormulaA1 = $"=IF(K{row}=0,0,(W{row}+Y{row}+AN{row}+AO{row})/K{row})";
            worksheet.Cell(row, 42).Style.NumberFormat.Format = "0.00";
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
            "Machine Name",             // 1  (A)
            "Id Type",                  // 2  (B)
            "Mould",                    // 3  (C)
            "Type",                     // 4  (D)
            "Shot",                     // 5  (E)
            "Run Time (hrs)",           // 6  (F)
            "Unplanned Downtime (hrs)", // 7  (G)
            "Planned Downtime (hrs)",   // 8  (H)
            "Operating Hours (hrs)",    // 9  (I)  ← formula: =F+G
            "Available Hours (hrs)",    // 10 (J)  ← formula: =F+G+H
            "Material Used (kg)",       // 11 (K)
            "Reject Weight (kg)",       // 12 (L)
            "SAP CT (s)",               // 13 (M)
            "Act CT (s)",               // 14 (N)
            "Total SAP Time (hrs)",     // 15 (O)  ← formula: =(E*M)/3600
            "Total Act Time (hrs)",     // 16 (P)  ← formula: =(E*N)/3600
        ];

        private static readonly string[] SUMMARY_COLUMNS =
        [
            "Machine Name",             // 1  (A)
            "Run Time (hrs)",           // 2  (B)
            "Unplanned Downtime (hrs)", // 3  (C)
            "Planned Downtime (hrs)",   // 4  (D)
            "Material Used (kg)",       // 5  (E)
            "Reject Weight (kg)",       // 6  (F)
            "Operating Hours (hrs)",    // 7  (G)  ← SUMIFS on Raw col I
            "Available Hours (hrs)",    // 8  (H)  ← SUMIFS on Raw col J
            "Total SAP Time (hrs)",     // 9  (I)  ← SUMIFS on Raw col O
            "Total Act Time (hrs)",     // 10 (J)  ← SUMIFS on Raw col P
            "OEE (%)",                  // 11 (K)
            "Performance (%)",          // 12 (L)
            "Availability (%)",         // 13 (M)
            "Quality (%)",              // 14 (N)
        ];

        private static readonly Dictionary<int, string> SUMMARY_HEADER_NOTES = new()
        {
            [7] = "Operating Hours (hrs)\n= Run Time + Unplanned Downtime",
            [8] = "Available Hours (hrs)\n= Run Time + Unplanned + Planned Downtime\nAll values already zeroed:\n- OFFDAY: all = 0\n- OVERTIME (no production): all = 0\n- OVERTIME (with production): full values kept",
            [9] = "Total SAP Time (hrs)\n= (SAP CT × Shot) / 3600",
            [10] = "Total Act Time (hrs)\n= (Act CT × Shot) / 3600",
            [11] = "OEE (%)\n= Availability% × Performance% × Quality% / 10000",
            [12] = "Performance (%)\n= Total SAP Time / Total Act Time × 100",
            [13] = "Availability (%)\n= Run Time / Operating Hours × 100",
            [14] = "Quality (%)\n= (Material Used − Reject Weight) / Material Used × 100",
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
            int totalCols = RAW_COLUMNS.Length; // 16

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
                ws.Cell(currentRow, 1).Value = r.machine_name; //A
                ws.Cell(currentRow, 2).Value = r.id_type; //B
                ws.Cell(currentRow, 3).Value = r.mould; //C
                ws.Cell(currentRow, 4).Value = r.type; //D
                ws.Cell(currentRow, 5).Value = r.shot; //E
                ws.Cell(currentRow, 6).Value = r.run_time; //F
                ws.Cell(currentRow, 7).Value = r.unplanned_dt; //G
                ws.Cell(currentRow, 8).Value = r.planned_dt; //H

                // Operating Hours (I) = Run Time + Unplanned Downtime
                ws.Cell(currentRow, 9).FormulaA1 = $"=F{currentRow}+G{currentRow}";
                // Available Hours (J) = Run Time + Unplanned Downtime + Planned Downtime
                ws.Cell(currentRow, 10).FormulaA1 = $"=F{currentRow}+G{currentRow}+H{currentRow}";

                ws.Cell(currentRow, 11).Value = r.material_used; //K
                ws.Cell(currentRow, 12).Value = r.reject_weight; //L
                ws.Cell(currentRow, 13).Value = r.sap_ct; //M
                ws.Cell(currentRow, 14).Value = r.act_ct; //N

                // Total SAP Time (O): SUM(shot × sap_ct) / 3600
                ws.Cell(currentRow, 15).Value = r.total_sap_time;
                // Total Act Time (P): SUM(shot × act_ct) / 3600
                ws.Cell(currentRow, 16).Value = r.total_actual_time;

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
                    14 => XLColor.FromHtml("#C00000"),
                    >= 11 => XLColor.FromHtml("#ED7D31"),
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
                .GroupBy(r => new { r.id_machine, r.machine_name })
                .OrderBy(g => g.Key.id_machine)
                .ToList();

            int currentRow = HEADER_ROW + 1;
            foreach (var machineGroup in machines)
            {
                ws.Cell(currentRow, 1).Value = machineGroup.Key.machine_name; // A

                ws.Cell(currentRow, 2).FormulaA1 = $"=SUMIFS('Raw Data'!F:F,'Raw Data'!A:A,A{currentRow})";  // Run Time B
                ws.Cell(currentRow, 3).FormulaA1 = $"=SUMIFS('Raw Data'!G:G,'Raw Data'!A:A,A{currentRow})";  // Unplanned Downtime C
                ws.Cell(currentRow, 4).FormulaA1 = $"=SUMIFS('Raw Data'!H:H,'Raw Data'!A:A,A{currentRow})";  // Planned Downtime D
                ws.Cell(currentRow, 5).FormulaA1 = $"=SUMIFS('Raw Data'!K:K,'Raw Data'!A:A,A{currentRow})";  // Material Used E
                ws.Cell(currentRow, 6).FormulaA1 = $"=SUMIFS('Raw Data'!L:L,'Raw Data'!A:A,A{currentRow})";  // Reject Weight F
                ws.Cell(currentRow, 7).FormulaA1 = $"=SUMIFS('Raw Data'!I:I,'Raw Data'!A:A,A{currentRow})";  // Operating Hours G
                ws.Cell(currentRow, 8).FormulaA1 = $"=SUMIFS('Raw Data'!J:J,'Raw Data'!A:A,A{currentRow})";  // Available Hours H
                ws.Cell(currentRow, 9).FormulaA1 = $"=SUMIFS('Raw Data'!O:O,'Raw Data'!A:A,A{currentRow})";  // Total SAP Time I
                ws.Cell(currentRow, 10).FormulaA1 = $"=SUMIFS('Raw Data'!P:P,'Raw Data'!A:A,A{currentRow})"; // Total Act Time J

                // OEE (N)
                ws.Cell(currentRow, 11).FormulaA1 = $"=IF(OR(K{currentRow}=0,L{currentRow}=0,M{currentRow}=0),0,K{currentRow}/100*L{currentRow}/100*M{currentRow}/100*100)";
                // Performance (L) = Total SAP Time / Total Act Time × 100
                ws.Cell(currentRow, 12).FormulaA1 = $"=IF(J{currentRow}=0,0,I{currentRow}/J{currentRow}*100)";
                // Availability (K) = Run Time / Operating Hours × 100
                ws.Cell(currentRow, 13).FormulaA1 = $"=IF(G{currentRow}>0,B{currentRow}/G{currentRow}*100,0)";
                // Quality (M) = (Material Used - Reject) / Material Used × 100
                ws.Cell(currentRow, 14).FormulaA1 = $"=IF(E{currentRow}=0,0,MAX(0,(E{currentRow}-F{currentRow})/E{currentRow}*100))";

                for (int c = 2; c <= totalCols; c++)
                    ws.Cell(currentRow, c).Style.NumberFormat.Format = "0.00";

                ws.Cell(currentRow, 11).Style.Font.Bold = true;

                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(currentRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
            }

            int lastDataRow = Math.Max(HEADER_ROW + 1, currentRow - 1);

            // ── Totals / Averages ──────────────────────────────────────────────────────
            int totalRow = currentRow + 1;
            ws.Cell(totalRow, 1).Value = "TOTAL / AVG";

            foreach (int c in new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 })
            {
                string letter = COL_LETTERS[c - 1];
                ws.Cell(totalRow, c).FormulaA1 = $"=SUM({letter}4:{letter}{lastDataRow})";
            }

            ws.Cell(totalRow, 11).FormulaA1 = $"=IF(OR(K{totalRow}=0,L{totalRow}=0,M{totalRow}=0),0,K{totalRow}/100*L{totalRow}/100*M{totalRow}/100*100)";
            ws.Cell(totalRow, 12).FormulaA1 = $"=IF(J{totalRow}=0,0,I{totalRow}/J{totalRow}*100)";
            ws.Cell(totalRow, 13).FormulaA1 = $"=IF(G{totalRow}>0,B{totalRow}/G{totalRow}*100,0)";
            ws.Cell(totalRow, 14).FormulaA1 = $"=IF(E{totalRow}=0,0,MAX(0,(E{totalRow}-F{totalRow})/E{totalRow}*100))";

            for (int c = 1; c <= totalCols; c++)
            {
                ws.Cell(totalRow, c).Style.NumberFormat.Format = "0.00";
                ApplyTotalStyle(ws.Cell(totalRow, c));
            }

            // ── Target row ─────────────────────────────────────────────────────────────
            int targetRow = totalRow + 1;
            ws.Cell(targetRow, 1).Value = "TARGET";

            foreach (var (col, val) in new (int, double)[] { (11, 70.0), (12, 95.0), (13, 97.0), (14, 65.0) })
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
            ws.Column(13).Width = 12;
            ws.Column(14).Width = 12;

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
        int id_machine,
        string machine_name,
        int id_type,
        int mould,
        string type,
        double shot,
        double run_time,
        double unplanned_dt,
        double planned_dt,
        double material_used,
        double reject_weight,
        double sap_ct,
        double act_ct,
        double total_sap_time,
        double total_actual_time
    );
}