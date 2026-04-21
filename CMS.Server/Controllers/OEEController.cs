using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OeeController : ControllerBase
{
    private readonly OEEService _oeeService;
    private readonly ExcelGenerationService _excelService;
    public OeeController(OEEService oeeService, ExcelGenerationService excelGenerationService)
    {
        _oeeService = oeeService;
        _excelService = excelGenerationService;
    }
    // ── Summary ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Oee(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date,
        [FromQuery] int shift)
        => Ok(await _oeeService.CalculateOeeAsync(start_date, end_date, shift));

    [HttpGet("reject")]
    public async Task<IActionResult> Reject(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadRejectAsync(start_date, end_date));

    [HttpGet("output")]
    public async Task<IActionResult> Output(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadOutputAsync(start_date, end_date));

    [HttpGet("downtime")]
    public async Task<IActionResult> Downtime(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadDowntimeAsync(start_date, end_date));

    // ── Machine Detail ─────────────────────────────────────────────────────────

    [HttpGet("machine/{id:int}/product-output")]
    public async Task<IActionResult> ProductOutput(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineProductOutputAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/daily-output")]
    public async Task<IActionResult> DailyOutput(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineDailyOutputAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/downtime-category")]
    public async Task<IActionResult> DowntimeCategory(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineDowntimeCategoryAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/downtime-events")]
    public async Task<IActionResult> DowntimeEvents(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineDowntimeEventsAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/reject")]
    public async Task<IActionResult> MachineReject(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineRejectAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/cycle-time")]
    public async Task<IActionResult> CycleTime(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineCycleTimeAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/shift-performance")]
    public async Task<IActionResult> ShiftPerformance(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineShiftPerformanceAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/utilities")]
    public async Task<IActionResult> MachineUtilities(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadMachineUtilitiesAsync(id, start_date, end_date));

    [HttpGet("ExportOEE")]
    public async Task<IActionResult> ExportOEE(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date,
        [FromQuery] int shift)
    {
        try
        {
            if (shift != 1 && shift != 2)
                return BadRequest(new { message = "Invalid shift. Use 1 (Morning) or 2 (Night)." });

            if (end_date < start_date)
                return BadRequest(new { message = "end_date must be on or after start_date." });

            var rawRows = await _oeeService.LoadOEERawForExport(start_date, end_date, shift);

            if (rawRows.Count == 0)
                return NotFound(new
                {
                    message = "No OEE data found for the specified date range and shift.",
                    start_date = start_date.ToString("yyyy-MM-dd"),
                    end_date = end_date.ToString("yyyy-MM-dd"),
                    shift
                });

            var excelBytes = _excelService.GenerateOEEReport(rawRows, start_date, end_date);

            var shiftName = shift == 1 ? "Morning" : "Night";
            var fileName = $"OEE_Report_{start_date:yyyy-MM-dd}_to_{end_date:yyyy-MM-dd}.xlsx";

            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExportOEE] Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}