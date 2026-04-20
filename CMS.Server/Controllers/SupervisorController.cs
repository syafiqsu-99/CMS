using CMS.Server.Models;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupervisorController(
    SupervisorService supervisorService,
    ExcelGenerationService excelService) : BaseController
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
    public async Task<IActionResult> ExportReport(
        [FromQuery] string production_date,
        [FromQuery] int shift)
    {
        if (string.IsNullOrEmpty(production_date))
            return BadRequest(new { message = "Production date is required." });

        if (!DateOnly.TryParse(production_date, out var parsedDate))
            return BadRequest(new { message = $"Invalid date format: {production_date}. Use YYYY-MM-DD." });

        if (shift != 1 && shift != 2)
            return BadRequest(new { message = "Invalid shift. Use 1 or 2." });

        var reportData = await supervisorService.LoadExcelReport(parsedDate, shift);
        if (reportData.Count == 0)
            return NotFound(new { message = "No data found.", date = parsedDate, shift });

        var excelFile = excelService.GenerateExcelReport(reportData, parsedDate, shift);
        var shiftName = shift == 1 ? "Morning" : "Night";
        var fileName = $"Report_{parsedDate:yyyy-MM-dd}_{shiftName}.xlsx";

        return File(excelFile,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
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
    public async Task<IActionResult> UpdateShiftCalendar([FromBody] List<calendar> entries)
    {
        if (entries == null || entries.Count == 0)
            return BadRequest(new { error = "No entries provided." });

        await supervisorService.UpsertShiftCalendar(entries);
        return Ok(new { message = $"Saved {entries.Count} calendar entries." });
    }
}