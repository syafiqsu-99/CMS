using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OeeController(OEEService oeeService) : ControllerBase
{
    // ── Summary ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Oee(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date,
        [FromQuery] int shift)
        => Ok(await oeeService.CalculateOeeAsync(start_date, end_date, shift));

    [HttpGet("reject")]
    public async Task<IActionResult> Reject(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadRejectAsync(start_date, end_date));

    [HttpGet("output")]
    public async Task<IActionResult> Output(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadOutputAsync(start_date, end_date));

    [HttpGet("downtime")]
    public async Task<IActionResult> Downtime(
        [FromQuery] DateOnly start_date,
        [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadDowntimeAsync(start_date, end_date));

    // ── Machine Detail ─────────────────────────────────────────────────────────

    [HttpGet("machine/{id:int}/product-output")]
    public async Task<IActionResult> ProductOutput(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineProductOutputAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/daily-output")]
    public async Task<IActionResult> DailyOutput(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineDailyOutputAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/downtime-category")]
    public async Task<IActionResult> DowntimeCategory(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineDowntimeCategoryAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/downtime-events")]
    public async Task<IActionResult> DowntimeEvents(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineDowntimeEventsAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/reject")]
    public async Task<IActionResult> MachineReject(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineRejectAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/cycle-time")]
    public async Task<IActionResult> CycleTime(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineCycleTimeAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/shift-performance")]
    public async Task<IActionResult> ShiftPerformance(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineShiftPerformanceAsync(id, start_date, end_date));

    [HttpGet("machine/{id:int}/utilities")]
    public async Task<IActionResult> MachineUtilities(int id, [FromQuery] DateOnly start_date, [FromQuery] DateOnly end_date)
        => Ok(await oeeService.LoadMachineUtilitiesAsync(id, start_date, end_date));
}