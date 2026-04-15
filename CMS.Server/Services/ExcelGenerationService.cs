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

        // ══════════════════════════════════════════════════════════════════════════════
        // OEE EXCEL EXPORT
        // ══════════════════════════════════════════════════════════════════════════════

        private const int C_MACHINE = 1;
        private const int C_RUN_TIME = 2;
        private const int C_DOWN_TIME = 3;
        private const int C_UNALLOCATED = 4;
        private const int C_MATERIAL = 5;
        private const int C_REJECT = 6;
        private const int C_AVAIL_HRS = 7;
        private const int C_AVG_SAP_CT = 8;
        private const int C_AVG_ACT_CT = 9;
        // ↓ formula columns start here
        private const int C_TOTAL_SAP = 10;
        private const int C_TOTAL_ACT = 11;
        private const int C_AVAILABILITY = 12;
        private const int C_PERFORMANCE = 13;
        private const int C_QUALITY = 14;
        private const int C_OEE = 15;
        private const int OEE_COL_COUNT = 15;

        private static readonly string[] OEE_COLUMNS =
        [
            "Machine Name",          // 1
            "Run Time (hrs)",        // 2
            "Down Time (hrs)",       // 3
            "Unallocated (hrs)",     // 4
            "Material Used (kg)",    // 5
            "Reject Weight (kg)",    // 6
            "Available Hours (hrs)", // 7
            "Avg SAP CT (s)",        // 8
            "Avg Act CT (s)",        // 9
            "Total SAP Time (s)",    // 10
            "Total Act Time (s)",    // 11
            "Availability (%)",      // 12
            "Performance (%)",       // 13
            "Quality (%)",           // 14
            "OEE (%)",               // 15
        ];

        private static readonly Dictionary<int, string> OEE_HEADER_NOTES = new()
        {
            [C_TOTAL_SAP] = "Total SAP Time (s)\n= Material Used (kg) × Avg SAP CT (s)",
            [C_TOTAL_ACT] = "Total Act Time (s)\n= Material Used (kg) × Avg Act CT (s)",
            [C_AVAILABILITY] = "Availability (%)\n" +
                               "If Available Hours > 0:\n" +
                               "  = Run Time / Available Hours × 100\n" +
                               "Else (live / same-day query):\n" +
                               "  = Run Time / (Run Time + Down Time) × 100",
            [C_PERFORMANCE] = "Performance (%)\n= Total SAP Time / Total Act Time × 100",
            [C_QUALITY] = "Quality (%)\n= MAX(0, (Material Used − Reject Weight) / Material Used × 100)",
            [C_OEE] = "OEE (%)\n= Availability% × Performance% × Quality% / 10000",
        };

        public byte[] GenerateOEEReport(IEnumerable<OEERawRow> rows, DateOnly startDate, DateOnly endDate)
        {
            using var workbook = new XLWorkbook();
            BuildOEESheet(workbook.Worksheets.Add("OEE Report"), rows.ToList(), startDate, endDate);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void BuildOEESheet(IXLWorksheet ws, List<OEERawRow> rows, DateOnly startDate, DateOnly endDate)
        {
            const int TITLE_ROW = 1;
            const int HEADER_ROW = 3;

            // Safeguard: Ensure the table always has at least 1 empty row if data is empty to prevent range crashing
            int dataRowCount = Math.Max(1, rows.Count);
            int firstDataRow = HEADER_ROW + 1;
            int lastDataRow = firstDataRow + dataRowCount - 1;

            // ── Title ─────────────────────────────────────────────────────────────────
            var title = ws.Cell(TITLE_ROW, 1);
            title.Value = $"OEE Report  |  {startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}";
            title.Style.Font.Bold = true;
            title.Style.Font.FontSize = 14;
            ws.Range(TITLE_ROW, 1, TITLE_ROW, OEE_COL_COUNT).Merge();

            // ── Column headers ────────────────────────────────────────────────────────
            for (int c = 1; c <= OEE_COL_COUNT; c++)
            {
                var cell = ws.Cell(HEADER_ROW, c);
                cell.Value = OEE_COLUMNS[c - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.WrapText = true;

                cell.Style.Fill.BackgroundColor = c switch
                {
                    C_OEE => XLColor.FromHtml("#C00000"),
                    >= C_TOTAL_SAP and <= C_OEE => XLColor.FromHtml("#ED7D31"),
                    _ => XLColor.FromHtml("#4472C4"),
                };
            }
            ws.Row(HEADER_ROW).Height = 42;

            // ── Write raw data ────────────────────────────────────────────────────────
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int excelRow = firstDataRow + i;

                ws.Cell(excelRow, C_MACHINE).Value = r.MachineName;
                ws.Cell(excelRow, C_RUN_TIME).Value = r.RunTime;
                ws.Cell(excelRow, C_DOWN_TIME).Value = r.DownTime;
                ws.Cell(excelRow, C_UNALLOCATED).Value = r.Unallocated;
                ws.Cell(excelRow, C_MATERIAL).Value = r.MaterialUsed;
                ws.Cell(excelRow, C_REJECT).Value = r.RejectWeight;
                ws.Cell(excelRow, C_AVAIL_HRS).Value = r.AvailableHours;
                ws.Cell(excelRow, C_AVG_SAP_CT).Value = r.AvgSapCt;
                ws.Cell(excelRow, C_AVG_ACT_CT).Value = r.AvgActCt;
            }

            // ── Create Excel Table ────────────────────────────────────────────────────
            var tableRange = ws.Range(HEADER_ROW, 1, lastDataRow, OEE_COL_COUNT);
            var table = tableRange.CreateTable("OEEData");
            table.Theme = XLTableTheme.TableStyleMedium2;
            table.ShowTotalsRow = false;

            // ── Build structured-reference formula strings ────────────────────────────
            static string S(string colName) => $"[@[{colName}]]";

            string runTime = S(OEE_COLUMNS[C_RUN_TIME - 1]);
            string downTime = S(OEE_COLUMNS[C_DOWN_TIME - 1]);
            string material = S(OEE_COLUMNS[C_MATERIAL - 1]);
            string reject = S(OEE_COLUMNS[C_REJECT - 1]);
            string availHrs = S(OEE_COLUMNS[C_AVAIL_HRS - 1]);
            string avgSapCt = S(OEE_COLUMNS[C_AVG_SAP_CT - 1]);
            string avgActCt = S(OEE_COLUMNS[C_AVG_ACT_CT - 1]);
            string totalSap = S(OEE_COLUMNS[C_TOTAL_SAP - 1]);
            string totalAct = S(OEE_COLUMNS[C_TOTAL_ACT - 1]);
            string availability = S(OEE_COLUMNS[C_AVAILABILITY - 1]);
            string performance = S(OEE_COLUMNS[C_PERFORMANCE - 1]);
            string quality = S(OEE_COLUMNS[C_QUALITY - 1]);

            string fTotalSap = $"={material}*{avgSapCt}";
            string fTotalAct = $"={material}*{avgActCt}";
            string fAvailability = $"=IF({availHrs}>0, {runTime}/{availHrs}*100, IF(({runTime}+{downTime})=0, 0, {runTime}/({runTime}+{downTime})*100))";
            string fPerformance = $"=IF({totalAct}=0, 0, {totalSap}/{totalAct}*100)";
            string fQuality = $"=IF({material}=0, 0, MAX(0, ({material}-{reject})/{material}*100))";
            string fOee = $"=IF(OR({availability}=0,{performance}=0,{quality}=0), 0, {availability}/100*{performance}/100*{quality}/100*100)";

            // ── Write formulas securely to the data ranges ────────────────────────────
            ws.Range(firstDataRow, C_TOTAL_SAP, lastDataRow, C_TOTAL_SAP).FormulaA1 = fTotalSap;
            ws.Range(firstDataRow, C_TOTAL_ACT, lastDataRow, C_TOTAL_ACT).FormulaA1 = fTotalAct;
            ws.Range(firstDataRow, C_AVAILABILITY, lastDataRow, C_AVAILABILITY).FormulaA1 = fAvailability;
            ws.Range(firstDataRow, C_PERFORMANCE, lastDataRow, C_PERFORMANCE).FormulaA1 = fPerformance;
            ws.Range(firstDataRow, C_QUALITY, lastDataRow, C_QUALITY).FormulaA1 = fQuality;
            ws.Range(firstDataRow, C_OEE, lastDataRow, C_OEE).FormulaA1 = fOee;

            // ── Number formats ────────────────────────────────────────────────────────
            string fmt2dp = "0.00";
            int[] numCols = [
                C_RUN_TIME, C_DOWN_TIME, C_UNALLOCATED, C_MATERIAL, C_REJECT,
                C_AVAIL_HRS, C_AVG_SAP_CT, C_AVG_ACT_CT, C_TOTAL_SAP, C_TOTAL_ACT,
                C_AVAILABILITY, C_PERFORMANCE, C_QUALITY, C_OEE
            ];

            foreach (int c in numCols)
            {
                ws.Range(firstDataRow, c, lastDataRow, c).Style.NumberFormat.Format = fmt2dp;
            }
            ws.Range(firstDataRow, C_OEE, lastDataRow, C_OEE).Style.Font.Bold = true;

            // ── Header comments for formula columns ───────────────────────────────────
            foreach (var (colIndex, noteText) in OEE_HEADER_NOTES)
            {
                var comment = ws.Cell(HEADER_ROW, colIndex).CreateComment();
                comment.AddText(noteText);
                comment.Style.Size.SetWidth(30);
                comment.Style.Size.SetHeight(90);
                comment.Style.Alignment.SetAutomaticSize(false);
            }

            // ── Totals / averages row ─────────────────────────────────────────────────
            int totalRow = lastDataRow + 2;

            ws.Cell(totalRow, C_MACHINE).Value = "TOTAL / AVG";
            ApplyTotalStyle(ws.Cell(totalRow, C_MACHINE));

            int[] totalColumns = [ C_RUN_TIME, C_DOWN_TIME, C_UNALLOCATED, C_MATERIAL, C_REJECT,
                                   C_AVAIL_HRS, C_AVG_SAP_CT, C_AVG_ACT_CT, C_TOTAL_SAP, C_TOTAL_ACT ];

            foreach (int c in totalColumns)
            {
                string colName = OEE_COLUMNS[c - 1];
                var cell = ws.Cell(totalRow, c);
                // FIX: Double Brackets [[ ]] are REQUIRED by Excel when referencing a column that has spaces from outside the table.
                cell.FormulaA1 = c is C_AVG_SAP_CT or C_AVG_ACT_CT
                    ? $"=AVERAGE(OEEData[[{colName}]])"
                    : $"=SUM(OEEData[[{colName}]])";
                cell.Style.NumberFormat.Format = "0.00";
                ApplyTotalStyle(cell);
            }

            int[] averageColumns = [C_AVAILABILITY, C_PERFORMANCE, C_QUALITY, C_OEE];
            foreach (int c in averageColumns)
            {
                string colName = OEE_COLUMNS[c - 1];
                var cell = ws.Cell(totalRow, c);
                // FIX: Double Brackets [[ ]]
                cell.FormulaA1 = $"=AVERAGE(OEEData[[{colName}]])";
                cell.Style.NumberFormat.Format = "0.00";
                ApplyTotalStyle(cell);
            }

            // ── Target row ────────────────────────────────────────────────────────────
            int targetRow = totalRow + 1;
            var targetLabel = ws.Cell(targetRow, C_MACHINE);
            targetLabel.Value = "TARGET";
            targetLabel.Style.Font.Bold = true;
            targetLabel.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC000");

            foreach (var (col, val) in new (int, double)[]
                { (C_AVAILABILITY, 70.0), (C_PERFORMANCE, 95.0), (C_QUALITY, 97.0), (C_OEE, 65.0) })
            {
                var cell = ws.Cell(targetRow, col);
                cell.Value = val;
                cell.Style.NumberFormat.Format = "0.00";
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC000");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // ── Column widths ─────────────────────────────────────────────────────────
            ws.Column(C_MACHINE).Width = 20;
            ws.Column(C_RUN_TIME).Width = 14;
            ws.Column(C_DOWN_TIME).Width = 14;
            ws.Column(C_UNALLOCATED).Width = 14;
            ws.Column(C_MATERIAL).Width = 16;
            ws.Column(C_REJECT).Width = 14;
            ws.Column(C_AVAIL_HRS).Width = 16;
            ws.Column(C_AVG_SAP_CT).Width = 14;
            ws.Column(C_AVG_ACT_CT).Width = 14;
            ws.Column(C_TOTAL_SAP).Width = 16;
            ws.Column(C_TOTAL_ACT).Width = 16;
            ws.Column(C_AVAILABILITY).Width = 15;
            ws.Column(C_PERFORMANCE).Width = 14;
            ws.Column(C_QUALITY).Width = 12;
            ws.Column(C_OEE).Width = 12;

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
        double RunTime,
        double DownTime,
        double Unallocated,
        double MaterialUsed,
        double RejectWeight,
        double AvailableHours,
        double AvgSapCt,
        double AvgActCt
    );
}