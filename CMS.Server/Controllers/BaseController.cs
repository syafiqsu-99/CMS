using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseController : ControllerBase
{
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
}