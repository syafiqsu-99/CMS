using CMS.Server.Models;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupervisorController : ControllerBase
{
    private readonly ExcelGenerationService _excelService;
    private readonly SupervisorService _supervisorService;

    public SupervisorController(SupervisorService supervisorService, ExcelGenerationService excelService)
    {
        _supervisorService = supervisorService;
        _excelService = excelService;
    }

    [HttpGet("daily-report")]
    public async Task<IActionResult> DailyReport([FromQuery] DateOnly production_date, [FromQuery] int shift)
        => Ok(await _supervisorService.LoadDailyReport(production_date, shift));

    [HttpPut("daily-report")]
    public async Task<IActionResult> UpdateDailyReport([FromBody] ReportUpdateDto dto)
    {
        if (dto.reportList is null || dto.reportList.Count == 0)
            return BadRequest(new { error = "No report rows provided." });

        await _supervisorService.UpsertDailyReport(dto.production_date, dto.shift, dto.reportList);
        return Ok(new { message = "Report saved." });
    }

    [HttpGet("prev-report")]
    public async Task<IActionResult> PrevReport([FromQuery] DateOnly production_date, [FromQuery] int shift)
        => Ok(await _supervisorService.LoadPrevReport(production_date, shift));

    [HttpPut("prev-report")]
    public async Task<IActionResult> UpdatePrevReport([FromBody] ReportUpdateDto dto)
    {
        if (dto.reportList is null || dto.reportList.Count == 0)
            return BadRequest(new { error = "No report rows provided." });

        await _supervisorService.UpsertPrevReport(dto.production_date, dto.shift, dto.reportList);
        return Ok(new { message = "Previous report saved." });
    }

    [HttpGet("export-report")]
    public async Task<IActionResult> ExportReport([FromQuery] string production_date, [FromQuery] int shift)
    {
        if (string.IsNullOrEmpty(production_date))
            return BadRequest(new { message = "Production date is required." });

        if (!DateOnly.TryParse(production_date, out var parsedDate))
            return BadRequest(new { message = $"Invalid date format: {production_date}. Use YYYY-MM-DD." });

        if (shift != 1 && shift != 2)
            return BadRequest(new { message = "Invalid shift. Use 1 or 2." });

        try
        {
            var reportData = await _supervisorService.LoadExcelReport(parsedDate, shift);
            if (reportData is null || reportData.Count == 0)
                return NotFound(new { message = "No data found for the specified date and shift." });

            var excelBytes = _excelService.GenerateExcelReport(reportData, parsedDate, shift);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"report_{parsedDate:yyyy-MM-dd}_shift{shift}.xlsx");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error generating report.", detail = ex.Message });
        }
    }

    [HttpPost("import-report")]
    public async Task<IActionResult> ImportReport([FromBody] List<Dictionary<string, JsonElement>> reportList)
    {
        if (reportList is null || reportList.Count == 0)
            return BadRequest(new { error = "No data provided." });

        foreach (var row in reportList)
        {
            if (!row.ContainsKey("production_date") || !row.ContainsKey("shift"))
                return BadRequest(new { message = "Each row must contain 'production_date' and 'shift'." });

            if (!DateOnly.TryParse(row["production_date"].GetString(), out _))
                return BadRequest(new { message = $"Invalid production_date: {row["production_date"].GetString()}. Use YYYY-MM-DD." });
        }

        try
        {
            var data = await _supervisorService.ImportReport(reportList, DateOnly.MinValue, 0);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("mould-change")]
    public async Task<IActionResult> MouldChange([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            await _supervisorService.UpdateMouldChange(payload);
            return Ok(new { message = "Mould changed successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("staff-schedule")]
    public async Task<IActionResult> GetStaffSchedule()
        => Ok(await _supervisorService.LoadStaffSchedule());

    [HttpPut("staff-schedule")]
    public async Task<IActionResult> UpdateStaffSchedule([FromBody] List<Dictionary<string, JsonElement>> payload)
    {
        if (payload is null || payload.Count == 0)
            return BadRequest(new { error = "No schedule data provided." });

        await _supervisorService.UpsertStaffSchedule(payload);
        return Ok(new { message = "Schedule saved." });
    }

    [HttpPost("staff")]
    public async Task<IActionResult> AddStaff([FromBody] StaffDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.staff_name))
            return BadRequest(new { error = "Staff name is required." });

        if (dto.staff_id <= 0)
            return BadRequest(new { error = "A valid staff_id is required." });

        await _supervisorService.AddStaff(dto.staff_id, dto.staff_name, dto.staff_role ?? string.Empty);
        return Ok(new { staff_id = dto.staff_id });
    }

    [HttpPut("staff")]
    public async Task<IActionResult> UpdateStaff([FromBody] StaffDto dto)
    {
        if (dto.staff_id <= 0)
            return BadRequest(new { error = "A valid staff_id is required for update." });

        await _supervisorService.UpdateStaff(dto.staff_id, dto.staff_name ?? string.Empty, dto.staff_role ?? string.Empty);
        return Ok(new { message = "Staff updated." });
    }

    [HttpDelete("staff/{id:int}")]
    public async Task<IActionResult> DeleteStaff(int id)
    {
        await _supervisorService.DeleteStaff(id);
        return Ok(new { message = "Staff deleted." });
    }

    [HttpGet("staff-photo/{staff_id:int}")]
    public IActionResult GetStaffPhoto(int staff_id)
    {
        var path = _supervisorService.GetStaffPhotoPath(staff_id);
        if (string.IsNullOrEmpty(path)) return NotFound();

        var mime = Path.GetExtension(path).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime);
    }

    [HttpPost("staff-photo")]
    public async Task<IActionResult> UploadStaffPhoto([FromForm] int staff_id, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        try
        {
            await _supervisorService.SaveStaffPhoto(staff_id, file);
            return Ok(new { message = "Photo saved." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("shift-calendar")]
    public async Task<IActionResult> GetShiftCalendar([FromQuery] int year, [FromQuery] int month)
        => Ok(await _supervisorService.LoadShiftCalendar(year, month));

    [HttpPut("shift-calendar")]
    public async Task<IActionResult> UpdateShiftCalendar([FromBody] List<Calendar> entries)
    {
        if (entries is null || entries.Count == 0)
            return BadRequest(new { error = "No entries provided." });

        await _supervisorService.UpsertShiftCalendar(entries);
        return Ok(new { message = $"Saved {entries.Count} calendar entries." });
    }

    [HttpGet("sap")]
    public async Task<IActionResult> SAP()
        => Ok(await _supervisorService.LoadSAP());

    [HttpGet("sap/paged")]
    public async Task<IActionResult> SAPPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null)
    {
        var (items, total) = await _supervisorService.LoadSAPPaged(page, pageSize, search);
        return Ok(new { items, totalCount = total });
    }

    [HttpPut("sap")]
    public async Task<IActionResult> UpdateSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem is null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields." });

        try
        {
            await _supervisorService.UpdateSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("sap")]
    public async Task<IActionResult> DeleteSAP([FromQuery] int id_type, [FromQuery] int mould)
    {
        try
        {
            await _supervisorService.DeleteSAP(id_type, mould);
            return Ok(new { message = "Deleted successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("sap")]
    public async Task<IActionResult> InsertSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem is null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields." });

        try
        {
            await _supervisorService.InsertSAP(sapItem);
            return Ok(new { message = "Successfully inserted SAP." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("sap/import")]
    public async Task<IActionResult> ImportSAP([FromBody] List<Dictionary<string, JsonElement>> sapItems)
    {
        if (sapItems is null || sapItems.Count == 0)
            return BadRequest(new { error = "No data provided." });

        try
        {
            await _supervisorService.ImportSAP(sapItems);
            return Ok(new { message = $"Successfully imported {sapItems.Count} SAP records." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}