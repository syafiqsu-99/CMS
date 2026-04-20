using CMS.Server.Models;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupervisorController(
    SupervisorService supervisorService,
    ExcelGenerationService excelService) : ControllerBase
{
    // ── Production Reports ────────────────────────────────────────────────────

    [HttpGet("daily-report")]
    public async Task<IActionResult> DailyReport(
        [FromQuery] DateOnly production_date,
        [FromQuery] int shift)
        => Ok(await supervisorService.LoadDailyReport(production_date, shift));

    [HttpPut("daily-report")]
    public async Task<IActionResult> UpdateDailyReport([FromBody] ReportUpdateDto dto)
    {
        if (dto.ReportList == null || dto.ReportList.Count == 0)
            return BadRequest(new { error = "No report rows provided." });

        await supervisorService.UpsertDailyReport(dto.ProductionDate, dto.Shift, dto.ReportList);
        return Ok(new { message = "Report saved." });
    }

    [HttpGet("prev-report")]
    public async Task<IActionResult> PrevReport(
        [FromQuery] DateOnly production_date,
        [FromQuery] int shift)
        => Ok(await supervisorService.LoadPrevReport(production_date, shift));

    [HttpPut("prev-report")]
    public async Task<IActionResult> UpdatePrevReport([FromBody] ReportUpdateDto dto)
    {
        if (dto.ReportList == null || dto.ReportList.Count == 0)
            return BadRequest(new { error = "No report rows provided." });

        await supervisorService.UpsertPrevReport(dto.ProductionDate, dto.Shift, dto.ReportList);
        return Ok(new { message = "Previous report saved." });
    }

    [HttpGet("export-report")]
    public async Task<IActionResult> ExportReport(string production_date, int shift)
    {
        try
        {
            if (string.IsNullOrEmpty(production_date))
            {
                return BadRequest(new { message = "Production date is required" });
            }

            if (!DateOnly.TryParse(production_date, out var parsedDate))
            {
                return BadRequest(new { message = $"Invalid date format: {production_date}. Use YYYY-MM-DD format." });
            }

            if (shift != 1 && shift != 2)
            {
                return BadRequest(new { message = "Invalid shift. Use 1 or 2" });
            }

            Console.WriteLine($"Querying for date: {parsedDate}, shift: {shift}");

            var reportData = await supervisorService.LoadExcelReport(parsedDate, shift);

            Console.WriteLine($"Records found: {reportData?.Count ?? 0}");

            if (reportData == null || !reportData.Any())
            {
                return NotFound(new
                {
                    message = "No data found for the specified date and shift",
                    requestedDate = parsedDate.ToString("yyyy-MM-dd"),
                    requestedShift = shift
                });
            }

            var excelFile = excelService.GenerateExcelReport(reportData, parsedDate, shift);

            var shiftName = shift == 1 ? "Morning" : "Night";
            var fileName = $"DailyProductionReport_{parsedDate:yyyy-MM-dd}_Shift{shiftName}.xlsx";

            return File(
                excelFile,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ExportReport: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpPost("import-report")]
    public async Task<ActionResult> ImportReport([FromBody] JsonElement payload)
    {
        try
        {
            if (!payload.TryGetProperty("reportList", out var reportListEl) ||
                reportListEl.ValueKind != JsonValueKind.Array)
            {
                return BadRequest(new { message = "Missing or invalid 'reportList' in request body." });
            }

            var reportList = reportListEl.Deserialize<List<Dictionary<string, JsonElement>>>();
            if (reportList == null || reportList.Count == 0)
            {
                return BadRequest(new { message = "reportList is empty." });
            }

            foreach (var row in reportList)
            {
                if (!row.ContainsKey("production_date") || !row.ContainsKey("shift"))
                {
                    return BadRequest(new { message = "Each row must contain 'production_date' and 'shift' columns." });
                }

                if (!DateOnly.TryParse(row["production_date"].GetString(), out _))
                {
                    return BadRequest(new { message = $"Invalid production_date value: {row["production_date"].GetString()}. Use YYYY-MM-DD format." });
                }
            }

            var data = await supervisorService.ImportReport(reportList, DateOnly.MinValue, 0);
            return Ok(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ImportReport: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    // ── Machine Management ────────────────────────────────────────────────────────────

    [HttpPost("mould-change")]
    public async Task<IActionResult> MouldChange([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            await supervisorService.UpdateMouldChange(payload);
            return Ok(new { message = "Mould changed successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Attendance ────────────────────────────────────────────────────────────

    [HttpGet("attendance")]
    public async Task<IActionResult> Attendance()
        => Ok(await supervisorService.LoadAttendance());

    // ── Staff Schedule ────────────────────────────────────────────────────────

    [HttpGet("staff-schedule")]
    public async Task<IActionResult> GetStaffSchedule()
        => Ok(await supervisorService.LoadStaffSchedule());

    [HttpPut("staff-schedule")]
    public async Task<IActionResult> UpdateStaffSchedule(
        [FromBody] List<Dictionary<string, JsonElement>> payload)
    {
        if (payload == null || payload.Count == 0)
            return BadRequest(new { error = "No schedule data provided." });

        await supervisorService.UpsertStaffSchedule(payload);
        return Ok(new { message = "Schedule saved." });
    }

    // ── Staff CRUD ────────────────────────────────────────────────────────────

    [HttpPost("staff")]
    public async Task<IActionResult> AddStaff([FromBody] StaffDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StaffName))
            return BadRequest(new { error = "Staff name is required." });

        var newId = await supervisorService.AddStaff(dto.StaffName, dto.StaffRole ?? string.Empty);
        return Ok(new { staff_id = newId });
    }

    [HttpPut("staff")]
    public async Task<IActionResult> UpdateStaff([FromBody] StaffDto dto)
    {
        if (dto.StaffId == null)
            return BadRequest(new { error = "staff_id is required for update." });

        await supervisorService.UpdateStaff(dto.StaffId.Value, dto.StaffName ?? string.Empty, dto.StaffRole ?? string.Empty);
        return Ok(new { message = "Staff updated." });
    }

    [HttpDelete("staff/{id:int}")]
    public async Task<IActionResult> DeleteStaff(int id)
    {
        await supervisorService.DeleteStaff(id);
        return Ok(new { message = "Staff deleted." });
    }

    // ── Staff Photo ───────────────────────────────────────────────────────────

    [HttpGet("staff-photo/{staffId:int}")]
    public IActionResult GetStaffPhoto(int staffId)
    {
        var path = supervisorService.GetStaffPhotoPath(staffId);
        if (string.IsNullOrEmpty(path))
            return NotFound();

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime);
    }

    [HttpPost("staff-photo")]
    public async Task<IActionResult> UploadStaffPhoto([FromForm] int staff_id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        try
        {
            await supervisorService.SaveStaffPhoto(staff_id, file);
            return Ok(new { message = "Photo saved." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Shift Calendar ────────────────────────────────────────────────────────

    [HttpGet("shift-calendar")]
    public async Task<IActionResult> GetShiftCalendar(
        [FromQuery] int year,
        [FromQuery] int month)
        => Ok(await supervisorService.LoadShiftCalendar(year, month));

    [HttpPut("shift-calendar")]
    public async Task<IActionResult> UpdateShiftCalendar(List<Calendar> entries)
    {
        if (entries == null || entries.Count == 0)
            return BadRequest(new { error = "No entries provided." });

        await supervisorService.UpsertShiftCalendar(entries);
        return Ok(new { message = $"Saved {entries.Count} calendar entries." });
    }
}