using CMS.server.Services;
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/machines")]
public class MachinesController(MachineMasterService machineService, MachineLogService logService)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await machineService.LoadMachineMasterAsync());

    [HttpPost("mould-change")]
    public async Task<IActionResult> MouldChange([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            await logService.UpdateMouldChange(payload);
            return Ok(new { message = "Mould changed successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline()
        => Ok(await logService.LoadTimeline());

    [HttpGet("{id:int}/utilities")]
    public async Task<IActionResult> Utilities(int id)
        => Ok(await logService.LoadUtilities(id));
}