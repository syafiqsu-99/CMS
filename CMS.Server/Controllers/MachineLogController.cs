using CMS.server.Services;
using CMS.Server.Models;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachineLogController : ControllerBase
{
    private readonly MachineLogService _machineLogService;
    private readonly PlcService _plcService;
    private readonly ExcelGenerationService _excelService;

    public MachineLogController(MachineLogService machineLogService, PlcService plcService, ExcelGenerationService excelService)
    {
        _machineLogService = machineLogService;
        _plcService = plcService;
        _excelService = excelService;
    }

    [HttpGet("Health")]
    public IActionResult Health()
    {
        try
        {
            return Ok(new
            {
                status = "Ready",
                timestamp = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "Backend Error",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpGet("DailyReport")]
    public async Task<ActionResult> DailyReport(DateOnly production_date, int shift)
    {
        var data = await _machineLogService.LoadDailyReport(production_date, shift);
        return Ok(data);
    }

    [HttpGet("PrevReport")]
    public async Task<ActionResult> PrevReport(DateOnly production_date, int shift)
    {
        var data = await _machineLogService.LoadPrevReport(production_date, shift);
        return Ok(data);
    }

    [HttpGet("ExportReport")]
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

            var reportData = await _machineLogService.LoadExcelReport(parsedDate, shift);

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

            var excelFile = _excelService.GenerateExcelReport(reportData, parsedDate, shift);

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

    [HttpPut("DailyReport")]
    public async Task<ActionResult> UpdateDailyReport([FromBody] JsonElement payload)
    {
        var reportList = payload.GetProperty("reportList").Deserialize<List<Dictionary<string, JsonElement>>>();
        var productionDate = payload.GetProperty("production_date").GetDateTime();
        var shift = payload.GetProperty("shift").GetInt32();

        var data = await _machineLogService.UpdateDailyReport(reportList, DateOnly.FromDateTime(productionDate), shift);
        return Ok(data);
    }
    [HttpPut("PrevReport")]
    public async Task<ActionResult> UpdatePrevReport([FromBody] JsonElement payload)
    {
        var reportList = payload.GetProperty("reportList").Deserialize<List<Dictionary<string, JsonElement>>>();
        var productionDate = payload.GetProperty("production_date").GetDateTime();
        var shift = payload.GetProperty("shift").GetInt32();

        var data = await _machineLogService.UpdatePrevReport(reportList, DateOnly.FromDateTime(productionDate), shift);
        return Ok(data);
    }

    [HttpGet("MachineMaster")]
    public async Task<ActionResult> MachineMaster()
    {
        var data = await _machineLogService.LoadMachineMaster();
        return Ok(data);
    }

    [HttpPost("MachineMaster")]
    public async Task<ActionResult> MouldChange([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            await _machineLogService.UpdateMouldChange(payload);
            return Ok(new { message = "Successfully Change Mould" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("Utilities")]
    public async Task<ActionResult> Utilities([FromQuery] int id_machine)
    {
        var data = await _machineLogService.LoadUtilities(id_machine);
        return Ok(data);
    }

    [HttpGet("SAP")]
    public async Task<ActionResult> SAP()
    {
        var data = await _machineLogService.LoadSAP();
        return Ok(data);
    }

    [HttpPut("SAP")]
    public async Task<ActionResult> UpdateSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem == null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields" });

        try
        {
            await _machineLogService.UpdateSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("SAP")]
    public async Task<ActionResult> DeleteSAP([FromQuery] int id_type, [FromQuery] int mould)
    {
        try
        {
            await _machineLogService.DeleteSAP(id_type, mould);

            return Ok(new { message = "Deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("SAP")]
    public async Task<ActionResult> InsertSAP([FromBody] Dictionary<string, JsonElement> sapItem)
    {
        if (sapItem == null || !sapItem.ContainsKey("id_type") || !sapItem.ContainsKey("mould"))
            return BadRequest(new { error = "Missing required fields" });

        try
        {
            await _machineLogService.InsertSAP(sapItem);
            return Ok(new { message = "Successfully updated SAP" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("SAP/ImportSAP")]
    public async Task<ActionResult> ImportSAP([FromBody] List<Dictionary<string, JsonElement>> sapItems)
    {
        if (sapItems == null || sapItems.Count == 0)
            return BadRequest(new { error = "No data provided" });

        try
        {
            await _machineLogService.ImportSAP(sapItems);
            return Ok(new { message = $"Successfully imported {sapItems.Count} SAP records" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("Attendance")]
    public async Task<ActionResult> Attendance()
    {
        var data = await _machineLogService.LoadAttendance();
        return Ok(data);
    }

    [HttpGet("Timeline")]
    public async Task<ActionResult> Timeline()
    {
        var data = await _machineLogService.LoadTimeline();
        return Ok(data);
    }

    [HttpGet("StaffSchedule")]
    public async Task<ActionResult> StaffSchedule()
    {
        var data = await _machineLogService.LoadStaffSchedule();
        return Ok(data);
    }

    [HttpPut("StaffSchedule")]
    public async Task<ActionResult> UpdateStaffSchedule([FromBody] JsonElement staffList)
    {
        try
        {
            if (!staffList.ValueKind.Equals(JsonValueKind.Array) || staffList.GetArrayLength() == 0)
            {
                return BadRequest(new { error = "No staff data provided" });
            }

            var staffData = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(staffList.GetRawText());

            await _machineLogService.UpdateStaffSchedule(staffData);
            return Ok(new { message = $"Successfully updated {staffData.Count} staff records" });

        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("Staff")]
    public async Task<ActionResult> InsertStaff([FromBody] JsonElement staff)
    {
        try
        {
            var staffData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(staff.GetRawText());
            if (staffData == null)
                return BadRequest(new { error = "Invalid staff data" });

            await _machineLogService.InsertStaff(new List<Dictionary<string, JsonElement>> { staffData });

            return Ok(new { message = "Staff inserted successfully", staff_id = staffData["staff_id"] });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("Staff")]
    public async Task<ActionResult> UpdateStaff([FromBody] JsonElement staff)
    {
        try
        {
            var staffData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(staff.GetRawText());
            if (staffData == null)
                return BadRequest(new { error = "Invalid staff data" });

            await _machineLogService.UpdateStaff(new List<Dictionary<string, JsonElement>> { staffData });

            return Ok(new { message = "Staff updated successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("Staff")]
    public async Task<ActionResult> DeleteStaff([FromBody] JsonElement staff)
    {
        try
        {
            var staffData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(staff.GetRawText());
            if (staffData == null)
                return BadRequest(new { error = "Invalid staff data" });

            await _machineLogService.DeleteStaff(new List<Dictionary<string, JsonElement>> { staffData });

            return Ok(new { message = "Staff deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("StaffPhoto/{staff_id}")]
    public IActionResult GetStaffPhoto(int staff_id)
    {
        try
        {
            var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "staff_list");
            var fileName = $"{staff_id}.jpg";
            var filePath = Path.Combine(rootPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "Photo not found" });
            }

            var imageBytes = System.IO.File.ReadAllBytes(filePath);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("StaffPhoto")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadStaffPhoto([FromForm] StaffPhoto model)
    {
        try
        {
            if (model.file == null || model.file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            if (model.staff_id <= 0)
                return BadRequest(new { error = "Invalid staff_id." });

            var rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "staff_list");

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            var fileName = $"{model.staff_id}.jpg";
            var filePath = Path.Combine(rootPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.file.CopyToAsync(stream);
            }

            return Ok(new { message = "Photo uploaded successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("OEE")]
    public async Task<ActionResult> OEE([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date, [FromQuery] int shift)
    {
        var data = await _machineLogService.OEECalculation(start_date, end_date, shift);
        return Ok(data);
    }

    [HttpGet("Reject")]
    public async Task<ActionResult> Reject([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date, [FromQuery] int shift)
    {
        var data = await _machineLogService.LoadReject(start_date, end_date);
        return Ok(data);
    }

    [HttpGet("Output")]
    public async Task<ActionResult> Output([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date, [FromQuery] int shift)
    {
        var data = await _machineLogService.LoadOutput(start_date, end_date);
        return Ok(data);
    }

    [HttpGet("Downtime")]
    public async Task<ActionResult> Downtime([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date, [FromQuery] int shift)
    {
        var data = await _machineLogService.LoadDowntime(start_date, end_date);
        return Ok(data);
    }
}
