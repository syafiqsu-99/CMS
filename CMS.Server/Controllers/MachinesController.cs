using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachinesController : ControllerBase
{
    private readonly MachinesService _machineService;
    private readonly BaseService _baseService;
    public MachinesController(MachinesService machineService, BaseService baseService)
    {
        _machineService = machineService;
        _baseService = baseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _machineService.LoadMachineMaster());

    [HttpGet("{id:int}/utilities")]
    public async Task<IActionResult> Utilities(int id)
        => Ok(await _machineService.LoadUtilities(id));

    [HttpGet("timeline")]
    public async Task<ActionResult> Timeline()
    {
        var data = await _baseService.LoadTimeline();
        return Ok(data);
    }
}