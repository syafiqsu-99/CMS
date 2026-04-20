using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("Attendance")]
    public async Task<ActionResult> Attendance()
    {
        var data = await _dashboardService.LoadAttendance();
        return Ok(data);
    }
}
