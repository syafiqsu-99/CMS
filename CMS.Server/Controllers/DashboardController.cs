using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly SupervisorService _supervisorService;
    public DashboardController(DashboardService dashboardService, SupervisorService supervisorService)
    {
        _dashboardService = dashboardService;
        _supervisorService = supervisorService;
    }

    [HttpGet("Attendance")]
    public async Task<ActionResult> Attendance()
    {
        var data = await _dashboardService.LoadAttendance();
        return Ok(data);
    }

    // ── Staff Photo ───────────────────────────────────────────────────────────

    [HttpGet("staff-photo/{staffId:int}")]
    public IActionResult GetStaffPhoto(int staffId)
    {
        var path = _supervisorService.GetStaffPhotoPath(staffId);
        if (string.IsNullOrEmpty(path))
            return NotFound();

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime);
    }
}
