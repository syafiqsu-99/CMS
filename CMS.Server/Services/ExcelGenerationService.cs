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
    }
}