using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachinesController : ControllerBase
{
    private readonly MachinesService _machineService;
    
    public MachinesController(MachinesService machineService)
    {
        _machineService = machineService;
    }
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _machineService.LoadMachineMasterAsync());

    [HttpGet("{id:int}/utilities")]
    public async Task<IActionResult> Utilities(int id)
        => Ok(await _machineService.LoadUtilities(id));
}