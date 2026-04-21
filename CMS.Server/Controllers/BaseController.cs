using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseController : ControllerBase
{
    private readonly BaseService _baseService;
    public BaseController(BaseService baseService)
    {
        _baseService = baseService;
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
}