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
    public async Task<IActionResult> Oee([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.CalculateOeeAsync(start_date, end_date));

    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly([FromQuery] int? year)
        => Ok(await _oeeService.CalculateMonthlyOeeAsync(year ?? DateTime.Now.Year));

    [HttpGet("reject")]
    public async Task<IActionResult> Reject([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadRejectAsync(start_date, end_date));

    [HttpGet("output")]
    public async Task<IActionResult> Output([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadOutputAsync(start_date, end_date));

    [HttpGet("downtime")]
    public async Task<IActionResult> Downtime([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await _oeeService.LoadDowntimeAsync(start_date, end_date));

    // ── Machine Detail ─────────────────────────────────────────────────────────

    [HttpGet("machine/{id:int}/detail")]
    public async Task<IActionResult> MachineDetail(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
    {
        try
        {
            var data = await _oeeService.LoadMachineDetailAsync(id, start_date, end_date);
            return Ok(data);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("ExportOEE")]
    public async Task<IActionResult> ExportOEE([FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
    {
        try
        {

            if (end_date < start_date)
                return BadRequest(new { message = "end_date must be on or after start_date." });

            var rawRows = await _oeeService.LoadOEERawForExport(start_date, end_date);

            if (rawRows == null || !rawRows.Any())
                return NotFound(new
                {
                    message = "No OEE data found for the specified date range and shift.",
                    start_date = start_date.ToString("yyyy-MM-dd"),
                    end_date = end_date.ToString("yyyy-MM-dd")
                });

            var excelBytes = _excelService.GenerateOEEReport(rawRows, start_date, end_date);

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

    [HttpGet("utilization-by-type")]
    public async Task<IActionResult> UtilizationByType([FromQuery] int month, [FromQuery] int year)
    {
        if (month is < 1 or > 12)
            return BadRequest(new { error = "month must be between 1 and 12." });

        return Ok(await _oeeService.LoadUtilizationByTypeAsync(month, year));
    }

    // ── Unplanned downtime (% of total downtime, per month) ──────────────────────

    [HttpGet("unplanned-downtime")]
    public async Task<IActionResult> UnplannedDowntime([FromQuery] int? year)
        => Ok(await _oeeService.LoadUnplannedDowntimeAsync(year ?? DateTime.Now.Year));

    // ── Mould setup hours (ISBM + EBM only) ──────────────────────────────────────

    [HttpGet("mould-setup")]
    public async Task<IActionResult> MouldSetup([FromQuery] int? year)
        => Ok(await _oeeService.LoadMouldSetupAsync(year ?? DateTime.Now.Year));

    // ── Production wastage by material group ─────────────────────────────────────

    [HttpGet("wastage")]
    public async Task<IActionResult> Wastage([FromQuery] int? year)
        => Ok(await _oeeService.LoadWastageAsync(year ?? DateTime.Now.Year));

    // ── Material group mapping (config) ──────────────────────────────────────────

    [HttpGet("material-groups")]
    public async Task<IActionResult> GetMaterialGroups()
        => Ok(await _oeeService.GetMaterialGroupsAsync());

    [HttpPut("material-groups")]
    public async Task<IActionResult> UpdateMaterialGroups([FromBody] Dictionary<string, List<string>> groups)
    {
        if (groups is null)
            return BadRequest(new { error = "No mapping supplied." });

        await _oeeService.SaveMaterialGroupsAsync(groups);
        return Ok(new { message = "Material groups saved." });
    }

    // ── Mould setup targets (config) ─────────────────────────────────────────────

    [HttpGet("mould-targets")]
    public async Task<IActionResult> GetMouldTargets()
        => Ok(await _oeeService.GetMouldTargetsAsync());

    [HttpPut("mould-targets")]
    public async Task<IActionResult> UpdateMouldTargets([FromBody] MouldTargetsDto dto)
    {
        await _oeeService.SaveMouldTargetsAsync(dto.blow, dto.half, dto.full);
        return Ok(new { message = "Mould targets saved." });
    }
}

public record MouldTargetsDto(double blow, double half, double full);