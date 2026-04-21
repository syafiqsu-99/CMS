using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly SupervisorService _supervisorService;
    private readonly BaseService _baseService;
    public DashboardController(DashboardService dashboardService, SupervisorService supervisorService, BaseService baseService)
    {
        _dashboardService = dashboardService;
        _supervisorService = supervisorService;
        _baseService = baseService;
    }

    [HttpGet("Attendance")]
    public async Task<ActionResult> Attendance()
    {
        var data = await _dashboardService.LoadAttendance();
        return Ok(data);
    }

    [HttpGet("staff-photo/{staff_id:int}")]
    public IActionResult GetStaffPhoto(int staff_id)
    {
        var path = _supervisorService.GetStaffPhotoPath(staff_id);
        if (string.IsNullOrEmpty(path)) return NotFound();

        var mime = Path.GetExtension(path).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime);
    }

    [HttpGet("timeline")]
    public async Task<ActionResult> Timeline()
    {
        var data = await _baseService.LoadTimeline();
        return Ok(data);
    }
}
