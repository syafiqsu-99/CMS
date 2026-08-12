using Microsoft.AspNetCore.Mvc;
using CMS.Server.Services;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseController : ControllerBase
{
    private readonly BaseService _baseService;
    private readonly IWebHostEnvironment _env;

    public BaseController(BaseService baseService, IWebHostEnvironment env)
    {
        _baseService = baseService;
        _env = env;
    }

    [HttpGet("Health")]
    public IActionResult Health()
    {
        try
        {
            return Ok(new
            {
                status = "Ready",
                timestamp = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "Backend Error",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpGet("Maintenance")]
    public IActionResult Maintenance()
    {
        var flagPath = Path.Combine(_env.ContentRootPath, "maintenance.flag");

        if (!System.IO.File.Exists(flagPath))
            return Ok(new { active = false, shutdownAt = (long?)null });

        try
        {
            var raw = System.IO.File.ReadAllText(flagPath).Trim();
            if (long.TryParse(raw, out var shutdownAt))
                return Ok(new { active = true, shutdownAt });

            return Ok(new { active = true, shutdownAt = (long?)null });
        }
        catch
        {
            return Ok(new { active = true, shutdownAt = (long?)null });
        }
    }
}