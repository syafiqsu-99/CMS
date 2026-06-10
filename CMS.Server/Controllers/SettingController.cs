using CMS.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CMS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingController(SettingService settingService) : ControllerBase
{
    // ── Password ───────────────────────────────────────────────────────────────

    [HttpGet("password")]
    public async Task<IActionResult> GetPasswords()
    {
        var result = await settingService.LoadDepartmentPasswords();
        return Ok(result);
    }

    [HttpPut("password")]
    public async Task<IActionResult> UpdatePasswords([FromBody] Dictionary<string, JsonElement> payload)
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

            var result = await settingService.ChangeDepartmentPasswords(passwords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to write passwords to PLC.", detail = ex.Message });
        }
    }

    // ── PLC Signals ────────────────────────────────────────────────────────────

    [HttpGet("plc-signals")]
    public async Task<IActionResult> GetPlcSignals()
    {
        try
        {
            var result = await settingService.ReadAllSubPlcSignals();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    [HttpGet("plc-signals/{id:int}")]
    public async Task<IActionResult> GetPlcSignalById(int id)
    {
        try
        {
            var result = await settingService.ReadSubPlcSignalById(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }
}