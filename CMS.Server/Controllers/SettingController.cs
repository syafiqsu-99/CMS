// CMS.Server/Controllers/SettingController.cs
using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingController(SettingService settingService) : ControllerBase
{
    // ── Password ───────────────────────────────────────────────────────────────

    [HttpPut("password")]
    public IActionResult UpdatePasswords([FromBody] Dictionary<string, JsonElement> payload)
    {
        try
        {
            var passwords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, element) in payload)
            {
                if (element.TryGetInt32(out int val))
                    passwords[key] = val;
                else
                    return BadRequest(new { error = $"Invalid value for '{key}'. Must be an integer." });
            }

            if (passwords.Count == 0)
                return BadRequest(new { error = "No valid department passwords supplied." });

            return Ok(settingService.ChangeDepartmentPasswords(passwords));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to write passwords to PLC.", detail = ex.Message });
        }
    }

    // ── PLC Signals ────────────────────────────────────────────────────────────

    [HttpGet("plc-signals")]
    public IActionResult GetPlcSignals([FromQuery] int? machineId)
    {
        if (machineId.HasValue)
        {
            if (machineId.Value < 1 || machineId.Value > 26)
                return BadRequest(new { error = "machineId must be between 1 and 26." });

            try { return Ok(settingService.ReadSubPlcSignals(machineId.Value)); }
            catch (Exception ex) { return StatusCode(503, new { error = ex.Message }); }
        }

        return Ok(settingService.ReadAllPlcSignals());
    }
}