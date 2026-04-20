using CMS.server.Services;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachinesController : BaseController
{
    private readonly MachineMasterService _machineService;
    private readonly MachineLogService _machineLogService;

    public MachinesController(MachineMasterService machineService, MachineLogService logService)
    {
        _machineService = machineService;
        _machineLogService = logService;
    }
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _machineService.LoadMachineMasterAsync());

    [HttpPost("mould-change")]
    public async Task<IActionResult> MouldChange([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            await _machineLogService.UpdateMouldChange(payload);
            return Ok(new { message = "Mould changed successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline()
        => Ok(await _machineLogService.LoadTimeline());

    [HttpGet("{id:int}/utilities")]
    public async Task<IActionResult> Utilities(int id)
        => Ok(await _machineLogService.LoadUtilities(id));
}