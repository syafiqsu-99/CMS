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
                worksheet.Cell(currentRow, 1).Value = item.MachineName;
                worksheet.Cell(currentRow, 2).Value = item.Shift;
                worksheet.Cell(currentRow, 3).Value = item.Packer;
                worksheet.Cell(currentRow, 4).Value = item.Material;
                worksheet.Cell(currentRow, 5).Value = item.IdType;
                worksheet.Cell(currentRow, 6).Value = item.Mould;
                worksheet.Cell(currentRow, 7).Value = item.Type;
                worksheet.Cell(currentRow, 8).Value = item.JoNo;
                worksheet.Cell(currentRow, 9).Value = item.QtyPerct;
                worksheet.Cell(currentRow, 10).Value = item.GrossWeight;
                worksheet.Cell(currentRow, 11).Value = item.PartWeight;
                worksheet.Cell(currentRow, 12).Value = item.ShotAccum;
                worksheet.Cell(currentRow, 13).Value = item.QtyOrder;
                worksheet.Cell(currentRow, 14).Value = item.WipOpening;
                worksheet.Cell(currentRow, 15).Value = item.WipClosing;
                worksheet.Cell(currentRow, 17).Value = item.FinishGood;
                worksheet.Cell(currentRow, 19).Value = item.QtyAccum;
                worksheet.Cell(currentRow, 23).Value = item.RejectStartup;
                worksheet.Cell(currentRow, 25).Value = item.RejectProd;
                worksheet.Cell(currentRow, 27).Value = item.ActCt;
                worksheet.Cell(currentRow, 28).Value = item.ProductionRunning;
                worksheet.Cell(currentRow, 29).Value = item.SapCt;

                worksheet.Cell(currentRow, 30).Value = item.ChangeFullSet;
                worksheet.Cell(currentRow, 31).Value = item.ChangeHalfSet;
                worksheet.Cell(currentRow, 32).Value = item.ChangeParts;
                worksheet.Cell(currentRow, 33).Value = item.MaintenanceDt;
                worksheet.Cell(currentRow, 34).Value = item.TechnicianDt;
                worksheet.Cell(currentRow, 35).Value = item.ProductionDt;
                worksheet.Cell(currentRow, 36).Value = item.Remark;
                worksheet.Cell(currentRow, 37).Value = item.PartScrap;
                worksheet.Cell(currentRow, 38).Value = item.RejectPurging;
                worksheet.Cell(currentRow, 39).Value = item.RejectPreform;

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
            "Machine Name",          // 1 (A)
            "Id Type",               // 2 (B)
            "Mould",                 // 3 (C)
            "Type",                  // 4 (D)
            "Shot",                  // 5 (E)
            "Run Time (hrs)",        // 6 (F)
            "Down Time (hrs)",       // 7 (G)
            "Unallocated (hrs)",     // 8 (H)
            "Material Used (kg)",    // 9 (I)
            "Reject Weight (kg)",    // 10 (J)
            "SAP CT (s)",            // 11 (K)
            "Act CT (s)",            // 12 (L)
            "Total SAP Time (hrs)",  // 13 (M)
            "Total Act Time (hrs)",  // 14 (N)
        ];

        private static readonly string[] SUMMARY_COLUMNS =
        [
            "Machine Name",          // 1 (A)
            "Run Time (hrs)",        // 2 (B)
            "Down Time (hrs)",       // 3 (C)
            "Unallocated (hrs)",     // 4 (D)
            "Material Used (kg)",    // 5 (E)
            "Reject Weight (kg)",    // 6 (F)
            "Available Hours (hrs)", // 7 (G)
            "Total SAP Time (hrs)",  // 8 (H)
            "Total Act Time (hrs)",  // 9 (I)
            "Availability (%)",      // 10 (J)
            "Performance (%)",       // 11 (K)
            "Quality (%)",           // 12 (L)
            "OEE (%)",               // 13 (M)
        ];

        private static readonly Dictionary<int, string> SUMMARY_HEADER_NOTES = new()
        {
            [7] = "Exclude public holiday and off day",
            [8] = "Total SAP Time (hrs)\n= (SAP CT * Shot) / 3600",
            [9] = "Total Act Time (hrs)\n= (Act CT * Shot) / 3600",
            [10] = "Availability (%)\n= Run Time / Available Hours × 100",
            [11] = "Performance (%)\n= Total SAP Time / Total Act Time × 100",
            [12] = "Quality (%)\n= (Material Used − Reject Weight) / Material Used × 100",
            [13] = "OEE (%)\n= Availability% × Performance% × Quality% / 10000",
        };

        private static readonly string[] COL_LETTERS = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q" };

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

            // Title
            var title = ws.Cell(TITLE_ROW, 1);
            title.Value = $"Raw Data  |  {startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}";
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            ws.Range(TITLE_ROW, 1, TITLE_ROW, 14).Merge();

            // Headers
            for (int c = 1; c <= 14; c++)
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

            // Raw Data
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
                ws.Cell(currentRow, 8).Value = r.Unallocated;
                ws.Cell(currentRow, 9).Value = r.MaterialUsed;
                ws.Cell(currentRow, 10).Value = r.RejectWeight;
                ws.Cell(currentRow, 11).Value = r.SapCt;
                ws.Cell(currentRow, 12).Value = r.ActCt;

                // Total SAP Time (hrs) (M) = (Shot (E) * SAP CT (K)) / 3600
                ws.Cell(currentRow, 13).FormulaA1 = $"=(E{currentRow}*K{currentRow})/3600";
                // Total Act Time (hrs) (N) = (Shot (E) * Act CT (L)) / 3600
                ws.Cell(currentRow, 14).FormulaA1 = $"=(E{currentRow}*L{currentRow})/3600";

                // Formatting
                for (int c = 5; c <= 14; c++)
                {
                    ws.Cell(currentRow, c).Style.NumberFormat.Format = "0.00";
                }
                for (int c = 1; c <= 14; c++)
                {
                    ws.Cell(currentRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                currentRow++;
            }

            ws.Columns(1, 14).AdjustToContents();
            ws.SheetView.FreezeRows(HEADER_ROW);
            ws.SheetView.FreezeColumns(1);
        }

        private static void BuildOEESummarySheet(IXLWorksheet ws, List<OEERawRow> rows, DateOnly startDate, DateOnly endDate)
        {
            const int TITLE_ROW = 1;
            const int HEADER_ROW = 3;

            // Title
            var title = ws.Cell(TITLE_ROW, 1);
            title.Value = $"OEE Summary  |  {startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}";
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            ws.Range(TITLE_ROW, 1, TITLE_ROW, 13).Merge();

            // Headers
            for (int c = 1; c <= 13; c++)
            {
                var cell = ws.Cell(HEADER_ROW, c);
                cell.Value = SUMMARY_COLUMNS[c - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;

                cell.Style.Fill.BackgroundColor = c switch
                {
                    13 => XLColor.FromHtml("#C00000"), // OEE
                    >= 8 and <= 13 => XLColor.FromHtml("#ED7D31"), // Formulas
                    _ => XLColor.FromHtml("#4472C4"), // Data
                };
            }
            ws.Row(HEADER_ROW).Height = 42;

            // Header Comments
            foreach (var (colIndex, noteText) in SUMMARY_HEADER_NOTES)
            {
                var comment = ws.Cell(HEADER_ROW, colIndex).CreateComment();
                comment.AddText(noteText);
                comment.Style.Size.SetWidth(30);
                comment.Style.Size.SetHeight(90);
                comment.Style.Alignment.SetAutomaticSize(false);
            }

            // Get distinct machines from raw data
            var machines = rows.GroupBy(r => new { r.IdMachine, r.MachineName })
                               .Select(g => new { g.Key.IdMachine, g.Key.MachineName, AvailableHours = g.Max(x => x.AvailableHours) })
                               .OrderBy(g => g.IdMachine)
                               .ToList();

            int currentRow = HEADER_ROW + 1;
            foreach (var machine in machines)
            {
                ws.Cell(currentRow, 1).Value = machine.MachineName;

                // Aggregations using SUMIFS referencing the "Raw Data" sheet
                ws.Cell(currentRow, 2).FormulaA1 = $"=SUMIFS('Raw Data'!F:F, 'Raw Data'!A:A, A{currentRow})"; // Run Time
                ws.Cell(currentRow, 3).FormulaA1 = $"=SUMIFS('Raw Data'!G:G, 'Raw Data'!A:A, A{currentRow})"; // Down Time
                ws.Cell(currentRow, 4).FormulaA1 = $"=SUMIFS('Raw Data'!H:H, 'Raw Data'!A:A, A{currentRow})"; // Unallocated
                ws.Cell(currentRow, 5).FormulaA1 = $"=SUMIFS('Raw Data'!I:I, 'Raw Data'!A:A, A{currentRow})"; // Material Used
                ws.Cell(currentRow, 6).FormulaA1 = $"=SUMIFS('Raw Data'!J:J, 'Raw Data'!A:A, A{currentRow})"; // Reject

                ws.Cell(currentRow, 7).Value = machine.AvailableHours; // Available Hours

                // Accurate Total Time Calculations via SUMIFS (H & I pull from Raw's M & N)
                ws.Cell(currentRow, 8).FormulaA1 = $"=SUMIFS('Raw Data'!M:M, 'Raw Data'!A:A, A{currentRow})"; // Total SAP Time (hrs)
                ws.Cell(currentRow, 9).FormulaA1 = $"=SUMIFS('Raw Data'!N:N, 'Raw Data'!A:A, A{currentRow})"; // Total Act Time (hrs)

                // OEE Formulations
                // Availability (J)
                ws.Cell(currentRow, 10).FormulaA1 = $"=IF(G{currentRow}>0, B{currentRow}/G{currentRow}*100, 0)";
                // Performance (K)
                ws.Cell(currentRow, 11).FormulaA1 = $"=IF(I{currentRow}=0, 0, H{currentRow}/I{currentRow}*100)";
                // Quality (L)
                ws.Cell(currentRow, 12).FormulaA1 = $"=IF(E{currentRow}=0, 0, MAX(0, (E{currentRow}-F{currentRow})/E{currentRow}*100))";
                // OEE (M)
                ws.Cell(currentRow, 13).FormulaA1 = $"=IF(OR(J{currentRow}=0,K{currentRow}=0,L{currentRow}=0), 0, J{currentRow}/100*K{currentRow}/100*L{currentRow}/100*100)";

                // Format row
                for (int c = 2; c <= 13; c++) ws.Cell(currentRow, c).Style.NumberFormat.Format = "0.00";
                ws.Cell(currentRow, 13).Style.Font.Bold = true;
                for (int c = 1; c <= 13; c++) ws.Cell(currentRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
            }

            int lastDataRow = Math.Max(HEADER_ROW + 1, currentRow - 1);

            // ── Totals / Averages ──────────────────────────────────────────────────
            int totalRow = currentRow + 1;
            ws.Cell(totalRow, 1).Value = "TOTAL / AVG";

            int[] sumCols = [2, 3, 4, 5, 6, 7, 8, 9]; // B to I
            foreach (int c in sumCols)
            {
                string letter = COL_LETTERS[c - 1];
                ws.Cell(totalRow, c).FormulaA1 = $"=SUM({letter}4:{letter}{lastDataRow})";
            }

            int[] avgCols = [10, 11, 12, 13]; // J to M
            foreach (int c in avgCols)
            {
                string letter = COL_LETTERS[c - 1];
                ws.Cell(totalRow, c).FormulaA1 = $"=AVERAGE({letter}4:{letter}{lastDataRow})";
            }

            for (int c = 1; c <= 13; c++)
            {
                ws.Cell(totalRow, c).Style.NumberFormat.Format = "0.00";
                ApplyTotalStyle(ws.Cell(totalRow, c));
            }

            // ── Target Row ─────────────────────────────────────────────────────────
            int targetRow = totalRow + 1;
            ws.Cell(targetRow, 1).Value = "TARGET";

            foreach (var (col, val) in new (int, double)[] { (10, 70.0), (11, 95.0), (12, 97.0), (13, 65.0) })
            {
                ws.Cell(targetRow, col).Value = val;
            }

            for (int c = 1; c <= 13; c++)
            {
                var cell = ws.Cell(targetRow, c);
                cell.Style.NumberFormat.Format = "0.00";
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC000");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ── Styling & Widths ───────────────────────────────────────────────────
            ws.Column(1).Width = 20;  // Machine
            ws.Column(2).Width = 14;  // Run Time
            ws.Column(3).Width = 14;  // Down Time
            ws.Column(4).Width = 14;  // Unallocated
            ws.Column(5).Width = 16;  // Material
            ws.Column(6).Width = 14;  // Reject
            ws.Column(7).Width = 16;  // Avail Hrs
            ws.Column(8).Width = 16;  // Total SAP (hrs)
            ws.Column(9).Width = 16;  // Total Act (hrs)
            ws.Column(10).Width = 15; // Availability
            ws.Column(11).Width = 14; // Performance
            ws.Column(12).Width = 12; // Quality
            ws.Column(13).Width = 12; // OEE

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
        double Unallocated,
        double MaterialUsed,
        double RejectWeight,
        double AvailableHours,
        double SapCt,
        double ActCt
    );
}