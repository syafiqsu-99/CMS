using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingController(SettingService settingService, ReportExportService reportExportService) : ControllerBase
{
    // ── Password ───────────────────────────────────────────────────────────────

    [HttpGet("password")]
    public async Task<IActionResult> GetPasswords()
    {
        var result = await settingService.LoadDepartmentPasswords();
        return Ok(result);
    }

    [HttpPut("password")]
    public async Task<IActionResult> UpdatePasswords([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            var passwords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, element) in payload)
            {
                if (element.TryGetInt32(out int val))
                    passwords[key] = val;
                else
                    return BadRequest(new { error = $"Invalid value for '{key}'. Must be an integer." });
            }

            if (passwords.Count == 0)
                return BadRequest(new { error = "No valid department passwords supplied." });

            var result = await settingService.ChangeDepartmentPasswords(passwords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to write passwords to PLC.", detail = ex.Message });
        }
    }

    // ── PLC Signals ────────────────────────────────────────────────────────────

    [HttpGet("plc-signals")]
    public async Task<IActionResult> GetPlcSignals()
    {
        try
        {
            var result = await settingService.ReadAllSubPlcSignals();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    [HttpGet("plc-signals/{id:int}")]
    public async Task<IActionResult> GetPlcSignalById(int id)
    {
        try
        {
            var result = await settingService.ReadSubPlcSignalById(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    // ── Logs ───────────────────────────────────────────────────────────────────

    [HttpGet("db-log")]
    public async Task<IActionResult> GetLogs([FromQuery] string? process = null, [FromQuery] int? id_machine = null)
    {
        try
        {
            var result = await settingService.GetLogsAsync(process, id_machine);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve logs.", detail = ex.Message });
        }
    }

    // ── Report Auto-Save Config ──────────────────────────────────────────────────

    [HttpGet("report-config")]
    public async Task<IActionResult> GetReportConfig()
    {
        try
        {
            var result = await settingService.GetReportConfigAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load report config.", detail = ex.Message });
        }
    }

    [HttpPut("report-config")]
    public async Task<IActionResult> UpdateReportConfig([FromBody] ReportConfigDto dto)
    {
        try
        {
            var result = await settingService.UpdateReportConfigAsync(dto.folderPath, dto.autoSaveEnabled);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to save report config.", detail = ex.Message });
        }
    }

    [HttpPost("report/run")]
    public async Task<IActionResult> RunReport([FromBody] ReportRunDto dto)
    {
        if (!DateOnly.TryParse(dto.production_date, out var parsedDate))
            return BadRequest(new { error = $"Invalid date format: {dto.production_date}. Use YYYY-MM-DD." });

        if (dto.shift != 1 && dto.shift != 2)
            return BadRequest(new { error = "Invalid shift. Use 1 or 2." });

        var destination = (dto.destination ?? "pc").Trim().ToLowerInvariant();

        try
        {
            if (destination == "folder")
            {
                var filePath = await reportExportService.ExportShiftToFolderAsync(parsedDate, dto.shift, HttpContext.RequestAborted);
                return Ok(new { message = "Report saved to folder.", filePath });
            }

            // Default: download to the user's PC.
            var (bytes, fileName) = await reportExportService.BuildWorkbookBytesAsync(parsedDate, dto.shift, HttpContext.RequestAborted);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to export report.", detail = ex.Message });
        }
    }
}

public record ReportConfigDto(string? folderPath, bool autoSaveEnabled);
public record ReportRunDto(string production_date, int shift, string? destination);