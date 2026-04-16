using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

/// <summary>
/// Common functionality shared by all CMS controllers.
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    [HttpGet("/api/health")]
    public IActionResult Health() => Ok(new { status = "Ready", timestamp = DateTime.UtcNow });
}